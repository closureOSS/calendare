using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Models;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Reports;

public interface IReport
{
    public Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext);
}
