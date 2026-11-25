using Arch.Core;
using Genelife.Components.Gen;

namespace Genelife.Family;

public static class TraitInvariants
{
    public sealed record Issue(string Name, string Level, string Detail);

    public static IReadOnlyList<Issue> Verify(World world)
    {
        var issues = new List<Issue>();
        var living = 0;
        var withTraits = 0;
        var invalid = 0;

        var query = new QueryDescription().WithAll<Alive>().WithNone<Dead>();
        world.Query(in query, (Entity entity, ref Alive _) =>
        {
            living++;
            if (!world.Has<Traits>(entity))
                return;

            withTraits++;
            if (!TraitValidation.IsValid(world.Get<Traits>(entity)))
                invalid++;
        });

        if (living == 0)
            return issues;

        if (withTraits < living)
        {
            issues.Add(new Issue(
                "traits.missing",
                "Fail",
                $"{living - withTraits} living genoses have no Traits component"));
        }

        if (invalid > 0)
        {
            issues.Add(new Issue(
                "traits.invalid",
                "Fail",
                $"{invalid} living genoses have undefined trait values"));
        }

        if (withTraits == 0)
        {
            issues.Add(new Issue(
                "traits.none",
                "Warn",
                "No living genos has a generated trait set"));
            return issues;
        }

        var distribution = TraitDistribution.Calculate(world);
        foreach (var (trait, counts) in distribution.Counts)
        {
            var distinct = counts.Count(pair => pair.Value > 0);
            if (distinct <= 1 && withTraits > 1)
            {
                issues.Add(new Issue(
                    $"traits.distribution.{trait}",
                    "Warn",
                    $"Trait '{trait}' has collapsed to a single value across {withTraits} genoses"));
            }
        }

        return issues;
    }
}
