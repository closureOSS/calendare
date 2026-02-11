using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository CommonProperties(this DavPropertyRepository repo)
    {
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc5397.html#section-3
            // TODO: Apply workaround for iphones according to https://gitlab.com/davical-project/davical/-/commit/b4bcc6cc2570b0fccd53b72152afff023d769dbe
            Name = XmlNs.Dav + "current-user-principal",
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.CurrentUser.Uri}"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-4.2
            Name = XmlNs.Dav + "principal-URL",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.Owner.Uri}"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc5842.html#section-3.1
            Name = XmlNs.Dav + "resource-id",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Current is not null)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"urn:uuid:{resource.Current.PermanentId}"));
                }
                else if (resource.Owner is not null)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"urn:uuid:{resource.Owner.PermanentId}"));
                }
                else
                {
                    return Task.FromResult(PropertyUpdateResult.NotFound);
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            //https://datatracker.ietf.org/doc/html/rfc3744#section-4.1 - not supported, always empty
            Name = XmlNs.Dav + "alternate-URI-set",
            IsExpensive = true,
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.8
            Name = XmlNs.Dav + "principal-collection-set",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4791#section-6.2.1
            Name = XmlNs.Caldav + "calendar-home-set",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                var xx = resource;
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.Owner.Uri}"));
                //prop.Add(new XElement(XmlNs.Dav + "href", "/caldav.php/family/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Matches = (resource, searchTerm) =>
            {
                return $"{resource.PathBase}{resource.Owner.Uri}".Contains(searchTerm ?? "");
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6352#section-7.1.1
            Name = XmlNs.Carddav + "addressbook-home-set",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.Owner.Uri}"));
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Matches = (resource, searchTerm) =>
            {
                return $"{resource.PathBase}{resource.Owner.Uri}".Contains(searchTerm ?? "");
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-2.4.1
            Name = XmlNs.Caldav + "calendar-user-address-set",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                var principal = resource.Owner;
                if (principal is null)
                {
                    return Task.FromResult(PropertyUpdateResult.Forbidden);
                }
                // TODO: Support multiple email addresses for a principal
                if (!string.IsNullOrEmpty(principal.Email) && principal.EmailOk is not null)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"mailto:{principal.Email}"));
                }
                else
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{principal.Uri}"));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Matches = (resource, searchTerm) =>
            {
                var principal = resource.Owner;
                if (principal is null)
                {
                    return false;
                }
                var st = searchTerm ?? "";
                if ($"{resource.PathBase}{principal.Uri}".Contains(st))
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(principal.Email) && principal.EmailOk is not null)
                {
                    return $"mailto:{principal.Email}".Contains(st);
                }
                return false;
            },
        });
        repo.Register(new DavProperty
        {
            // we treat email-address-set equal to https://datatracker.ietf.org/doc/html/rfc6638#section-2.4.1 calendar-user-address-set
            // email-address-set should be the list of email addresses of a principal (content format unclear)
            Name = XmlNs.CalenderServer + "email-address-set",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                var principal = resource.Owner;
                if (principal is null)
                {
                    return Task.FromResult(PropertyUpdateResult.Forbidden);
                }
                // TODO: Support multiple email addresses for a principal
                if (!string.IsNullOrEmpty(principal.Email) && principal.EmailOk is not null)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"mailto:{principal.Email}"));
                }
                else
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{principal.Uri}"));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Matches = (resource, searchTerm) =>
            {
                var principal = resource.Owner;
                if (principal is null)
                {
                    return false;
                }
                var st = searchTerm ?? "";
                if ($"{resource.PathBase}{principal.Uri}".Contains(st))
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(principal.Email) && principal.EmailOk is not null)
                {
                    return $"mailto:{principal.Email}".Contains(st);
                }
                return false;
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-9.2
            Name = XmlNs.Caldav + "schedule-default-calendar-URL",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                // TODO: Concept of a default calender (default target for scheduling)
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.Owner.Uri}{CollectionUris.DefaultCalendar}/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = IgnoreAmend,
            // Update = (prop) =>
            // {
            //     // TODO: Implement
            //     throw new NotImplementedException("schedule-default-calendar-URL");
            // }
        });
        repo.Register(new DavProperty
        {
            // Found in eMClient, assumption: equals schedule-default-calendar-URL (as we store events and tasks in the same calendar collection)
            Name = XmlNs.CalenderServer + "schedule-default-tasks-URL",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                // TODO: Concept of a default calender (default target for scheduling)
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.Owner.Uri}{CollectionUris.DefaultCalendar}/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = IgnoreAmend,
            // Update = (prop) =>
            // {
            //     // TODO: Implement
            //     throw new NotImplementedException("schedule-default-calendar-URL");
            // }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-2.1.1
            Name = XmlNs.Caldav + "schedule-outbox-URL",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource.Owner is null)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var collections = await userRepository.GetCollectionsByType(resource.Owner.UserId, CollectionSubType.SchedulingOutbox, ctx.RequestAborted);
                var outbox = collections.FirstOrDefault();
                if (outbox is not null)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{outbox.Uri}"));
                }
                return PropertyUpdateResult.Success;
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-2.2.1
            Name = XmlNs.Caldav + "schedule-inbox-URL",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource.Owner is null)
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var collections = await userRepository.GetCollectionsByType(resource.Owner.UserId, CollectionSubType.SchedulingInbox, ctx.RequestAborted);
                var inbox = collections.FirstOrDefault();
                if (inbox is not null)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{inbox.Uri}"));
                }
                return PropertyUpdateResult.Success;
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/draft-desruisseaux-caldav-sched-04#section-5.3.1
            Name = XmlNs.Caldav + "calendar-free-busy-set",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                var collectionRepository = ctx.RequestServices.GetRequiredService<CollectionRepository>();
                var collections = await collectionRepository.ListByOwnerAsync(new()
                {
                    CurrentUser = resource.CurrentUser,
                    OwnerUsername = resource.Owner.Username,
                    CollectionTypes = [CollectionType.Calendar],
                    CollectionSubTypes = [CollectionSubType.Default],
                }, ctx.RequestAborted);
                foreach (var col in collections ?? [])
                {
                    if (col.ScheduleTransparency is null || string.Equals(col.ScheduleTransparency, ScheduleTransparency.Opaque, StringComparison.Ordinal))
                    {
                        prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{col.Uri}"));
                    }
                }
                return PropertyUpdateResult.Success;
            },
            Update = IgnoreAmend,
            Remove = IgnoreAmend,
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.3
            Name = XmlNs.Dav + "supported-privilege-set",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                var supportedPrivilegeSet = PrivilegesDefinitions.LoadTree();
                prop.WritePrivilegeSet(supportedPrivilegeSet);
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.6
            Name = XmlNs.Dav + "acl-restrictions",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "grant-only"));
                prop.Add(new XElement(XmlNs.Dav + "no-invert"));
                prop.Add(new XElement(XmlNs.Dav + "required-principal",
                    new XElement(XmlNs.Dav + "property",
                        new XElement(XmlNs.Dav + "owner")
                )));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.7
            Name = XmlNs.Dav + "inherited-acl-set",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.5
            // TODO: Check if user has read-acl privilege!!
            //
            // This is a protected property that specifies the list of access
            //    control entries (ACEs), which define what principals are to get what
            //    privileges for this resource.
            Name = XmlNs.Dav + "acl",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource is null)
                {
                    return PropertyUpdateResult.Ignore;
                }
                if (!resource.Privileges.HasAnyOf(PrivilegeMask.ReadAcl | PrivilegeMask.Write))
                {
                    return PropertyUpdateResult.Forbidden;
                }
                var aceList = new List<AccessControlEntityEx>
                {
                    new()
                    {
                        Grantee = resource.Owner,
                        Privileges = PrivilegeMask.All,
                        Name = XmlNs.Dav + "owner"
                    }
                };
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var relationships = await userRepository.GetPrivilegesGrantedToAsync(resource, transitive: false, ctx.RequestAborted);
                if (relationships is not null && relationships.Count > 0)
                {
                    aceList.AddRange(relationships.Select(x => new AccessControlEntityEx
                    {
                        Grantee = x.Grantee!,
                        Privileges = x.Privileges,
                    }));
                }
                aceList.Add(new()
                {
                    Grantee = resource.CurrentUser,
                    Privileges = resource.CurrentUser.GlobalPermit,
                    Name = XmlNs.Dav + "authenticated"
                });
                // For each principal add a ace - element
                foreach (var ace in aceList)
                {
                    var xmlAce = new XElement(XmlNs.Dav + "ace");
                    var xmlPrincipal = new XElement(XmlNs.Dav + "principal");
                    if (ace.Name is not null)
                    {
                        xmlPrincipal.Add(new XElement(XmlNs.Dav + "property", new XElement(ace.Name)));
                    }
                    else
                    {
                        xmlPrincipal.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{ace.Grantee!.Uri}"));
                    }
                    xmlAce.Add(xmlPrincipal);
                    var xmlGrant = new XElement(XmlNs.Dav + "grant");
                    foreach (var privilege in ace.Grants)
                    {
                        xmlGrant.Add(new XElement(XmlNs.Dav + "privilege", new XElement(privilege.Id)));
                    }
                    xmlAce.Add(xmlGrant);
                    prop.Add(xmlAce);
                }
                return PropertyUpdateResult.Success;
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.4
            // TODO: Check if user has read-current-user-privilege-set
            Name = XmlNs.Dav + "current-user-privilege-set",
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource is null)
                {
                    return Task.FromResult(PropertyUpdateResult.Success);
                }
                if (!resource.Privileges.HasAnyOf(PrivilegeMask.ReadCurrentUserPrivilegeSet | PrivilegeMask.ReadAcl | PrivilegeMask.WriteAcl))
                {
                    return Task.FromResult(PropertyUpdateResult.Forbidden);
                }
                var grantList = PrivilegesDefinitions.LoadList(resource.Privileges, ~resource.PrivilegesSupported);
                foreach (var grant in grantList)
                {
                    prop.Add(new XElement(XmlNs.Dav + "privilege", new XElement(grant.Id)));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://raw.githubusercontent.com/apple/ccs-calendarserver/refs/heads/master/doc/Extensions/caldav-proxy.txt
            Name = XmlNs.CalenderServer + "calendar-proxy-read-for",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var memberships = await userRepository.GetPrincipalMembershipsAsync(resource.Owner.Id, [CollectionSubType.CalendarProxyRead], ctx.RequestAborted);
                SortedSet<string> members = [];
                memberships.ForEach(m => members.Add($"{resource.PathBase}/{m.Username}/"));
                var grants = await userRepository.GetPrivilegesGrantedByAsync(resource.Owner.Id, transitive: true, ctx.RequestAborted);
                var grantTypeFilter = new string[] { "R" };
                grants.Where(g => resource.Owner.Id != g.Grantor?.Id && grantTypeFilter.Contains(g.GrantType?.Confers, StringComparer.Ordinal)).ToList().ForEach(g => members.Add($"{resource.PathBase}/{g.Grantor?.Owner.Username}/"));
                foreach (var member in members)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", member));
                }
                return PropertyUpdateResult.Success;
            }
        });
        repo.Register(new DavProperty
        {
            // https://raw.githubusercontent.com/apple/ccs-calendarserver/refs/heads/master/doc/Extensions/caldav-proxy.txt
            Name = XmlNs.CalenderServer + "calendar-proxy-write-for",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                SortedSet<string> members = [];
                var memberships = await userRepository.GetPrincipalMembershipsAsync(resource.Owner.Id, [CollectionSubType.CalendarProxyWrite], ctx.RequestAborted);
                memberships.ForEach(m => members.Add($"{resource.PathBase}/{m.Username}/"));
                var grants = await userRepository.GetPrivilegesGrantedByAsync(resource.Owner.Id, transitive: true, ctx.RequestAborted);
                var grantTypeFilter = new string[] { "RW", "A" };
                grants.Where(g => resource.Owner.Id != g.Grantor?.Id && grantTypeFilter.Contains(g.GrantType?.Confers, StringComparer.Ordinal)).ToList().ForEach(g => members.Add($"{resource.PathBase}/{g.Grantor?.Owner.Username}/"));
                foreach (var member in members)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", member));
                }
                return PropertyUpdateResult.Success;
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-4.3
            // https://raw.githubusercontent.com/apple/ccs-calendarserver/refs/heads/master/doc/Extensions/caldav-proxy.txt
            // LIST on a proxy principlal (calendar-proxy-XXX-for) all principals which have read/write access (usecase 0524) to main principal
            //    This property of a group principal identifies the principals that are
            //    direct members of this group.  Since a group may be a member of
            //    another group, a group may also have indirect members (i.e., the
            //    members of its direct members).  A URL in the DAV:group-member-set
            //    for a principal MUST be the DAV:principal-URL of that principal.
            //
            //  Short: A group's list of members
            Name = XmlNs.Dav + "group-member-set",
            IsExpensive = true,
            TypeRestrictions = [DavResourceType.Principal],
            GetValue = async (prop, qry, resource, ctx) =>
            {
                if (resource.Current is null || resource.Current?.PrincipalType is null || !string.Equals(resource.Current?.PrincipalType?.Label, PrincipalTypeCode.Group, StringComparison.Ordinal))
                {
                    return PropertyUpdateResult.NotFound;
                }
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var group = await userRepository.GetMembersAsync(resource.DavName, ctx.RequestAborted);
                if (group is null)
                {
                    return PropertyUpdateResult.NotFound;
                }
                var members = group.Members.Select(m => m.Uri).ToHashSet(StringComparer.Ordinal) ?? [];
                // memberships are synthetically added based on the relationship (privileges)
                var grants = await userRepository.GetPrivilegesGrantedToAsync(resource, transitive: false, ctx.RequestAborted);
                var env = ctx.RequestServices.GetRequiredService<DavEnvironmentRepository>();
                if (env.HasFeatures(CalendareFeatures.VirtualProxyMembers, ctx))
                {
                    PrivilegeMask minimalPrivilege = PrivilegeMask.All;
                    if (resource.IsProxyRead())
                    {
                        minimalPrivilege = PrivilegeMask.Read;
                    }
                    else if (resource.IsProxyWrite())
                    {
                        minimalPrivilege = PrivilegeMask.Write;
                    }
                    if (grants is not null)
                    {
                        members.UnionWith(
                            grants.Where(g => g.Grantee?.Id != resource.Current!.Id && g.Privileges.HasFlag(minimalPrivilege)
                            && (minimalPrivilege == PrivilegeMask.Write || !g.Privileges.HasFlag(PrivilegeMask.Write)))
                            .Select(g => g.Grantee?.Uri ?? "*"));
                    }
                }
                foreach (var memberUri in members)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{memberUri}"));
                    // TODO: How to handle groups? RFC almost implies that no resolving of sub-groups is needed, really?
                }
                return PropertyUpdateResult.Success;
            },
            Update = async (prop, resource, collection, ctx) =>
            {
                if (resource.Current?.PrincipalType is null || !string.Equals(resource.Current?.PrincipalType?.Label, PrincipalTypeCode.Group, StringComparison.Ordinal))
                {
                    return PropertyUpdateResult.NotFound;
                }
                var resourceRepository = ctx.RequestServices.GetRequiredService<ResourceRepository>();
                List<Principal> members = [];
                foreach (var href in prop.Elements(XmlNs.Dav + "href"))
                {
                    var granteePrincipal = await resourceRepository.GetResourceAsync(new Middleware.CaldavUri(href.Value, resource.PathBase), ctx, ctx.RequestAborted);
                    if (granteePrincipal is null)
                    {
                        return PropertyUpdateResult.BadRequest;
                    }
                    members.Add(granteePrincipal.Owner);
                }
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                await userRepository.AmendGroupMembersAsync(collection, members, ctx.RequestAborted);
                return PropertyUpdateResult.Success;
            },
        }, XmlNs.CalenderServer + "group-member-set");

        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-4.4
            // https://raw.githubusercontent.com/apple/ccs-calendarserver/refs/heads/master/doc/Extensions/caldav-proxy.txt
            // This protected property identifies the groups in which the principal
            // is directly a member.  Note that a server may allow a group to be a
            // member of another group, in which case the DAV:group-membership of
            // those other groups would need to be queried in order to determine the
            // groups in which the principal is indirectly a member.
            //
            //  Short: A person's list of group memberships
            Name = XmlNs.Dav + "group-membership",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var memberships = await userRepository.GetMembershipAsync(resource.DavName, ctx.RequestAborted);
                if (memberships is null)
                {
                    return PropertyUpdateResult.BadRequest;
                }
                foreach (var group in memberships)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{group.GroupUri}"));
                }
                return PropertyUpdateResult.Success;
            }
        },
        XmlNs.CalenderServer + "group-membership");

        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-04#section-4.4.2
            // https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-03#section-5.5
            // Used to show to whom a resource has been shared.
            Name = XmlNs.Dav + "invite",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var relationships = await userRepository.GetPrivilegesGrantedToAsync(resource, transitive: true, ctx.RequestAborted);
                if (relationships is not null && relationships.Count > 0)
                {
                    foreach (var rs in relationships)
                    {
                        if (rs.Grantee is not null && rs.Grants.Where(x => x.Privileges.HasFlag(PrivilegeMask.Write) || x.Privileges.HasFlag(PrivilegeMask.Read)).Any())
                        {
                            var sharee = new XElement(XmlNs.Dav + "sharee");
                            sharee.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{rs.Grantee.Uri}"));
                            sharee.Add(new XElement(XmlNs.Dav + "invite-accepted"));    // TODO: Other states of invitation (see 5.5)
                            var shareAccess = new XElement(XmlNs.Dav + "share-access");
                            if (rs.Grants.Any(x => x.Privileges == PrivilegeMask.All))
                            {
                                shareAccess.Add(new XElement(XmlNs.Dav + "shared-owner"));
                            }
                            else if (rs.Grants.Any(x => x.Privileges.HasFlag(PrivilegeMask.Write)))
                            {
                                shareAccess.Add(new XElement(XmlNs.Dav + "read-write"));
                            }
                            else
                            {
                                shareAccess.Add(new XElement(XmlNs.Dav + "read"));
                            }
                            sharee.Add(shareAccess);
                            var shareProps = new XElement(XmlNs.Dav + "prop");
                            shareProps.Add(new XElement(XmlNs.Dav + "displayname", rs.Grantee.DisplayName));
                            sharee.Add(shareProps);
                            prop.Add(sharee);
                        }

                    }
                }
                return PropertyUpdateResult.Success;
            }
        },
        XmlNs.CalenderServer + "invite");

        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-04#section-4.4.1
            // https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-03#section-5.5
            // Used to show to whom a resource has been shared.
            Name = XmlNs.Dav + "share-access",
            IsExpensive = true,
            GetValue = async (prop, qry, resource, ctx) =>
            {
                var userRepository = ctx.RequestServices.GetRequiredService<UserRepository>();
                var relationships = await userRepository.GetPrivilegesGrantedToAsync(resource, transitive: true, ctx.RequestAborted);
                if (relationships is not null && relationships.Count > 0)
                {
                    prop.Add(new XElement(XmlNs.Dav + "shared-owner"));   // TODO: Implement other states (not-shared|shared-owner|read|read-write)
                }
                return PropertyUpdateResult.Success;
            }
        },
        XmlNs.CalenderServer + "share-access");

        return repo;
    }

    public static Task<PropertyUpdateResult> IgnoreAmend(XElement _, DavResource _1, Collection _2, HttpContext _3) => Task.FromResult(PropertyUpdateResult.Success);
}
