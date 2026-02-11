using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Text;
using Serilog;

namespace Calendare.Server.Handlers;


public partial class PostHandler : HandlerBase, IMethodHandler
{
    private async Task PushRegisterRequest(HttpContext httpContext, DavResource resource, XDocument xml)
    {
        if (!Env.HasFeatures(CalendareFeatures.WebdavPush, httpContext))
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotImplemented);
            return;
        }
        if (!resource.Privileges.HasAnyOf(PrivilegeMask.Read))
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.Read);
            return;
        }
        if (xml.Root is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
            return;
        }
        if (resource.Current is null)
        {
            // 403 push-not-available
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;

        }
        var subscriptionRegistry = httpContext.RequestServices.GetRequiredService<PushSubscriptionRepository>();
        //
        // Subscription details
        //
        var xmlSubscription = xml.Root.Element(XmlNs.Bitfire + "subscription");
        if (xmlSubscription is null)
        {
            Log.Warning("PULL Subscription missing subscription definition");
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
            return;
        }
        var xmlWebPushSubscription = xmlSubscription.Element(XmlNs.Bitfire + "web-push-subscription");
        if (xmlWebPushSubscription is null)
        {
            Log.Warning("PULL Subscription missing subscription definition, only web-push-subscription is supported)");
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
            return;
        }
        var xmlPushResource = xmlWebPushSubscription.Element(XmlNs.Bitfire + "push-resource");
        if (xmlPushResource is null)
        {
            // 403 invalid-subscription
            Log.Warning("PULL Subscription push-resource must be defined");
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
            return;
        }
        var subscription = await subscriptionRegistry.GetByDestinationUri(resource.CurrentUser.UserId, resource.Current.Id, xmlPushResource.Value, httpContext.RequestAborted) ?? new PushSubscription
        {
            Resource = resource.Current,
            UserId = resource.CurrentUser.UserId,
            PushDestinationUri = xmlPushResource.Value,
            SubscriptionId = $"{resource.CurrentUser.UserId}.{resource.Current.Id}".UrlEncodedMD5Hash(),
        };
        if (subscription.UserId != resource.CurrentUser.UserId)
        {
            // user must match for updated
            Log.Warning("PULL Subscription can only be changed by the user {userId} itself {currentUserId}", subscription.UserId, resource.CurrentUser.UserId);
            // 403 push-not-available
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;

        }
        if (subscription.Resource.Id != resource.Current.Id)
        {
            // collection must be the same for update
            Log.Warning("PULL Subscription for collection can't be moved {resourceId} itself {currentId}", subscription.Resource.Id, resource.Current.Id);
            // 403 push-not-available
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;
        }

        var xmlPublicKey = xmlWebPushSubscription.Element(XmlNs.Bitfire + "subscription-public-key");
        if (xmlPublicKey is not null)
        {
            subscription.ClientPublicKey = xmlPublicKey.Value;
            subscription.ClientPublicKeyType = "p256dh"; // TODO: get from xml attribute type
        }
        var xmlAuthSecret = xmlWebPushSubscription.Element(XmlNs.Bitfire + "auth-secret");
        if (xmlAuthSecret is not null)
        {
            subscription.AuthSecret = xmlAuthSecret.Value;
        }
        var xmlContentEncoding = xmlWebPushSubscription.Element(XmlNs.Bitfire + "content-encoding");
        if (xmlContentEncoding is not null)
        {
            subscription.ContentEncoding = xmlContentEncoding.Value;
        }
        else
        {
            subscription.ContentEncoding = "aes128gcm";
        }
        // 403 invalid-subscription
        // 403 push-not-available
        // End of subscription details


        //
        // Trigger
        //
        var xmlTrigger = xml.Root.Element(XmlNs.Bitfire + "trigger");
        // 403 no-supported-trigger
        // End of trigger

        //
        // Expiry
        //
        var now = SystemClock.Instance.GetCurrentInstant();
        var maxExpiry = now.Plus(Duration.FromDays(90));
        subscription.Created = now;
        subscription.Expiration = maxExpiry;
        var xmlExpires = xml.Root.Element(XmlNs.Bitfire + "expires");
        if (xmlExpires is not null)
        {
            var expiry = ParseDateTime(xmlExpires.Value);
            if (expiry is not null)
            {
                if (expiry < now)
                {
                    Log.Warning("PULL Subscription is already expired");
                    await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest);
                    return;
                }
                subscription.Expiration = expiry.Value;
            }
        }
        subscription.Expiration = Instant.Min(subscription.Expiration, maxExpiry);
        var result = await subscriptionRegistry.Amend(subscription, httpContext.RequestAborted);


        var response = httpContext.Response;
        response.Headers.Expires = FormatDateTime(result.Expiration);
        // End of expiry
        response.Headers.Location = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{PathBase}/{resource.CurrentUser.Username}/{CollectionUris.PushSubscription}/{result.SubscriptionId}";
        await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
        return;
    }

    private static Instant? ParseDateTime(string value)
    {
        // Sun, 06 Nov 1994 08:49:37 GMT
        // Obsolete RFC 850: Sunday, 06-Nov-94 08:49:37 GMT
        // Obsolete ANSI C: Sun Nov  6 08:49:37 1994
        var formIMFfixdate = ZonedDateTimePattern.CreateWithInvariantCulture(@"ddd, d MMM yyyy HH:mm:ss z", DateTimeZoneProviders.Tzdb);
        var parseResult = formIMFfixdate.Parse(value);
        if (parseResult.Success)
        {
            return parseResult.Value.ToInstant();
        }
        return null;
    }

    private static string FormatDateTime(Instant value)
    {
        var formIMFfixdate = InstantPattern.CreateWithInvariantCulture(@"ddd, d MMM yyyy HH:mm:ss \G\M\T");
        return formIMFfixdate.Format(value);
    }
}
