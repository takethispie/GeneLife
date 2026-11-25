using System.Text.Json.Serialization;

namespace Genelife.Api.Commands;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MoveIntent), "move")]
[JsonDerivedType(typeof(UseBuildingIntent), "useBuilding")]
[JsonDerivedType(typeof(PlayerActionIntent), "playerAction")]
public abstract record Intent
{
    [JsonPropertyOrder(-1)]
    public string PlayerId { get; init; } = string.Empty;
    public long Sequence { get; init; }
}

public sealed record MoveIntent : Intent
{
    public ulong GenosNetworkId { get; init; }
    public int? TargetTileX { get; init; }
    public int? TargetTileY { get; init; }

    // Legacy float payload fields. New clients should send TargetTileX/Y.
    public float X { get; init; }
    public float Y { get; init; }
}

public sealed record UseBuildingIntent : Intent
{
    public ulong GenosNetworkId { get; init; }
    public ulong BuildingNetworkId { get; init; }
}