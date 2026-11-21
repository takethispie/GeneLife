using System.Text.Json.Serialization;

namespace Genelife.Api.Commands;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MoveIntent), "move")]
public abstract record Intent
{
    [JsonPropertyOrder(-1)]
    public string PlayerId { get; init; } = string.Empty;
}

public sealed record MoveIntent : Intent
{
    public float X { get; init; }
    public float Y { get; init; }
}