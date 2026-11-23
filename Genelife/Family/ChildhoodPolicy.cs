using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Life;

namespace Genelife.Family;

public static class ChildhoodPolicy
{
    public static bool IsChild(World world, Entity sim, LifeStagePolicy? policy = null)
    {
        policy ??= LifeStagePolicy.Default;
        return world.Has<Alive>(sim) && world.Get<Alive>(sim).Age < policy.WorkStartAge;
    }

    public static bool IsSchoolAge(World world, Entity sim, LifeStagePolicy? policy = null)
    {
        policy ??= LifeStagePolicy.Default;
        if (!world.Has<Alive>(sim))
            return false;
        var age = world.Get<Alive>(sim).Age;
        return age >= policy.SchoolStartAge && age < policy.WorkStartAge;
    }

    public static bool IsDependentChild(World world, Entity sim, LifeStagePolicy? policy = null)
    {
        policy ??= LifeStagePolicy.Default;
        if (!world.Has<Alive>(sim))
            return false;

        var age = world.Get<Alive>(sim).Age;
        if (age >= policy.WorkStartAge)
            return false;

        return age < policy.SchoolStartAge ||
               world.Has<DependentChild>(sim) ||
               (world.Has<HouseholdMember>(sim) &&
                world.Get<HouseholdMember>(sim).Role == HouseholdRole.Child);
    }

    public static Entity GuardianOf(
        World world,
        Genelife.Economy.EconomyContext economy,
        Entity sim,
        RelationshipIndex? relationships = null)
    {
        if (relationships is not null)
        {
            foreach (var guardian in relationships.GuardiansOf(sim))
            {
                if (world.IsAlive(guardian))
                    return guardian;
            }
        }

        var household = economy.Housing.HouseholdOf(sim);
        return household == Entity.Null
            ? Entity.Null
            : new Genelife.Housing.HouseholdFinanceService(world, economy).Head(household);
    }

    public static void ClearDependentMarker(World world, Entity sim)
    {
        if (world.Has<DependentChild>(sim))
            world.Remove<DependentChild>(sim);
    }
}
