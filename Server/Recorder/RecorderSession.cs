using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Recorder;

public class RecorderSession
{
    public string? Method { get; private set; }
    public string? Path { get; private set; }
    public string? RequestBody { get; private set; }
    // public string? RequestLeader { get; private set; }
    public List<string> RequestHeaders { get; private set; } = [];

    public List<string> ResponseHeaders { get; private set; } = [];
    public string? ResponseBody { get; private set; }
    // public string? ResponseStatus { get; private set; }
    public string? ResponseError { get; private set; }
    public HttpStatusCode ResponseStatusCode { get; private set; }
    private readonly InternalQueue<TrxJournal> Queue;

    public RecorderSession(InternalQueue<TrxJournal> queue)
    {
        Queue = queue;
    }

    public void SetRequestBody(string body)
    {
        RequestBody = ClearNull(body);
    }

    public void SetRequestBody(XDocument xml)
    {
        //doc.Declaration = new XDeclaration("1.0", "utf-8", null);
        using StringWriter writer = new Utf8StringWriter();
        xml.Save(writer, SaveOptions.None);
        RequestBody = ClearNull(writer.ToString());
    }

    public void SetResponseBody(string body)
    {
        ResponseBody = ClearNull(body);
    }

    public void SetResponseBody(XDocument xml)
    {
        //doc.Declaration = new XDeclaration("1.0", "utf-8", null);
        using StringWriter writer = new Utf8StringWriter();
        xml.Save(writer, SaveOptions.None);
        ResponseBody = ClearNull(writer.ToString());
    }

    public void SetRequest(HttpRequest request)
    {
        Method = request.Method;
        Path = request.Path;
        // RequestLeader = $"{request.Method} {request.Path} {request.Protocol}";
        foreach (var header in request.Headers)
        {
            var value = ClearNull(header.Value.ToString());
            var key = ClearNull(header.Key);
            RequestHeaders.Add($"{key}: {value}");
        }
    }

    private static string? ClearNull(string? input)
    {
        return string.IsNullOrEmpty(input) ? null : input!.Replace('\0', '§');
    }

    public void SetResponse(HttpResponse response, XDocument? xml = null)
    {
        if (xml is not null) SetRequestBody(xml);
        // var sc = ReasonPhrases.GetReasonPhrase(response.StatusCode);
        // ResponseStatus = $"{response.StatusCode} {sc}";
        ResponseStatusCode = (HttpStatusCode)response.StatusCode;
        foreach (var header in response.Headers)
        {
            var value = ClearNull(header.Value.ToString());
            var key = ClearNull(header.Key);
            ResponseHeaders.Add($"{key}: {value}");
        }
    }

    public void SetResponse(Exception? ex)
    {
        if (ex is not null)
        {
            ResponseStatusCode = HttpStatusCode.InternalServerError;
            // ResponseStatus = $"EXCEPTION_{ex.GetType()}";
            ResponseError = $"EXCEPTION_{ex.GetType()}\n\n{ex.Message}";
        }
    }

    public async Task Write()
    {
        await Queue.Push(this.ToDto());
    }

    // public override string ToString()
    // {
    //     return $"# REQUEST\n\n{RequestLeader}\n{string.Join('\n', RequestHeaders)}\n\n{RequestBody}\n\n# RESPONSE\n\nHTTP/1.1 {ResponseStatus}\n{string.Join('\n', ResponseHeaders)}\n\n{ResponseBody}{(string.IsNullOrEmpty(ResponseError) ? "" : $"# ERROR\n\n{ResponseError}")}";
    // }

    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding { get { return Encoding.UTF8; } }
    }
}
