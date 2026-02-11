using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Calendare.Server.Models;

namespace Calendare.Server.Repository;

public class DavPropertyRepository
{
    private readonly List<DavProperty> RegistredProperties = [];
    private readonly Dictionary<XName, XName> Aliases = [];

    public DavProperty? Property(XName name, DavResourceType resourceType)
    {
        if (Aliases.TryGetValue(name, out var propertyName))
        {
            var candidates = RegistredProperties
                .FindAll(prop => prop.Name == propertyName && (prop.TypeRestrictions == null || prop.TypeRestrictions.Contains(resourceType)))
                .OrderBy(prop => prop.TypeRestrictions?.Count() ?? 999);
            return candidates.FirstOrDefault();
        }
        return null;
    }

    public void Register(DavProperty property)
    {
        RegistredProperties.Add(property);
        Aliases[property.Name] = property.Name;
    }

    public void Register(DavProperty property, XName aliasName)
    {
        RegistredProperties.Add(property);
        Aliases[property.Name] = property.Name;
        Aliases[aliasName] = property.Name;
    }

    public List<XName> ListDefaultPropertyNames(DavResourceType resourceType)
    {
        var candidates = RegistredProperties
            .FindAll(prop => prop.IsExpensive == false && (prop.TypeRestrictions == null || prop.TypeRestrictions.Contains(resourceType)))
            ;
        // TODO: Cleanup duplicates with different resourceTypes sets
        // var dupes = candidates.GroupBy(x => new { name = x.Name.ToString() }).Where(x => x.Skip(1).Any()).ToList();
        // if (dupes.Count != 0)
        // {

        // }
        return [.. candidates.Select(c => c.Name)];
    }
}
