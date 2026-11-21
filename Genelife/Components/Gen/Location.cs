using System.Numerics;

namespace Genelife.Components.Gen;

/// <summary>
/// Component representing a physical location in 2D world space.
/// </summary>
public record struct Location
{
    public Vector2 Position;

    public Location(Vector2 position)
    {
        Position = position;
    }

    public Location(float x, float y)
    {
        Position = new Vector2(x, y);
    }
}