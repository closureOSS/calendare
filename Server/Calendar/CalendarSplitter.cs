using System;
using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using Calendare.Server.Middleware;
using Calendare.VSyntaxReader.Components;

namespace Calendare.Server.Calendar;

public class CalendarSplitter
{
    public Models.Principal Owner { get; }
    public CaldavUri Uri { get; }
    public Models.Principal CurrentUser { get; set; }


    public CalendarSplitter(Models.Principal owner, Models.Principal currentUser, CaldavUri uri)
    {
        Owner = owner;
        CurrentUser = currentUser;
        Uri = uri;
    }

    public List<CollectionObject> Split(VCalendar vCalendar)
    {
        if (vCalendar.Builder is null)
        {
            throw new ArgumentNullException(nameof(vCalendar));
        }
        var result = new List<CollectionObject>();
        var recurringComponents = vCalendar.Children.OfType<RecurringComponent>().GroupBy(rc => rc.Uid ?? Guid.NewGuid().ToString(), StringComparer.Ordinal);
        foreach (var occGroup in recurringComponents)
        {
            var davname = $"{Uri.Path!}{occGroup.Key}";
            if (!davname.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))
            {
                davname += ".ics";
            }
            var groupedCalendar = vCalendar.Builder.CreateCalendar();
            groupedCalendar.ProductIdentifier = vCalendar.ProductIdentifier;
            // TODO: Remove or make optional - copy VTIMEZONE to instance, but we don't use it internally at all
            // foreach (var vTimezone in vCalendar.Children.OfType<VTimezone>())
            // {
            //     vTimezone.CopyTo(groupedCalendar);
            // }
            foreach (var comp in occGroup)
            {
                groupedCalendar.AddChild(comp);
            }
            var mainComponent = occGroup.FirstOrDefault() ?? throw new ArgumentException("No main component in recurring component found", nameof(vCalendar));
            mainComponent.Uid ??= occGroup.Key;
            var collectionObject = new CollectionObject
            {
                CalendarItem = new(),
                OwnerId = Owner.UserId,
                ActualUserId = CurrentUser.UserId,
                Uri = davname,
                Uid = mainComponent.Uid ?? throw new ArgumentException("Uid is null", nameof(vCalendar)),
                VObjectType = mainComponent.Name,
            };
            collectionObject.UpdateWith(groupedCalendar, true);
            mainComponent.ReadCommonProperties(collectionObject);
            result.Add(collectionObject);
        }
        // TODO: Other components (currently known on the top level: VAVAILABILITY, VPOLL)
        // var otherComponents = vCalendar.Children.Where(c => c is not RecurringComponent);
        // foreach (var other in otherComponents)
        // {
        //     var otherCalendar = vCalendar.Builder.CreateCalendar();
        //     otherCalendar.AddChild(other);
        //     var otherContent = otherCalendar.Serialize();
        //     var etag = otherContent.ComputeMD5Hash();
        //     var davname = $"{Uri.Path!}{firstEvent.Uid}";

        // }
        return result;
    }
}
