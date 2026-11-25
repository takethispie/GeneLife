using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Life;

namespace Genelife.Family;

public static class ChildhoodPolicy
{
    public static bool IsChild(World world, Entity genos, LifeStagePolicy? policy = null)
    {
        policy ??= LifeStagePolicy.Default;
        return world.Has<Alive>(genos) && world.Get<Alive>(genos).Age < policy.WorkStartAge;
    }

    public static bool IsSchoolAge(World world, Entity genos, LifeStagePolicy? policy = null)
    {
        policy ??= LifeStagePolicy.Default;
        if (!world.Has<Alive>(genos))
            return false;
        var age = world.Get<Alive>(genos).Age;
        return age >= policy.SchoolStartAge && age < policy.WorkStartAge;
    }

    public static bool IsDependentChild(World world, Entity genos, LifeStagePolicy? policy = null)
    {
        policy ??= LifeStagePolicy.Default;
        if (!world.Has<Alive>(genos))
            return false;

        var age = world.Get<Alive>(genos).Age;
        if (age >= policy.WorkStartAge)
            return false;

        return age < policy.SchoolStartAge ||
               world.Has<DependentChild>(genos) ||
               (world.Has<HouseholdMember>(genos) &&
                world.Get<HouseholdMember>(genos).Role == HouseholdRole.Child);
    }

    public static Entity GuardianOf(
        World world,
        Genelife.Economy.EconomyContext economy,
        Entity genos,
        RelationshipIndex? relationships = null)
    {
        if (relationships is not null)
        {
            foreach (var guardian in relationships.GuardiansOf(genos))
            {
                if (world.IsAlive(guardian))
                    return guardian;
            }
        }

        var household = economy.Housing.HouseholdOf(genos);
        return household == Entity.Null
            ? Entity.Null
            : new Genelife.Housing.HouseholdFinanceService(world, economy).Head(household);
    }

    public static void ClearDependentMarker(World world, Entity genos)
    {
        if (world.Has<DependentChild>(genos))
            world.Remove<DependentChild>(genos);
    }
}
