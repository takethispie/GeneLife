using System.Numerics;

namespace GeneLife.Core.Components;

public struct Moving
{
    /// <summary>
    /// velocity in m/s (will be rounded to the meter) 
    /// </summary>
    public Vector3 Velocity;

    public Moving()
    {
        Velocity = Vector3.Zero;
    }
}