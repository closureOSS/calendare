using System;
using System.Net;
using System.Threading.Tasks;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Options;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace Calendare.Server.Middleware;

internal class CaldavMiddleware : IMiddleware
{
    private readonly IOptions<CaldavOptions> ConfigOptions;
    private readonly string PathBase;
    private readonly ResourceRepository ResourceRepository;

    public CaldavMiddleware(IOptions<CaldavOptions> configOptions, IOptions<CalendareOptions> calendareOptions, ResourceRepository resourceRepository)
    {
        ConfigOptions = configOptions;
        PathBase = calendareOptions.Value.PathBase;
        ResourceRepository = resourceRepository;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var config = ConfigOptions.Value;
        if (context.Request.Path.StartsWithSegments("/.well-known", StringComparison.OrdinalIgnoreCase, out var wellKnownPath))
        {
            var recorder = context.RequestServices.GetRequiredService<RecorderSession>();
            recorder.SetRequest(context.Request);
            switch (wellKnownPath)
            {
                case "/carddav":
                case "/caldav":
                    context.Response.Headers.CacheControl = "no-cache";
                    if (context.User.Identity?.IsAuthenticated == true)
                    {
                        context.Response.Redirect($"{PathBase}/{context.User.Identity.Name}/", true);
                    }
                    else
                    {
                        context.Response.Redirect($"{PathBase}/", true);
                    }
                    break;

                // TODO: Handle /timezone or silently ignore ??

                case "/ischedule":
                    // NOT SUPPORTED
                    // TODO: Remove with all related components
                    if (config.Handlers.TryGetValue($"SCHEDULE#{context.Request.Method}", out var handlerType))
                    {
                        var handler = (IMethodHandler)context.RequestServices.GetRequiredService(handlerType);
                        try
                        {
                            Log.Debug("TODO");
                            DavResource resource = default!;
                            await handler.HandleRequestAsync(context, resource);
                            recorder.SetResponse(context.Response);
                            return;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Handler for {method} failed", $"SCHEDULE#{context.Request.Method}");
                            recorder.SetResponse(e);
                            throw;
                        }
                        finally
                        {
                            await recorder.Write();
                        }
                    }
                    else
                    {
                        Log.Warning("Method {method} no supported", $"SCHEDULE#{context.Request.Method}");
                        await next(context);
                    }
                    break;

                default:
                    Log.Warning("Skipped .well-known request, unknown {wellknownPath}", wellKnownPath);
                    await next(context);
                    break;
            }
            recorder.SetResponse(context.Response);
            try
            {
                await recorder.Write();
            }
            catch (Exception e)
            {
                Log.Error(e, "Handler for {method} failed", context.Request.Method);
            }
            return;
        }
        if (context.Request.Path.StartsWithSegments(PathBase, System.StringComparison.OrdinalIgnoreCase))
        {
            if (config.Handlers.TryGetValue(context.Request.Method, out var handlerType))
            {
                // Log.Information("Using handler {handlerType}", handlerType);
                if (IsAuthenticationRequired(context))
                {
                    await context.ChallengeAsync();
                    return;
                }

                var handler = (IMethodHandler)context.RequestServices.GetRequiredService(handlerType);
                var recorder = context.RequestServices.GetRequiredService<RecorderSession>();
                recorder.SetRequest(context.Request);
                try
                {
                    // TODO: Should we set the server id in the header?
                    // context.Response.Headers.Server = "Calendare";
                    var resource = await ResourceRepository.GetAsync(context, context.RequestAborted);
                    if (resource is not null)
                    {
                        await handler.HandleRequestAsync(context, resource);
                        recorder.SetResponse(context.Response);
                    }
                    else
                    {
                        Log.Error("Handler for {method} on unrecognized resource", context.Request.Method);
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        recorder.SetResponse(context.Response);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Handler for {method} failed", context.Request.Method);
                    recorder.SetResponse(e);
                    throw;
                }
                finally
                {
                    await recorder.Write();
                }
            }
            else
            {
                if (!config.UnsupportedMethods.Contains(context.Request.Method))
                {
                    Log.Warning("Method {method} no supported", context.Request.Method);
                }
                await next(context);
            }
        }
        else
        {
            Log.Debug("Skipped request, because it didn't match the filter.");
            await next(context);
        }
    }

    private static bool IsAuthenticationRequired(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return false;
        }
        if (context.Request.Path == "")
        {
            return false;
        }
        return true;
    }
}
