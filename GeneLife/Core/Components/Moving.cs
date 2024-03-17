using System.Numerics;

namespace GeneLife.Core.Components;

public struct Moving
{
    /// <summary>
    /// velocity in m/s (will be rounded to the meter) 
    /// </summary>
    public float Velocity;
    public Vector3 Target;

    public Moving()
    {
        Velocity = 1f;
        Target = Vector3.Zero;
    }
}