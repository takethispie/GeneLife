using System.Numerics;

namespace GeneLife.Core.Components;

public struct Position
{
    /// <summary>
    /// position in meters
    /// </summary>
    public Vector3 Coordinates;
    
    public Position()
    {
        Coordinates = Vector3.Zero;
    }

    public Position(Vector3 pos)
    {
        Coordinates = pos;
    }

}