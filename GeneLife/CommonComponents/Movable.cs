using System.Numerics;

namespace GeneLife.CommonComponents;

public struct Movable
{
    /// <summary>
    /// velocity in meters
    /// </summary>
    public Vector3 Velocity;

    public Movable()
    {
        Velocity = Vector3.Zero;
    }
}