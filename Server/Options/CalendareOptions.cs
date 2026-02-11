using System.Collections.Generic;

namespace Calendare.Server.Options;

public class CalendareOptions
{
    public string PathBase { get; set; } = "/caldav.php";
    public bool IsTestMode { get; set; }
    public List<ClientFeatureSet> Features { get; set; } = [];
    public List<TimezoneAlias> TimezoneAliases { get; set; } = [];
}
