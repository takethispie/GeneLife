using System.Numerics;

namespace Genelife.Components.Gen;

/// <summary>
/// Component representing a physical location
/// </summary>
public record struct Location
{
    public Vector3 Coordinates;
    
    public Location(Vector3 coords)
    {
        Coordinates = coords;
    }
}
