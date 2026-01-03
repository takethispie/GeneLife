using System.Numerics;

namespace Genelife.Global.Extensions;

public static class Vector3Extensions
{
    public static string ToFormattedPositionString(this Vector3 vector3) => vector3.X + ":" + vector3.Y +  ":" + vector3.Z;
}