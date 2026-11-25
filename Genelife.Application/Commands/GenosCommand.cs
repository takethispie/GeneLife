using Genelife.Components.Gen;

namespace Genelife.Api.Commands;

public abstract record GenosCommand
{
    public NetworkId TargetNetworkId { get; init; }
}

public sealed record MoveCommand : GenosCommand
{
}

public sealed record InteractCommand : GenosCommand
{
    public NetworkId OtherNetworkId { get; init; }
}

public sealed record UseCommand : GenosCommand
{
}

public sealed record SpawnCommand
{
    public string Name { get; init; } = string.Empty;
    public int Age { get; init; } = 18;
}

public sealed record AssignCommand : GenosCommand
{
    public string PlayerId { get; init; } = string.Empty;
}

public sealed record TransferCommand : GenosCommand
{
    public string FromPlayerId { get; init; } = string.Empty;
    public string ToPlayerId { get; init; } = string.Empty;
}