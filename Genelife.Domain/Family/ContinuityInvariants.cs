using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Values;

namespace Genelife.Family;

public static class ContinuityInvariants
{
    public sealed record Issue(string Name, string Level, string Detail);

    public static IReadOnlyList<Issue> Verify(
        World world,
        RelationshipIndex relationships,
        EconomyContext economy)
    {
        var issues = new List<Issue>();
        var workAge = economy.Life.Policy.WorkStartAge;
        var stateCare = 0;
        var orphans = 0;
        var children = 0;

        world.Query(in new QueryDescription().WithAll<Alive>().WithNone<Dead>(),
            (Entity entity, ref Alive alive) =>
            {
                if (alive.Age >= workAge)
                    return;

                children++;
                var hasStateCare = world.Has<StateCare>(entity);
                if (hasStateCare)
                    stateCare++;

                var livingParent = relationships.ParentsOf(entity)
                    .Any(parent => world.IsAlive(parent) && !world.Has<Dead>(parent));
                var livingGuardian = relationships.GuardiansOf(entity)
                    .Any(guardian => world.IsAlive(guardian) && !world.Has<Dead>(guardian));

                if (!livingParent && !livingGuardian && !hasStateCare)
                {
                    orphans++;
                    issues.Add(new Issue("minor.no-guardian", "Fail",
                        $"Minor {entity.Id} has no living parent, guardian or state-care record"));
                }

                foreach (var guardian in relationships.GuardiansOf(entity))
                {
                    if (!world.IsAlive(guardian) || world.Has<Dead>(guardian))
                        issues.Add(new Issue("guardian.dead", "Fail",
                            $"Minor {entity.Id} has a dead guardian {guardian.Id}"));
                    else if (world.Has<Alive>(guardian) &&
                             world.Get<Alive>(guardian).Age < workAge)
                        issues.Add(new Issue("guardian.minor", "Fail",
                            $"Minor {entity.Id} has a minor guardian {guardian.Id}"));
                }
            });

        var trustTotal = Money.Zero;
        world.Query(in new QueryDescription().WithAll<TrustAccount>(),
            (Entity entity, ref TrustAccount trust) =>
            {
                trustTotal += trust.Balance;
                if (!world.IsAlive(trust.Beneficiary) || world.Has<Dead>(trust.Beneficiary))
                    issues.Add(new Issue("trust.beneficiary", "Fail",
                        $"Trust {entity.Id} has a dead or missing beneficiary"));
                if (trust.Balance.IsNegative)
                    issues.Add(new Issue("trust.negative", "Fail",
                        $"Trust {entity.Id} has a negative balance"));
                if (trust.Guardian != Entity.Null &&
                    (!world.IsAlive(trust.Guardian) || world.Has<Dead>(trust.Guardian)))
                    issues.Add(new Issue("trust.guardian", "Fail",
                        $"Trust {entity.Id} has a dead or missing guardian"));
            });

        var breakdown = economy.GetMoneySupplyBreakdown();
        if (breakdown.MinorTrusts != trustTotal)
            issues.Add(new Issue("trust.money-supply", "Fail",
                $"Trust total {trustTotal.Cents} differs from money-supply trust field {breakdown.MinorTrusts.Cents}"));

        world.Query(in new QueryDescription().WithAll<Relationship>(),
            (Entity entity, ref Relationship relationship) =>
            {
                if (relationship.Kind != RelationshipKind.Partner)
                    return;
                if (!world.IsAlive(relationship.From) || world.Has<Dead>(relationship.From) ||
                    !world.IsAlive(relationship.To) || world.Has<Dead>(relationship.To))
                    issues.Add(new Issue("death.active-partner", "Fail",
                        $"Relationship {entity.Id} keeps an active partner edge to a dead sim"));
            });

        if (children > 0 && orphans > 0)
            issues.Add(new Issue("orphan.count", "Fail",
                $"{orphans}/{children} minors have no guardian or state-care record"));

        return issues;
    }
}
