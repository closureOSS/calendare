using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Utils;

public static class XMLExtensions
{
    public static async Task<(XDocument? Doc, bool Success)> BodyAsXmlAsync(this HttpRequest request, CancellationToken ct)
    {
        if (request.Body is null || request.ContentLength is null || request.ContentLength == 0)
        {
            return (null, true);
        }
        try
        {
            var xml = await XDocument.LoadAsync(request.Body, LoadOptions.None, ct);
            return (xml, true);
        }
        catch (XmlException xe)
        {
            Log.Error("XML is malformed: {error}", xe.Message);
            return (null, false);
        }
    }

    public static string InnerXMLToString(this XElement el)
    {
        var reader = el.CreateReader();
        reader.MoveToContent();
        return reader.ReadInnerXml();
    }

    public static async Task BodyXmlAsync(this HttpResponse response, XDocument doc, HttpStatusCode httpStatusCode, CancellationToken ct)
    {
        response.ContentType = $"{MimeContentTypes.Xml}; {MimeContentTypes.Utf8}";
        response.StatusCode = (int)httpStatusCode;
        doc.Declaration = new XDeclaration("1.0", "utf-8", null);
        using StringWriter writer = new Utf8StringWriter();
        doc.Save(writer, SaveOptions.None);
        var docContent = writer.ToString();
        response.ContentLength = Encoding.UTF8.GetByteCount(docContent);
        await response.WriteAsync(docContent, ct);
    }

    public static string XMLToString(this XDocument xml)
    {
        xml.Declaration = new XDeclaration("1.0", "utf-8", null);
        using StringWriter writer = new Utf8StringWriter();
        xml.Save(writer, SaveOptions.None);
        var content = writer.ToString();
        return content;
    }

    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }
}
