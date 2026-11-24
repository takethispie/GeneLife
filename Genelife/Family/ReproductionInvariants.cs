using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Life;

namespace Genelife.Family;

public static class ReproductionInvariants
{
    public sealed record Issue(string Name, string Level, string Detail);

    public static IReadOnlyList<Issue> Verify(
        World world,
        RelationshipIndex relationships,
        EconomyContext economy)
    {
        var issues = new List<Issue>();
        var activeByParent = new Dictionary<Entity, int>();

        world.Query(in new QueryDescription().WithAll<Pregnancy>(),
            (Entity entity, ref Pregnancy pregnancy) =>
            {
                if (pregnancy.Status is not (PregnancyStatus.Active or PregnancyStatus.ReadyToBirth))
                    return;

                activeByParent.TryGetValue(pregnancy.CarryingParent, out var count);
                activeByParent[pregnancy.CarryingParent] = count + 1;

                if (!world.IsAlive(pregnancy.CarryingParent) ||
                    !world.IsAlive(pregnancy.OtherParent) ||
                    pregnancy.CarryingParent == pregnancy.OtherParent)
                    issues.Add(new Issue("pregnancy.parents", "Fail",
                        $"Pregnancy {entity.Id} has invalid parents"));
                if (pregnancy.DueTick <= pregnancy.ConceivedTick)
                    issues.Add(new Issue("pregnancy.ticks", "Fail",
                        $"Pregnancy {entity.Id} has DueTick <= ConceivedTick"));
                if (pregnancy.ChildrenExpected < 1)
                    issues.Add(new Issue("pregnancy.children", "Fail",
                        $"Pregnancy {entity.Id} expects fewer than one child"));
            });

        foreach (var (parent, count) in activeByParent)
            if (count > 1)
                issues.Add(new Issue("pregnancy.duplicate-active", "Fail",
                    $"Sim {parent.Id} has {count} active pregnancies"));

        var policy = economy.Life.Policy;
        var reportedOvercrowdedHouseholds = new HashSet<Entity>();
        world.Query(in new QueryDescription().WithAll<Alive>().WithNone<Dead>(),
            (Entity entity, ref Alive alive) =>
            {
                var child = alive.Age < policy.WorkStartAge;
                if (child && world.Has<Employee>(entity))
                    issues.Add(new Issue("child.employed", "Fail",
                        $"Sim {entity.Id} is a child but is employed"));
                if (child && world.Has<RetirementIncome>(entity))
                    issues.Add(new Issue("child.pension", "Fail",
                        $"Sim {entity.Id} is a child but receives a pension"));

                if (world.Has<BirthRecord>(entity))
                {
                    if (!world.Has<Lifecycle>(entity) || !world.Has<Traits>(entity) ||
                        !world.Has<GenName>(entity))
                        issues.Add(new Issue("newborn.components", "Fail",
                            $"Newborn {entity.Id} is missing a required component"));
                    if (child &&
                        relationships.ParentsOf(entity).Count == 0 &&
                        !world.Has<StateCare>(entity) &&
                        relationships.GuardiansOf(entity).Count == 0)
                        issues.Add(new Issue("newborn.parent-edges", "Fail",
                            $"Newborn {entity.Id} has no parent edges and no guardian or state-care fallback"));
                }

                if (!child)
                    return;

                if (alive.Age < policy.SchoolStartAge && world.Has<Enrollment>(entity))
                    issues.Add(new Issue("child.early-enrollment", "Warn",
                        $"Sim {entity.Id} is enrolled before school start age"));

                if (alive.Age >= policy.SchoolStartAge && !world.Has<Enrollment>(entity))
                    issues.Add(new Issue("child.not-enrolled", "Warn",
                        $"Sim {entity.Id} is school age but has no enrollment record"));

                var household = economy.Housing.HouseholdOf(entity);
                if (household == Entity.Null)
                {
                    issues.Add(new Issue("child.no-household", "Warn",
                        $"Sim {entity.Id} is a child without a household"));
                    return;
                }

                var unit = economy.Housing.UnitOf(household);
                if (unit != Entity.Null && world.Has<HousingUnit>(unit) &&
                    world.Get<HousingUnit>(unit).IsOvercrowded &&
                    reportedOvercrowdedHouseholds.Add(household))
                    issues.Add(new Issue("housing.overcrowded", "Warn",
                        $"Household {household.Id} is overcrowded"));

                var guardian = relationships.GuardiansOf(entity).FirstOrNull();
                if (guardian != Entity.Null)
                {
                    var guardianHousehold = economy.Housing.HouseholdOf(guardian);
                    if (guardianHousehold != Entity.Null && guardianHousehold != household)
                        issues.Add(new Issue("guardian.residence", "Warn",
                            $"Sim {entity.Id} does not live with guardian {guardian.Id}"));
                }
            });

        return issues;
    }
}
