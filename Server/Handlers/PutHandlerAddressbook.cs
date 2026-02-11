using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using FolkerKinzel.VCards;
using FolkerKinzel.VCards.Enums;
using FolkerKinzel.VCards.Models;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Handlers;


public partial class PutHandler : IMethodHandler
{
    private async Task AmendAddressbookItem(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;

        // the resource type of the parent must be Addressbook
        if (resource is null || resource.Parent is null || resource.ParentResourceType != DavResourceType.Addressbook)
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-9.7.1
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, XmlNs.Dav + "collection-must-exist", "The destination collection does not exist.");
            return;
        }
        var resourceOriginal = resource.ToLight();
        var ifNoneMatch = request.GetIfNoneMatch();
        if (ifNoneMatch && resource.Object is not null)
        {
            Log.Error("URI {uri} is already mapped (If-None-Match)", resource.Uri.Path);
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "if-none-match", "Existing resource matches 'If-None-Match' header - not accepted.");
            return;
        }
        string? bodyContent;
        try
        {
            bodyContent = await request.BodyAsStringAsync(httpContext.RequestAborted);
        }
        catch (InvalidDataException ex)
        {
            Log.Error(ex, "Failed to decode");
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.UnsupportedMediaType, XmlNs.Dav + "content-encoding", "Unable to decode 'xxx' content encoding.");
            return;
        }
        Recorder.SetRequestBody(bodyContent);
        var etag = bodyContent.PrettyMD5Hash();

        var vcard = Vcf.Parse(bodyContent).FirstOrDefault();
        // foreach (var vcard in vcards)
        if (vcard is null)
        {
            // TODO: Check status code (parsing of vcard content failed)
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;
        }
        var vcfData = bodyContent;
        if (vcard.ContactID is null || vcard.ContactID.IsEmpty)
        {
            vcard.ContactID = new FolkerKinzel.VCards.Models.Properties.ContactIDProperty(ContactID.Create(Guid.NewGuid()));
            // VCardBuilder.Create(vcard).ContactID.Set();
            vcfData = Vcf.AsString(vcard, VCdVersion.V3_0);
        }
        var target = resource.Object ?? new();
        target.OwnerId = resource.Owner.UserId;
        target.ActualUserId = resource.CurrentUser.UserId;
        target.Uri = resource.Uri.Path!;
        target.Uid = GetVCardId(vcard.ContactID.Value);
        target.Etag = vcfData.PrettyMD5Hash();
        target.RawData = vcfData;
        target.VObjectType = "VCARD";
        target.AddressItem ??= new();
        var ai = target.AddressItem;
        ai.CardVersion = vcard.Version.ToString();
        ai.FormattedName = vcard.DisplayNames?.FirstOrDefault()?.Value;
        // ai.Name = vcard.NameViews?.FirstOrDefault()?.ToDisplayName(NameFormatter.Default);
        var nv = vcard.NameViews?.FirstOrDefault();
        if (nv is not null)
        {
            ai.Name = NameFormatter.Default.ToDisplayName(nv, vcard);
        }
        ai.Nickname = vcard.NickNames?.FirstOrDefault()?.ToString();
        var opCode = await VerifyOperation(httpContext, resource, resourceOriginal, target);
        if (opCode != DbOperationCode.Failure)
        {
            SetEtagHeader(httpContext.Response, target.Etag);
            await AmendCollectionObject(httpContext, opCode, target);
        }
    }

    private static string GetVCardId(ContactID? contactID) => contactID?.Convert
         (
             guid => guid.ToString(),
             uri => uri.AbsoluteUri,
             str => str
         ) ?? "";

    private static string GetVCardIdUriSafe(ContactID? contactID)
    {
        if (contactID is null || contactID.IsEmpty || contactID.Guid.HasValue == false)
        {
            return Guid.NewGuid().ToString();
        }
        return contactID.Guid.Value.ToString();
    }

    private async Task AmendAddressbook(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;

        var bodyContent = await request.BodyAsStringAsync(httpContext.RequestAborted);
        Recorder.SetRequestBody(bodyContent);
        // var etag = bodyContent.ComputeMD5Hash();

        var vcards = Vcf.Parse(bodyContent);

        if (resource.Current is null)
        {
            if (resource.ParentResourceType == DavResourceType.Principal ||
                (resource.Parent is not null && resource.ParentResourceType == DavResourceType.Container))
            {
                // create calendar
                var collection = new Calendare.Data.Models.Collection
                {
                    Uri = resource.Uri.Path!,
                    ParentContainerUri = $"{resource.Uri.ParentCollectionPath}",
                    ParentId = resource.Parent?.Id ?? resource.Owner.Id,
                    DisplayName = null,
                    // DavDisplayName = resource.Uri.Collection,
                    CollectionType = CollectionType.Addressbook,
                    Etag = $"{resource.Owner.Id}{resource.Uri.Path!}".PrettyMD5Hash(),
                    OwnerId = resource.Owner.UserId,
                };
                resource.Current = await CollectionRepository.CreateAsync(collection, httpContext.RequestAborted);
            }
            if (resource.Current is null)
            {
                await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "collection-must-exist", "The destination collection does not exist.");
                return;
            }
        }

        var collectionObjects = new List<CollectionObject>();
        if (vcards.Count < 1)
        {
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Carddav + "valid-addressbook-data");
            return;
        }
        foreach (var vcard in vcards)
        {
            if (vcard.ContactID is null || vcard.ContactID.IsEmpty)
            {
                vcard.ContactID = new FolkerKinzel.VCards.Models.Properties.ContactIDProperty(ContactID.Create(Guid.NewGuid()));
            }
            var vcfData = Vcf.AsString(vcard, VCdVersion.V3_0);
            var etag = vcfData.PrettyMD5Hash();
            var displayName = vcard.DisplayNames?.FirstOrDefault();
            var collectionObject = new CollectionObject
            {
                OwnerId = resource.Owner.UserId,
                ActualUserId = resource.CurrentUser.UserId,
                Uri = $"{resource.Uri.Path!}{GetVCardIdUriSafe(vcard.ContactID?.Value)}.vcf",
                Uid = GetVCardId(vcard.ContactID?.Value),
                Etag = etag,
                RawData = vcfData,
                VObjectType = "VCARD",
                AddressItem = new ObjectAddress
                {
                    CardVersion = vcard.Version.ToString(),
                    FormattedName = displayName?.Value,
                    Name = vcard.NameViews?.FirstOrDefault() is not null ? NameFormatter.Default.ToDisplayName(vcard.NameViews.First()!, vcard) : null,
                    Nickname = vcard.NickNames?.FirstOrDefault()?.ToString(),
                },
                Collection = resource.Current,
            };

            collectionObjects.Add(collectionObject);
        }
        await ItemRepository.CreateAsync(collectionObjects, httpContext.RequestAborted);
        response.StatusCode = (int)HttpStatusCode.Created;
    }
}
