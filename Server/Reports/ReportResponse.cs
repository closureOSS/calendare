using System.Net;
using System.Xml.Linq;
using Calendare.Data.Models;

namespace Calendare.Server.Reports;

public class ReportResponse
{
    public HttpStatusCode StatusCode { get; init; }
    public XDocument? Doc { get; init; }
    public string? Content { get; init; }
    public string? ContentType { get; init; }
    public string? Comment { get; init; }
    public PrivilegeMask MissingPrivilege { get; init; } = PrivilegeMask.None;
    public bool IsSuccess { get; init; }

    public ReportResponse(XDocument doc)
    {
        Doc = doc;
        StatusCode = HttpStatusCode.OK;
        IsSuccess = true;
    }

    public ReportResponse(string doc, string contentType, HttpStatusCode httpStatus = HttpStatusCode.OK)
    {
        Content = doc;
        ContentType = contentType;
        StatusCode = httpStatus;
        IsSuccess = true;
    }

    public ReportResponse(HttpStatusCode errorCode, string? comment = null)
    {
        StatusCode = errorCode;
        Comment = comment;
    }

    public ReportResponse(PrivilegeMask missingPrivilege)
    {
        MissingPrivilege = missingPrivilege;
        StatusCode = HttpStatusCode.Forbidden;
    }
}
