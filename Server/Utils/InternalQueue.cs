using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Calendare.Server.Utils;


public class InternalQueue<T> // where T : new()
{
    private readonly Channel<T> Queue;

    public InternalQueue()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        };
        Queue = Channel.CreateUnbounded<T>(options);
    }

    public async Task Push(T msg)
    {
        await Queue.Writer.WriteAsync(msg);
    }

    public async Task<T?> Pop(CancellationToken ct)
    {
        var msg = await Queue.Reader.ReadAsync(ct);
        return msg;
    }
}
