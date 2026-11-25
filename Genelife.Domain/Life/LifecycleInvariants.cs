using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Life;

/// <summary>
/// Fail-fast checks shared by restore, admin reads and scenario tests. These
/// catch impossible states such as a student holding an employee role.
/// </summary>
public static class LifecycleInvariants
{
    public static List<string> Verify(World world, LifeStagePolicy? policy = null)
    {
        policy ??= LifeStagePolicy.Default;
        var issues = new List<string>();

        world.Query(in new QueryDescription().WithAll<Alive>().WithNone<Dead>(),
            (Entity entity, ref Alive alive) =>
            {
                if (!world.Has<Lifecycle>(entity))
                {
                    issues.Add($"Sim {NetworkIdOf(world, entity)} is living without a Lifecycle.");
                    return;
                }

                var lifecycle = world.Get<Lifecycle>(entity);
                if (lifecycle.Stage != policy.StageFor(alive.Age))
                    issues.Add($"Sim {NetworkIdOf(world, entity)} is {lifecycle.Stage} at age {alive.Age}.");

                if (lifecycle.Stage != LifeStage.Education && world.Has<Enrollment>(entity))
                    issues.Add($"Sim {NetworkIdOf(world, entity)} has an Enrollment outside Education.");

                if (lifecycle.Stage != LifeStage.Work)
                {
                    if (world.Has<Employee>(entity))
                        issues.Add($"Sim {NetworkIdOf(world, entity)} is employed outside Work.");
                    if (world.Has<JobSeeker>(entity))
                        issues.Add($"Sim {NetworkIdOf(world, entity)} is job seeking outside Work.");
                    if (world.Has<WorkSchedule>(entity))
                        issues.Add($"Sim {NetworkIdOf(world, entity)} has a schedule outside Work.");
                }

                if (lifecycle.Stage == LifeStage.Education && world.Has<Employee>(entity))
                    issues.Add($"Student {NetworkIdOf(world, entity)} is also an employee.");
            });

        return issues;
    }

    private static ulong NetworkIdOf(World world, Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : 0UL;
}
