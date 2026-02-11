using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Models;

public enum PropertyUpdateResult
{
    /// <summary>
    /// Property not found, does not exist
    /// </summary>
    NotFound,
    /// <summary>
    /// Property set to value
    /// </summary>
    Success,
    /// <summary>
    /// Property found, but not allowed to access
    /// </summary>
    Forbidden,
    /// <summary>
    /// Ignore property (do not report)
    /// </summary>
    Ignore,
    /// <summary>
    /// Property exists, but operation is failing due to bad data
    /// </summary>
    BadRequest,
}

public record class DavProperty
{
    public required XName Name { get; init; }
    public bool IsExpensive { get; init; }
    public XElement? Element { get; set; }
    public List<DavResourceType>? TypeRestrictions { get; set; }

    public Func<XElement, XElement?, DavResource, HttpContext, Task<PropertyUpdateResult>>? GetValue { get; set; }
    public Func<XElement, DavResource, Collection, HttpContext, Task<PropertyUpdateResult>>? Update { get; set; }
    public Func<XElement, DavResource, Collection, HttpContext, Task<PropertyUpdateResult>>? Remove { get; set; }
    public Func<DavResource, string?, bool>? Matches { get; set; }
}
