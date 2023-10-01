using Arch.Core;
using Arch.System;
using GeneLife.Core.Components;
using System.Drawing;
using System.Numerics;

namespace GeneLife.Athena.Systems;
internal class MoveSystem : BaseSystem<World, float>
{

    private readonly QueryDescription movingEntities = new QueryDescription().WithAll<Moving>();

    public MoveSystem(World world) : base(world)
    {

    }

    public Vector3 MovePointTowards(Vector3 a, Vector3 b, float distance)
    {
        var vector = b - a;
        var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z); ;
        var unitVector = new Vector3(
            vector.X / Convert.ToSingle(length), 
            vector.Y / Convert.ToSingle(length), 
            vector.Z / Convert.ToSingle(length)
        );
        return a + unitVector * distance;
    }

    public override void Update(in float t)
    {

    }
}
