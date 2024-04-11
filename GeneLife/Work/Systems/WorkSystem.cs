using Arch.Core;
using Arch.System;
using System.Numerics;

namespace GeneLife.Work.Systems;

internal class WorkSystem : BaseSystem<World, float>
{
    public Dictionary<string, Vector3> WorkPlaces = [];

    public WorkSystem(World world) : base(world)
    {
    }

    public override void Update(in float t)
    {
    }
}