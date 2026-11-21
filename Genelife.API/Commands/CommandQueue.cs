using System.Threading.Channels;

namespace Genelife.Api.Commands;

public sealed class CommandQueue : ICommandQueue
{
    private readonly Channel<Intent> channel;

    public CommandQueue(int capacity = 4096)
    {
        channel = Channel.CreateBounded<Intent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });
    }

    public int Count => channel.Reader.Count;

    public bool TryEnqueue(Intent intent, out string? error)
    {
        if (channel.Writer.TryWrite(intent))
        {
            error = null;
            return true;
        }

        error = "Command queue is full";
        return false;
    }

    public async ValueTask<Intent?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await channel.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public void Drain(Action<Intent> handler)
    {
        while (channel.Reader.TryRead(out var intent))
            handler(intent);
    }
}