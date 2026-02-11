using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Calendare.Server.Calendar;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.Server.Webpush;
using Calendare.VSyntaxReader.Components;
using Calendare.VSyntaxReader.Parsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository ContainerProperties(this DavPropertyRepository repo)
    {
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.2
            Name = XmlNs.Dav + "displayname",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Current?.DisplayName))
                {
                    prop.Value = resource.Current.DisplayName;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.DisplayName = prop.Value;
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Matches = (resource, searchTerm) =>
            {
                if (resource.Current is null)
                {
                    return false;
                }
                return (resource.Current.DisplayName ?? "").Contains(searchTerm ?? "");
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4791#section-5.2.1
            Name = XmlNs.Caldav + "calendar-description",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Current?.Description))
                {
                    prop.Value = resource.Current.Description;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.Description = prop.Value;
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Remove = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.Description = null;
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // TODO: symbolic-color <D:calendar-color xmlns:D="http://apple.com/ns/ical/" symbolic-color="blue">#1BADF8</D:calendar-color>
            Name = XmlNs.AppleIcal + "calendar-color",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar, DavResourceType.Principal],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Current?.Color))
                {
                    prop.Value = resource.Current.Color;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.Color = prop.Value;
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Remove = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.Color = null;
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // <D:calendar-order xmlns:D="http://apple.com/ns/ical/">5</D:calendar-order>
            Name = XmlNs.AppleIcal + "calendar-order",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current?.OrderBy is not null)
                {
                    prop.Value = $"{resource.Current.OrderBy}";
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                if (int.TryParse(prop.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderBy))
                {
                    resource.Current.OrderBy = orderBy;
                    return Task.FromResult(PropertyUpdateResult.Success);
                }
                return Task.FromResult(PropertyUpdateResult.BadRequest);
            },
            Remove = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.OrderBy = null;
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });

        repo.Register(new DavProperty
        {
            Name = XmlNs.Dav + "description",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Current?.Description))
                {
                    prop.Value = resource.Current.Description;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.Description = prop.InnerXMLToString();
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Remove = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.Description = null;
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://www.rfc-editor.org/rfc/rfc6352#section-6.2.1
            Name = XmlNs.Carddav + "addressbook-description",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Current?.Description))
                {
                    prop.Value = resource.Current.Description;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.Description = prop.Value;
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Remove = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Current.Description = null;
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.1
            Name = XmlNs.Dav + "creationdate",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current is not null)
                {
                    prop.Value = resource.Current.Created.ToRfc3339();
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.7
            Name = XmlNs.Dav + "getlastmodified",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current is not null)
                {
                    prop.Value = resource.Current.Modified.ToRfc2616();
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.5
            Name = XmlNs.Dav + "getcontenttype",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Value = "httpd/unix-directory";
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.1
            Name = XmlNs.Dav + "owner",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current is not null)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}/{resource.Current.Owner.Username}/"));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc5995#section-3.2.1
            Name = XmlNs.Dav + "add-member",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                // TODO: for calendar collections only at the moment, expand to address book
                switch (resource.Current?.CollectionType)
                {
                    case CollectionType.Calendar:
                        if (resource.Current.CollectionSubType == CollectionSubType.Default)
                        {
                            prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.Current.Uri}"));
                        }
                        break;

                    default:
                        break;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6352#section-7.1.2
            Name = XmlNs.Carddav + "principal-address",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                // TODO: add vcard of principal from addressbook
                // TODO: Add support in Admin UI to select an address VCARD
                // prop.Add(new XElement(XmlNamespaces.DavNs + "href", "???/principal.vcf"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.9
            Name = XmlNs.Dav + "resourcetype",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(CollectionNames.Collection));
                switch (resource.Current?.CollectionType)
                {
                    case CollectionType.Calendar:
                        switch (resource.Current?.CollectionSubType)
                        {
                            case CollectionSubType.SchedulingOutbox:
                                prop.Add(new XElement(CollectionNames.CalendarOutbox));
                                break;
                            case CollectionSubType.SchedulingInbox:
                                prop.Add(new XElement(CollectionNames.CalendarInbox));
                                break;
                            default:
                                prop.Add(new XElement(CollectionNames.Calendar));
                                break;
                        }
                        break;

                    case CollectionType.Addressbook:
                        prop.Add(new XElement(CollectionNames.Addressbook));
                        break;

                    default:
                        break;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                var isCalendar = prop.Elements(CollectionNames.Calendar).Any();
                var isAddressbook = prop.Elements(CollectionNames.Addressbook).Any();
                if (isAddressbook && isCalendar)
                {
                    Log.Error("Collections may not be both CalDAV calendars and CardDAV addressbooks at the same time");
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                // TODO: Validation, change type only if collection is empty?
                resource.Current.CollectionType = isCalendar ? CollectionType.Calendar : (isAddressbook ? CollectionType.Addressbook : CollectionType.Collection);
                var resourcetypes = new List<string>();
                foreach (var rt in prop.Elements())
                {
                    // Log.Information($"{rt.Name} {rt.Name.LocalName} {rt.Name.NamespaceName}");
                    resourcetypes.Add($"<{rt.Name.Namespace}:{rt.Name.LocalName}/>");
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // TODO: what is etag vs non-standard ctag ???
            Name = XmlNs.CalenderServer + "getctag",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Current?.Etag))
                {
                    prop.Value = $"\"{resource.Current.Etag}\"";
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.6
            Name = XmlNs.Dav + "getetag",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Current?.Etag))
                {
                    prop.Value = $"\"{resource.Current.Etag}\"";
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });

        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.3
            Name = XmlNs.Dav + "supported-method-set",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                foreach (var method in new string[] {
                    "OPTIONS","PROPFIND","PROPPATCH","REPORT","DELETE","MOVE","GET","PUT","HEAD","ACL",
                    // "LOCK","UNLOCK",
                })
                {
                    prop.Add(new XElement(XmlNs.Dav + "supported-method", new XAttribute("name", method)));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.3
            Name = XmlNs.Dav + "supported-report-set",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                // radicale doesn't support principal-* and expand-property reports, but returns an empty result
                prop.AddSupportedReports(CommonReports);
                prop.AddSupportedReports([
                    XmlNs.Dav + "acl-principal-prop-set"
                ]);
                if (resource.Current?.CollectionSubType == CollectionSubType.SchedulingInbox)
                {
                    prop.AddSupportedReports([
                        XmlNs.Caldav + "calendar-query",
                        XmlNs.Caldav + "calendar-multiget",
                    ]);
                }
                if (resource.Current?.CollectionType == CollectionType.Calendar && resource.Current?.CollectionSubType == CollectionSubType.Default)
                {
                    prop.AddSupportedReports([
                        XmlNs.Caldav + "calendar-query",
                        XmlNs.Caldav + "calendar-multiget",
                        XmlNs.Caldav + "free-busy-query",
                    ]);
                }
                if (resource.Current?.CollectionType == CollectionType.Addressbook)
                {
                    prop.AddSupportedReports([
                        XmlNs.Carddav + "addressbook-query",
                        XmlNs.Carddav + "addressbook-multiget",
                    ]);
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-9.1
            Name = XmlNs.Caldav + "schedule-calendar-transp",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.NotFound);
                }
                var propTransparency = new XElement(XmlNs.Caldav + (resource.Current.ScheduleTransparency is null ? ScheduleTransparency.Opaque : resource.Current.ScheduleTransparency));
                prop.Add(propTransparency);
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                var transp = prop.Elements().FirstOrDefault();
                switch (transp?.Name.LocalName.ToLowerInvariant())
                {
                    case ScheduleTransparency.Opaque:
                    case ScheduleTransparency.Transparent:
                        resource.Current.ScheduleTransparency = transp?.Name.LocalName.ToLowerInvariant();
                        break;
                    default:
                        Log.Warning("Calendar transparency {transp} not supported/unknown", transp?.Name.LocalName);
                        return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4791#section-5.2.3
            Name = XmlNs.Caldav + "supported-calendar-component-set",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current?.CollectionType == CollectionType.Calendar)
                {
                    prop.Add(new XElement(XmlNs.Caldav + "comp", new XAttribute("name", ComponentName.VEvent)));
                    prop.Add(new XElement(XmlNs.Caldav + "comp", new XAttribute("name", ComponentName.VTodo)));
                    switch (resource.Current.CollectionSubType)
                    {
                        case CollectionSubType.SchedulingInbox:
                            prop.Add(new XElement(XmlNs.Caldav + "comp", new XAttribute("name", ComponentName.VFreebusy)));
                            prop.Add(new XElement(XmlNs.Caldav + "comp", new XAttribute("name", ComponentName.VAvailability)));
                            prop.Add(new XElement(XmlNs.Caldav + "comp", new XAttribute("name", ComponentName.VPoll)));
                            break;

                        case CollectionSubType.SchedulingOutbox:
                            prop.Add(new XElement(XmlNs.Caldav + "comp", new XAttribute("name", ComponentName.VFreebusy)));
                            break;

                        default:
                            prop.Add(new XElement(XmlNs.Caldav + "comp", new XAttribute("name", ComponentName.VJournal)));
                            break;
                    }
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = IgnoreAmend,
            Remove = IgnoreAmend,
        });
        repo.Register(new DavProperty
        {
            // See https://datatracker.ietf.org/doc/html/rfc4791#section-5.2.3, but name - sets instead of set - apple specific?
            Name = XmlNs.Caldav + "supported-calendar-component-sets",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = IgnoreAmend,
            Remove = IgnoreAmend,
        });
        repo.Register(new DavProperty
        {
            // https://www.rfc-editor.org/rfc/rfc6352#section-6.2.2
            Name = XmlNs.Carddav + "supported-address-data",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                if (resource.Current.CollectionType == CollectionType.Addressbook)
                {
                    prop.Add(new XElement(XmlNs.Carddav + "address-data-type", new XAttribute("content-type", MimeContentTypes.VCard), new XAttribute("version", "4.0")));
                    prop.Add(new XElement(XmlNs.Carddav + "address-data-type", new XAttribute("content-type", MimeContentTypes.VCard), new XAttribute("version", "3.0")));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = IgnoreAmend,
            Remove = IgnoreAmend,
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4791#section-5.2.2
            Name = XmlNs.Caldav + "calendar-timezone",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                // TODO: Implement calendar VTIMEZONE retrieval or not (see RFC7809 support)
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionType != CollectionType.Calendar)
                {
                    return PropertyUpdateResult.Ignore;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.GetProperty(resource.Current, "calendar-timezone", ctx.RequestAborted);
                if (ava is not null)
                {
                    prop.Value = ava.Value;
                }
                return PropertyUpdateResult.Success;
            },
            Update = async (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionType != CollectionType.Calendar)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var calendarBuilder = ctx.RequestServices.GetRequiredService<ICalendarBuilder>();
                var parseResult = calendarBuilder.Parser.TryParse(prop.Value, out var calendar, $"{resource.Owner.Id}");
                if (!parseResult)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                var vtimezones = calendar?.Children.OfType<VTimezone>();
                if (vtimezones is null || vtimezones.Count() != 1)
                {
                    Log.Error("calendar-timezone must contain a VCALENDAR with exactly one VTIMEZONE component");
                    return PropertyUpdateResult.BadRequest;
                }
                var vtimezone = vtimezones.First();
                // Retrieve TzId and use it if recognized (this property contains a VCALENDAR with one VTIMEZONE)
                if (TimezoneParser.TryReadTimezone(vtimezone.TzId, out var timezone))
                {
                    resource.Current.Timezone = timezone!.Id;
                }
                else
                {
                    Log.Warning("Timezone {tzId} not recognized; ignoring supplied VTIMEZONE", vtimezone.TzId);
                }

                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.AmendProperty(resource.Current, "calendar-timezone", prop.Value, resource.CurrentUser.UserId, ctx.RequestAborted);
                return PropertyUpdateResult.Success;
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc7809#section-5.2
            Name = XmlNs.Caldav + "calendar-timezone-id",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current is not null)
                {
                    if (resource.Current.CollectionType == CollectionType.Calendar)
                    {
                        if (resource.Current.Timezone is not null)
                        {
                            prop.Value = resource.Current.Timezone;
                        }
                    }
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                if (resource.Current.CollectionType != CollectionType.Calendar)
                {
                    return Task.FromResult(PropertyUpdateResult.Forbidden);
                }
                if (TimezoneParser.TryReadTimezone(prop.Value, out var timezone))
                {
                    resource.Current.Timezone = timezone!.Id;
                    return Task.FromResult(PropertyUpdateResult.Success);
                }
                Log.Warning("Invalid timezone id {tzId}", prop.Value);
                return Task.FromResult(PropertyUpdateResult.BadRequest);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc7809#section-3.1.2
            // https://datatracker.ietf.org/doc/html/rfc7809#section-5.1
            Name = XmlNs.Caldav + "timezone-service-set",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                // TODO: Investigate to implement timezone service and discovery, current URI is a placeholder [low priority]
                prop.Add(new XElement(XmlNs.Dav + "href", $"https://calendare.closure.ch/timezones"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6578#section-4
            Name = XmlNs.Dav + "sync-token",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                var itemRepository = ctx.RequestServices.GetRequiredService<ItemRepository>();
                var token = await itemRepository.GetCurrentSyncToken(resource.Current.Id, ctx.RequestAborted);
                if (token is not null && token.Id > Guid.Empty)
                {
                    prop.Value = token.Uri;
                }
                return PropertyUpdateResult.Success;
            },
            IsExpensive = true,
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc7953#section-7.2.4
            Name = XmlNs.Caldav + AvailabilityExtensions.PROPERTY_calendar_availability,
            TypeRestrictions = [DavResourceType.Calendar],
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.GetProperty(resource.Current, AvailabilityExtensions.PROPERTY_calendar_availability, ctx.RequestAborted);
                if (ava is not null)
                {
                    prop.Value = ava.Value;
                }
                else
                {
                    // prop.Value = """
                    // BEGIN:VCALENDAR
                    // CALSCALE:GREGORIAN
                    // PRODID:-//example.com//iCalendar 2.0//EN
                    // VERSION:2.0
                    // BEGIN:VAVAILABILITY
                    // UID:9BADC1F6-0FC4-44BF-AC3D-993BEC8C962A
                    // DTSTAMP:20111005T133225Z
                    // DTSTART;TZID=Europe/Zurich:20241101T000000
                    // BEGIN:AVAILABLE
                    // UID:6C9F69C3-BDA8-424E-B2CB-7012E796DDF7
                    // SUMMARY:Monday to Friday from 9:00 to 18:00 (FAKE response, TODO:)
                    // DTSTART;TZID=Europe/Zurich:20111002T090000
                    // DTEND;TZID=Europe/Zurich:20111002T180000
                    // RRULE:FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR
                    // END:AVAILABLE
                    // END:VAVAILABILITY
                    // END:VCALENDAR
                    // """;
                }
                return PropertyUpdateResult.Success;
            },
            Update = async (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.AmendProperty(collection, AvailabilityExtensions.PROPERTY_calendar_availability, prop.Value, resource.CurrentUser.UserId, ctx.RequestAborted);
                return PropertyUpdateResult.Success;
            },
            IsExpensive = true,
        });
        repo.Register(new DavProperty
        {
            // Same as https://datatracker.ietf.org/doc/html/rfc7953#section-7.2.4, just another namespace (MacOS)
            Name = XmlNs.CalenderServer + AvailabilityExtensions.PROPERTY_calendar_availability,
            TypeRestrictions = [DavResourceType.Calendar],
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.GetProperty(resource.Current, AvailabilityExtensions.PROPERTY_calendar_availability, ctx.RequestAborted);
                if (ava is not null)
                {
                    prop.Value = ava.Value;
                }
                else
                {
                    // prop.Value = """
                    // BEGIN:VCALENDAR
                    // CALSCALE:GREGORIAN
                    // PRODID:-//example.com//iCalendar 2.0//EN
                    // VERSION:2.0
                    // BEGIN:VAVAILABILITY
                    // UID:9BADC1F6-0FC4-44BF-AC3D-993BEC8C962A
                    // DTSTAMP:20111005T133225Z
                    // DTSTART;TZID=Europe/Zurich:20241101T000000
                    // BEGIN:AVAILABLE
                    // UID:6C9F69C3-BDA8-424E-B2CB-7012E796DDF7
                    // SUMMARY:Monday to Friday from 9:00 to 18:00 (FAKE response, TODO:)
                    // DTSTART;TZID=Europe/Zurich:20111002T090000
                    // DTEND;TZID=Europe/Zurich:20111002T180000
                    // RRULE:FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR
                    // END:AVAILABLE
                    // END:VAVAILABILITY
                    // END:VCALENDAR
                    // """;
                }
                return PropertyUpdateResult.Success;
            },
            Update = async (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.AmendProperty(collection, AvailabilityExtensions.PROPERTY_calendar_availability, prop.Value, resource.CurrentUser.UserId, ctx.RequestAborted);
                return PropertyUpdateResult.Success;
            },
            IsExpensive = true,
        });
        repo.Register(new DavProperty
        {
            Name = XmlNs.Caldav + "default-alarm-vevent-date",
            TypeRestrictions = [DavResourceType.Calendar],
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.GetProperty(resource.Current, "default-alarm-vevent-date", ctx.RequestAborted);
                if (ava is not null)
                {
                    prop.Value = ava.Value;
                }
                //    <C:default-alarm-vevent-date>BEGIN:VALARM
                //     X-WR-ALARMUID:B8F9D257-8E68-476C-8327-964B2AD5027F
                //     UID:B8F9D257-8E68-476C-8327-964B2AD5027F
                //     TRIGGER:-PT15H
                //     ATTACH;VALUE=URI:Basso
                //     ACTION:AUDIO
                //     END:VALARM
                //     </C:default-alarm-vevent-date>
                return PropertyUpdateResult.Success;
            },
            Update = async (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.AmendProperty(collection, "default-alarm-vevent-date", prop.Value, resource.CurrentUser.UserId, ctx.RequestAborted);
                return PropertyUpdateResult.Success;
            },
            IsExpensive = true,
        });
        repo.Register(new DavProperty
        {
            Name = XmlNs.Caldav + "default-alarm-vevent-datetime",
            TypeRestrictions = [DavResourceType.Calendar],
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.GetProperty(resource.Current, "default-alarm-vevent-datetime", ctx.RequestAborted);
                if (ava is not null)
                {
                    prop.Value = ava.Value;
                }
                // <C:default-alarm-vevent-datetime>BEGIN:VALARM
                // X-WR-ALARMUID:967770A1-EAD9-4422-8D0F-BD7C02F4B0F7
                // UID:967770A1-EAD9-4422-8D0F-BD7C02F4B0F7
                // TRIGGER;VALUE=DATE-TIME:19760401T005545Z
                // ACTION:NONE
                // END:VALARM
                // </C:default-alarm-vevent-datetime>
                return PropertyUpdateResult.Success;
            },
            Update = async (prop, resource, collection, ctx) =>
            {
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (resource.Current.CollectionSubType == CollectionSubType.SchedulingOutbox)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var ava = await collectionRepository.AmendProperty(collection, "default-alarm-vevent-datetime", prop.Value, resource.CurrentUser.UserId, ctx.RequestAborted);
                return PropertyUpdateResult.Success;
            },
            IsExpensive = true,
        });

        repo.Register(new DavProperty
        {
            // https://raw.githubusercontent.com/apple/ccs-calendarserver/refs/heads/master/doc/Extensions/caldav-sharing.txt
            Name = XmlNs.CalenderServer + "allowed-sharing-modes",
            IsExpensive = true,
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar],
            GetValue = async (prop, qry, resource, ctx) =>
            {
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var relationships = await userRepository.GetPrivilegesGrantedToAsync(resource, transitive: false, ctx.RequestAborted);
                if (resource.Current is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                if (!(resource.Current.CollectionType == CollectionType.Calendar || resource.Current.CollectionType == CollectionType.Addressbook))
                {
                    return PropertyUpdateResult.NotFound;
                }
                if (resource.Current.CollectionSubType != CollectionSubType.Default)
                {
                    return PropertyUpdateResult.NotFound;
                }
                // TODO: implement proper handling, check if the modes are allowed ...
                prop.Add(new XElement(XmlNs.CalenderServer + "can-be-shared"));
                prop.Add(new XElement(XmlNs.CalenderServer + "can-be-published"));
                return PropertyUpdateResult.Success;
            }
        });

        repo.Register(new DavProperty
        {
            // https://github.com/bitfireAT/webdav-push/blob/main/content.mkd
            Name = XmlNs.Bitfire + "transports",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar, DavResourceType.Principal],
            GetValue = (prop, qry, resource, ctx) =>
            {
                var env = ctx.RequestServices.GetRequiredService<DavEnvironmentRepository>();
                if (!env.HasFeatures(CalendareFeatures.WebdavPush, ctx))
                {
                    return Task.FromResult(PropertyUpdateResult.NotFound);
                }
                var xmlWebPush = new XElement(XmlNs.Bitfire + "web-push");
                var vapidOptions = ctx.RequestServices.GetService<IOptions<VapidOptions>>();
                if (vapidOptions?.Value?.PublicKey is not null)
                {
                    var publicKeyTag = new XElement(XmlNs.Bitfire + "vapid-public-key", vapidOptions.Value.PublicKey);
                    publicKeyTag.SetAttributeValue("type", "p256ecdsa");
                    xmlWebPush.Add(publicKeyTag);
                }
                prop.Add(xmlWebPush);
                // <P:web-push>
                //   <P:server-public-key type="p256dh">BA1Hxzyi1RUM1b5wjxsn7nGxAszw2u61m164i3MrAIxHF6YK5h4SDYic-dRuU_RCPCfA5aq9ojSwk5Y2EmClBPs</P:server-public-key>
                // </P:web-push>
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            IsExpensive = true,
        });
        repo.Register(new DavProperty
        {
            // https://github.com/bitfireAT/webdav-push/blob/main/content.mkd
            Name = XmlNs.Bitfire + "topic",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar, DavResourceType.Principal],
            GetValue = (prop, qry, resource, ctx) =>
            {
                var env = ctx.RequestServices.GetRequiredService<DavEnvironmentRepository>();
                if (!env.HasFeatures(CalendareFeatures.WebdavPush, ctx))
                {
                    return Task.FromResult(PropertyUpdateResult.NotFound);
                }
                if (resource.Current is not null)
                {
                    prop.Value = resource.Current.PermanentId.ToBase64Url();
                }
                else if (resource.Owner is not null)
                {
                    prop.Value = resource.Owner.PermanentId.ToBase64Url();
                }
                //  <P:topic>O7M1nQ7cKkKTKsoS_j6Z3w</P:topic>
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            IsExpensive = true,
        });
        repo.Register(new DavProperty
        {
            // https://github.com/bitfireAT/webdav-push/blob/main/content.mkd
            Name = XmlNs.Bitfire + "supported-triggers",
            TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar, DavResourceType.Principal],
            GetValue = (prop, qry, resource, ctx) =>
            {
                var env = ctx.RequestServices.GetRequiredService<DavEnvironmentRepository>();
                if (!env.HasFeatures(CalendareFeatures.WebdavPush, ctx))
                {
                    return Task.FromResult(PropertyUpdateResult.NotFound);
                }
                // <P:content-update>
                //   <sync-level>1</sync-level>
                // </P:content-update>
                // <P:property-update>
                //   <depth>0</depth>
                // </P:property-update>
                prop.Add(new XElement(XmlNs.Bitfire + "content-update", new XElement(XmlNs.Bitfire + "depth", 1)));
                prop.Add(new XElement(XmlNs.Bitfire + "property-update", new XElement(XmlNs.Bitfire + "depth", 0)));
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            IsExpensive = true,
        });


        //
        // Ignored properties
        //
        repo.Register(new DavProperty { Name = XmlNs.Caldav + "supported-calendar-data", Update = IgnoreAmend, TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar], IsExpensive = true, });  // Ignored, since we will support iCalendar 2.0 https://datatracker.ietf.org/doc/html/rfc4791#section-5.2.3
        // repo.Register(new DavProperty { Name = XmlNs.Caldav + "calendar-data", Update = IgnoreAmend, IsExpensive = true, });    // Ignored, since we will support iCalendar 2.0 https://datatracker.ietf.org/doc/html/rfc4791#section-9.6
        repo.Register(new DavProperty { Name = XmlNs.Caldav + "max-resource-size", Update = IgnoreAmend, TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar], IsExpensive = true, });// Ignored, since we will support arbitrary size
        repo.Register(new DavProperty { Name = XmlNs.Caldav + "min-date-time", Update = IgnoreAmend, TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar], IsExpensive = true, });    // Ignored, since we will support arbitrary time
        repo.Register(new DavProperty { Name = XmlNs.Caldav + "max-date-time", Update = IgnoreAmend, TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar], IsExpensive = true, });    // Ignored, since we will support arbitrary time
        repo.Register(new DavProperty { Name = XmlNs.Caldav + "max-instances", Update = IgnoreAmend, TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar], IsExpensive = true, });    // Ignored, since we will support arbitrary instances
        repo.Register(new DavProperty { Name = XmlNs.Caldav + "max-attendees-per-instance", Update = IgnoreAmend, TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar], IsExpensive = true, });    // Ignored, since we will support arbitrary instances
        repo.Register(new DavProperty { Name = XmlNs.Carddav + "max-resource-size", Update = IgnoreAmend, TypeRestrictions = [DavResourceType.Addressbook, DavResourceType.Container, DavResourceType.Calendar], IsExpensive = true, });// Ignored, since we will support arbitrary size https://www.rfc-editor.org/rfc/rfc6352#section-6.2.3
        return repo;
    }
}
