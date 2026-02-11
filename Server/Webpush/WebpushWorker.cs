using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using WebPush;

namespace Calendare.Server.Webpush;

public class WebpushWorker : BackgroundService
{
    private readonly InternalQueue<SyncMsg> Queue;
    private readonly IServiceProvider ServiceProvider;

    public WebpushWorker(InternalQueue<SyncMsg> queue, IServiceProvider serviceProvider)
    {
        Queue = queue;
        ServiceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        await Consumer(ct);
    }

    private async Task Consumer(CancellationToken ct)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var repoPush = scope.ServiceProvider.GetRequiredService<PushSubscriptionRepository>();
            var repoToken = scope.ServiceProvider.GetRequiredService<ItemRepository>();
            var vapidOptions = scope.ServiceProvider.GetRequiredService<IOptions<VapidOptions>>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            while (!ct.IsCancellationRequested)
            {
                var msg = await Queue.Pop(ct);
                if (msg is not null)
                {
                    // Log.Information("Changes in collection {collectionId} announced", msg.CollectionId);
                    // await Task.Delay(500, ct);  // wait for overlapping changes ...
                    // check if push subscription for collection exists
                    var subscriptions = await repoPush.ListByCollectionId(msg.CollectionId, ct);
                    if (subscriptions.Count != 0)
                    {
                        // if yes, retrieve sync token
                        var topic = subscriptions[0].Resource.PermanentId.ToBase64Url();
                        var syncTokenInfo = await repoToken.GetCurrentSyncToken(msg.CollectionId, ct);
                        var syncToken = syncTokenInfo.Uri;
                        Log.Information("Changes in collection {collectionId} {topic} announced as {syncToken}", msg.CollectionId, topic, syncToken);
                        // and send push message
                        foreach (var subscription in subscriptions)
                        {
                            try
                            {
                                using var client = httpClientFactory.CreateClient();
                                client.BaseAddress = new Uri(subscription.PushDestinationUri);
                                // var success = await SendPushMessage(client, subscription, webpushOptions.Value, topic, CreateXmlPushMessage(topic, syncToken), ct);
                                var webPushClient = new WebPushClient(client);
                                var success = await SendPushMessage(webPushClient, subscription, vapidOptions.Value, topic, CreateXmlPushMessage(topic, syncToken), ct);
                                if (success)
                                {
                                    await repoPush.MarkSuccess(subscription, ct);
                                }
                                else
                                {
                                    await repoPush.MarkFailure(subscription, ct);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Sending push message failed");
                                await repoPush.MarkFailure(subscription, ct);
                            }
                        }
                    }
                }
                else
                {
                    await Task.Delay(100, ct);
                }
            }
        }
    }

    private static XDocument CreateXmlPushMessage(string topic, string syncToken)
    {
        var xmlPushMessage = new XElement(XmlNs.Bitfire + "push-message");
        var xmlDoc = new XDocument(xmlPushMessage);
        if (xmlDoc.Root is null) throw new InvalidOperationException("XDocument must contain a root object");
        xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.DavPrefix, XmlNs.Dav));
        xmlPushMessage.Add(new XElement(XmlNs.Bitfire + "topic", topic));

        var xmlContentUpdate = new XElement(XmlNs.Bitfire + "content-update");
        xmlContentUpdate.Add(new XElement(XmlNs.Dav + "sync-token", syncToken));
        xmlPushMessage.Add(xmlContentUpdate);

        var xmlPropertyUpdate = new XElement(XmlNs.Bitfire + "property-update");
        xmlPushMessage.Add(xmlPropertyUpdate);
        return xmlDoc;
    }

    private static async Task<bool> SendPushMessage(IWebPushClient webPushClient, Calendare.Data.Models.PushSubscription subscriptionOptions, VapidOptions vapid, string topic, XDocument xml, CancellationToken ct)
    {
        WebPush.PushSubscription subscription = new PushSubscription(subscriptionOptions.PushDestinationUri, subscriptionOptions.ClientPublicKey!, subscriptionOptions.AuthSecret!);
        var uri = new Uri(subscription.Endpoint);
        try
        {
            var payload = xml.XMLToString();
            var options = new WebPushOptions
            {
                VapidDetails = new VapidDetails($"mailto:{subscriptionOptions.User!.Email!}", vapid.PublicKey!, vapid.PrivateKey!),
                Topic = topic,
                ContentEncoding = ContentEncoding.Aes128gcm,
            };
            await webPushClient.SendNotificationAsync(subscription, payload, options, ct);
            // var body = new StringContent(content, Encoding.UTF8, "application/xml");
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Sending push message {uri} failed", uri);
            return false;
        }
    }
}
