using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace Calendare.Server.Recorder;

public class RecorderWorker : BackgroundService
{
    private readonly InternalQueue<TrxJournal> Queue;
    private readonly IServiceProvider ServiceProvider;
    private RecorderOptions Options;


    public RecorderWorker(IOptions<RecorderOptions> options, InternalQueue<TrxJournal> queue, IServiceProvider serviceProvider)
    {
        Options = options.Value;
        Queue = queue;
        ServiceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Yield();
        await Consumer(ct);
    }

    private async Task Consumer(CancellationToken ct)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var siteRepository = scope.ServiceProvider.GetRequiredService<SiteRepository>();

            while (!ct.IsCancellationRequested)
            {
                var msg = await Queue.Pop(ct);
                if (msg is not null)
                {
                    var requestLeader = $"{msg.Method} {msg.Path} HTTP/1.1";
                    var responseStatus = "";
                    if (msg.ResponseStatusCode is not null)
                    {
                        var sc = ReasonPhrases.GetReasonPhrase(msg.ResponseStatusCode.Value);
                        responseStatus = $"{msg.ResponseStatusCode} {sc}";
                    }
                    switch (Options.Mode)
                    {
                        case RecorderOperationMode.Files:
                            try
                            {
                                Log.Information($"REQUEST {requestLeader} --> {responseStatus}");
                                using (StreamWriter outputFile = new StreamWriter(Path.Combine(Options.Directory ?? ".", $"{msg.Method}-{responseStatus}-{Path.GetRandomFileName().Replace('.', 'x')}.trace")))
                                {
                                    await outputFile.WriteAsync($"# REQUEST\n\n{requestLeader}\n{string.Join('\n', msg.RequestHeaders)}\n\n{msg.RequestBody}\n\n# RESPONSE\n\nHTTP/1.1 {responseStatus}\n{string.Join('\n', msg.ResponseHeaders)}\n\n{msg.ResponseBody}{(string.IsNullOrEmpty(msg.ResponseError) ? "" : $"# ERROR\n\n{msg.ResponseError}")}");
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Recording failed {recorder}", this);
                            }
                            break;

                        case RecorderOperationMode.Database:
                            try
                            {
                                Log.Information($"REQUEST {requestLeader} --> {responseStatus}");
                                await siteRepository.AddTrxJournal(msg);
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Recording failed {recorder}", this);
                            }
                            break;

                        default:
                            break;
                    }
                }
            }
        }
    }
}
