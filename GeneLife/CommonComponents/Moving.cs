using System.Numerics;

namespace GeneLife.CommonComponents;

public struct Moving
{
    /// <summary>
    /// velocity in meters
    /// </summary>
    public Vector3 Velocity;

    public Moving()
    {
        Velocity = Vector3.Zero;
    }
}