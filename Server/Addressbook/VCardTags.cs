using System.Collections.Generic;
using System.Linq;
using FolkerKinzel.VCards.Enums;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FolkerKinzel.VCards;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class VCardTags
{
    public static Prop? Lookup(string tag)
    {
        if (Tags.TryGetValue(tag, out var value))
        {
            return value;
        }
        return null;
    }

    public static string? Lookup(Prop prop)
    {
        if (Tags.ContainsValue(prop))
        {
            return Tags.First(z => z.Value == prop).Key;
        }
        return null;
    }

    private static readonly Dictionary<string, Prop> Tags = new(System.StringComparer.Ordinal)
    {
        { "PROFILE", Prop.Profile},
        { "KIND", Prop.Kind},
        { "REV", Prop.Updated},
        { "UID", Prop.ContactID},
        { "CATEGORIES", Prop.Categories},
        { "TZ", Prop.TimeZones},
        { "GEO", Prop.GeoCoordinates},
        { "CLASS", Prop.Access},
        { "SOURCE", Prop.Sources},
        { "NAME", Prop.DirectoryName},
        { "MAILER", Prop.Mailer},
        { "PRODID", Prop.ProductID},
        { "FN", Prop.DisplayNames},
        { "N", Prop.NameViews},
        { "GENDER", Prop.GenderViews},
        { "NICKNAME", Prop.NickNames},
        { "TITLE", Prop.Titles},
        { "ROLE", Prop.Roles},
        { "ORG", Prop.Organizations},
        { "BDAY", Prop.BirthDayViews},
        { "BIRTHPLACE", Prop.BirthPlaceViews},
        { "ANNIVERSARY", Prop.AnniversaryViews},
        { "DEATHDATE", Prop.DeathDateViews},
        { "DEATHPLACE", Prop.DeathPlaceViews},
        { "ADR", Prop.Addresses},
        { "TEL", Prop.Phones},
        { "EMAIL", Prop.EMails},
        { "URL", Prop.Urls},
        { "IMPP", Prop.Messengers},
        { "KEY", Prop.Keys},
        { "CALURI", Prop.CalendarAddresses},
        { "CALADRURI", Prop.CalendarUserAddresses},
        { "FBURL", Prop.FreeOrBusyUrls},
        { "CAPURI", Prop.CalendarAccessUris},
        { "RELATED", Prop.Relations},
        { "MEMBER", Prop.Members},
        { "ORG-DIRECTORY", Prop.OrgDirectories},
        { "EXPERTISE", Prop.Expertises},
        { "INTEREST", Prop.Interests},
        { "HOBBY", Prop.Hobbies},
        { "LANG", Prop.Language},
        { "XML", Prop.Xmls},
        { "LOGO", Prop.Logos},
        { "PHOTO", Prop.Photos},
        { "SOUND", Prop.Sounds},
        { "CLIENTPIDMAP", Prop.AppIDs},
    };

}
