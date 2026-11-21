using Genelife.Components.Gen;

namespace Genelife.Api.Commands;

public abstract record SimCommand
{
    public NetworkId TargetNetworkId { get; init; }
}

public sealed record MoveCommand : SimCommand
{
}

public sealed record InteractCommand : SimCommand
{
    public NetworkId OtherNetworkId { get; init; }
}

public sealed record UseCommand : SimCommand
{
}

public sealed record SpawnCommand
{
    public string Name { get; init; } = string.Empty;
    public int Age { get; init; } = 18;
}

public sealed record AssignCommand : SimCommand
{
    public string PlayerId { get; init; } = string.Empty;
}

public sealed record TransferCommand : SimCommand
{
    public string FromPlayerId { get; init; } = string.Empty;
    public string ToPlayerId { get; init; } = string.Empty;
}