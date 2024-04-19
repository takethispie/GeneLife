using System.Numerics;

namespace Genelife.Physical.Domain;
public static class VectorExtensions
{
    public static Vector3 MovePointTowards(this Vector3 a, Vector3 b, float distance)
    {
        if (Vector3.Distance(a, b) <= distance) return b;
        var vector = b - a;
        var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z); ;
        var unitVector = new Vector3(
            vector.X / Convert.ToSingle(length),
            vector.Y / Convert.ToSingle(length),
            vector.Z / Convert.ToSingle(length)
        );

        return a + unitVector * distance;
    }
}
