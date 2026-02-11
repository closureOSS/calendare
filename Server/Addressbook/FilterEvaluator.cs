using System;
using System.Collections.Generic;
using System.Linq;
using Calendare.Data.Models;
using FolkerKinzel.VCards;
using FolkerKinzel.VCards.Enums;
using FolkerKinzel.VCards.Models.Properties;
using Serilog;

namespace Calendare.Server.Addressbook;

public class FilterEvaluator
{
    public AddressbookFilter? Filter { get; private set; }

    public void Compile(AddressbookFilter? filter)
    {
        if (filter is null)
        {
            return;
        }
        Filter = filter;
    }

    public bool Matches(CollectionObject co)
    {
        if (Filter is null)
        {
            return true;
        }
        if (co.AddressItem is null)
        {
            return false;
        }
        var vcard = Vcf.Parse(co.RawData);
        if (vcard is not null)
        {
            var vc = vcard.FirstOrDefault();
            if (vc is null)
            {
                return false;
            }
            if (vcard.Count != 1)
            {
                throw new Exception("Vcard has more than one item");
            }
            bool anyMatch = false;
            foreach (var propFilter in Filter.PropFilters)
            {
                var matchPropFilter = MatchPropFilter(vc, propFilter);
                if (matchPropFilter == true && anyMatch == false)
                {
                    anyMatch = true;
                }
                if (matchPropFilter == false && Filter.LogicalAnd == true)
                {
                    return false;
                }
            }
            return !Filter.LogicalAnd && anyMatch;
        }
        return true;
    }

    private static bool MatchPropFilter(VCard vc, PropertyFilter propFilter)
    {
        // TODO: group prefix x-abc.name vs name (https://datatracker.ietf.org/doc/html/rfc6352#section-10.5.1)
        IGrouping<string?, KeyValuePair<Prop, VCardProperty>> grouped = vc.Groups.First(x => true || string.Equals(x.Key, "x-abc", StringComparison.OrdinalIgnoreCase));
        var propertyMatches = grouped.Where(x => x.Key == propFilter.VCardProperty);
        bool anyMatch = false;
        if (propertyMatches is not null && propertyMatches.Any())
        {
            if (propFilter.IsNotDefined == true)
            {
                return false;
            }
            foreach (KeyValuePair<Prop, VCardProperty> kvp in propertyMatches)
            {
                var testValue = kvp.Value.ToString();
                Log.Information("Filter {propFilter}/{propKey} against [{value}]", propFilter.Name, kvp.Key, testValue);
                foreach (var textMatch in propFilter.TextMatches ?? [])
                {
                    var check = textMatch(testValue);
                    // var ccc = testValue.Contains("aro", StringComparison.InvariantCultureIgnoreCase);
                    if (check == true && anyMatch == false)
                    {
                        anyMatch = true;
                    }
                    if (check == false && propFilter.LogicalAnd == true)
                    {
                        return false;
                    }
                }
                foreach (var paramMatch in propFilter.ParamFilters ?? [])
                {
                    var pp = kvp.Value.Parameters;
                    string? paramTestValue = null;
                    switch (paramMatch.Name)
                    {
                        case "TYPE":
                            paramTestValue = pp.PropertyClass.HasValue ? pp.PropertyClass.ToString() : null;
                            break;

                        default:
                            if (pp.NonStandard is not null)
                            {
                                var nsp = pp.NonStandard.FirstOrDefault(x => string.Equals(x.Key, paramMatch.Name, StringComparison.OrdinalIgnoreCase));
                                if (!string.IsNullOrEmpty(nsp.Key))
                                {
                                    paramTestValue = nsp.Value;
                                }
                            }
                            break;
                    }
                    if (paramTestValue is not null)
                    {
                        foreach (var textMatch in paramMatch.TextMatches ?? [])
                        {
                            var check = textMatch(paramTestValue);
                            if (check == true && anyMatch == false)
                            {
                                anyMatch = true;
                            }
                            if (check == false && propFilter.LogicalAnd == true)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (propFilter.IsNotDefined == true)
            {
                return true;
            }
        }
        return !propFilter.LogicalAnd && anyMatch;
    }
}
