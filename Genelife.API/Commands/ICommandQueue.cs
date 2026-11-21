namespace Genelife.Api.Commands;

public interface ICommandQueue
{
    bool TryEnqueue(Intent intent, out string? error);
    int Count { get; }
}