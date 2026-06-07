using System.Numerics;

namespace Domain.ValueObjects;

public record Address {
    public string Name { get; set; } = string.Empty;
    public Vector3 Position { get; set; } = Vector3.Zero;
}