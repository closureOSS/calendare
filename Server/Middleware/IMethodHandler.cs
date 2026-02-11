using System.Threading.Tasks;
using Calendare.Server.Models;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Handlers;

public interface IMethodHandler
{
    Task HandleRequestAsync(HttpContext httpContext, DavResource resource);
}
