using Calendare.Data.Models;
using Calendare.Server.Middleware;
using Serilog;

namespace Calendare.Server.Models;

public class DavResource
{
    public string DavName { get; set; }
    public CaldavUri Uri { get; private set; }
    public string? PathBase { get; set; }
    public required Principal Owner { get; init; }
    public required Principal CurrentUser { get; init; }
    public string? DavEtag { get; set; }
    public string? ScheduleTag { get; set; }
    public bool Exists { get; set; }
    public PrivilegeMask Privileges { get; set; } = PrivilegeMask.None;
    public PrivilegeMask PrivilegesSupported { get => CalculateSupportedPrivilegeMask(); }

    public DavResourceType ResourceType { get; set; } = DavResourceType.Unknown;
    public DavResourceType ParentResourceType { get; set; } = DavResourceType.Root;

    public Collection? Parent { get; set; }
    public Collection? Current { get; set; }
    public CollectionObject? Object { get; set; }

    public DavResource()
    {
        ResourceType = DavResourceType.Root;
        Uri = new CaldavUri("/");
        DavName = "/";
    }

    public DavResource(CaldavUri uri)
    {
        Uri = uri;
        ResourceType = uri.IsValid() ? DavResourceType.Unknown : DavResourceType.Root;
        DavName = uri.Path ?? "/";
    }

    public bool VerifyResourceType()
    {
        switch (ResourceType)
        {
            case DavResourceType.Root:
            case DavResourceType.User:
            case DavResourceType.Principal:
                return true;

            case DavResourceType.Container:
            case DavResourceType.Calendar:
            case DavResourceType.Addressbook:
                if (Current is not null)
                {
                    return true;
                }
                Log.Error("PROPFIND collection doesn't exist", DavName);
                return false;

            case DavResourceType.CalendarItem:
            case DavResourceType.AddressbookItem:
                if (Object is not null)
                {
                    return true;
                }
                Log.Error("PROPFIND object doesn't exist", DavName);
                return false;

            case DavResourceType.Unknown:
            default:
                // Log.Error("PROPFIND support for resource missing", resource.DavName);
                Log.Error("Resource {resource} {resourceType} pre-conditions failed", DavName, ResourceType);
                return true;
        }
    }

    public DavResource Graft(ObjectCalendar oc) => Graft(oc.CollectionObject, DavResourceType.CalendarItem);

    public DavResource Graft(ObjectAddress oc) => Graft(oc.CollectionObject, DavResourceType.AddressbookItem);

    private DavResource Graft(CollectionObject obj, DavResourceType davResourceType)
    {
        var clone = new DavResource
        {
            DavName = obj.Uri,
            PathBase = PathBase,
            Uri = new CaldavUri(obj.Uri, PathBase),
            Owner = Owner, // TODO: Handle item set by another user
            CurrentUser = CurrentUser,
            DavEtag = obj.Etag,
            ScheduleTag = obj.ScheduleTag,
            Exists = true,
            Privileges = Privileges,  // TODO: Handle item set by another user or private flags
            ResourceType = davResourceType,
            ParentResourceType = ResourceType,
            Parent = Current ?? obj.Collection,
            Current = null,
            Object = obj,
        };
        return clone;
    }

    public DavResource Graft(Collection current)
    {
        var clone = new DavResource
        {
            DavName = current.Uri,
            PathBase = PathBase,
            Uri = new CaldavUri(current.Uri, PathBase),
            Owner = Owner, // TODO: Handle item set by another user
            CurrentUser = CurrentUser,
            DavEtag = current.Etag,
            Exists = true,
            Privileges = Privileges,  // TODO: Handle item set by another user or private flags
            ResourceType = current.CollectionType.ToResourceType(),
            ParentResourceType = ResourceType,
            Parent = Current,
            Current = current,
            Object = null,
        };
        return clone;
    }

    private PrivilegeMask CalculateSupportedPrivilegeMask()
    {
        var exclude = PrivilegeMask.None;
        if (Exists)
        {
            switch (ResourceType)
            {
                default:
                case DavResourceType.Unknown:
                case DavResourceType.Root:
                case DavResourceType.User:
                    break;

                case DavResourceType.Principal:
                    exclude |= PrivilegeMask.ScheduleDeliver | PrivilegeMask.ScheduleSend;
                    break;

                case DavResourceType.Container:
                    exclude |= PrivilegeMask.ReadFreeBusy | PrivilegeMask.ScheduleDeliver | PrivilegeMask.ScheduleSend;
                    break;

                case DavResourceType.Calendar:
                    exclude |= PrivilegeMask.Share;
                    if (Current is not null)
                    {
                        switch (Current.CollectionSubType)
                        {
                            case CollectionSubType.SchedulingOutbox:
                                exclude |= PrivilegeMask.ReadFreeBusy | PrivilegeMask.ScheduleDeliver;
                                break;
                            case CollectionSubType.SchedulingInbox:
                                exclude |= PrivilegeMask.ReadFreeBusy | PrivilegeMask.ScheduleSend;
                                break;
                            case CollectionSubType.Default:
                            default:
                                exclude |= PrivilegeMask.ScheduleDeliver | PrivilegeMask.ScheduleSend;
                                break;
                        }
                    }
                    break;

                case DavResourceType.Addressbook:
                    exclude |= PrivilegeMask.Unbind | PrivilegeMask.Bind | PrivilegeMask.Share | PrivilegeMask.ReadFreeBusy | PrivilegeMask.ScheduleSend | PrivilegeMask.ScheduleDeliver;
                    break;

                case DavResourceType.CalendarItem:
                    // factually it should be All, as privileges are only supported on collections
                    exclude |= PrivilegeMask.Unbind | PrivilegeMask.Bind | PrivilegeMask.Share | PrivilegeMask.ScheduleDeliver | PrivilegeMask.ScheduleSend | PrivilegeMask.WriteAcl;
                    break;

                case DavResourceType.AddressbookItem:
                    // factually it should be All, as privileges are only supported on collections
                    exclude |= PrivilegeMask.Unbind | PrivilegeMask.Bind | PrivilegeMask.Share | PrivilegeMask.ReadFreeBusy | PrivilegeMask.ScheduleSend | PrivilegeMask.ScheduleDeliver | PrivilegeMask.WriteAcl;
                    break;
            }
        }
        return PrivilegeMask.All & ~exclude;
    }
}
