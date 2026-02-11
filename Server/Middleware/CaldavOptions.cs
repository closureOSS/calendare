
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Calendare.Server.Middleware;



public class CaldavOptions
{
    public Dictionary<string, Type> Handlers { get; } = [];
    public HashSet<string> UnsupportedMethods { get; } = [];
    public Dictionary<XName, Type> Reports { get; } = [];
}
