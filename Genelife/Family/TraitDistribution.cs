using Arch.Core;
using Genelife.Components.Gen;

namespace Genelife.Family;

public sealed class TraitDistributionSnapshot
{
    public int Population { get; init; }
    public int WithTraits { get; init; }
    public int MissingTraits { get; init; }
    public int MutationEvents { get; init; }
    public decimal FemaleShare { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Counts { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, int>>();
}

public static class TraitDistribution
{
    public static TraitDistributionSnapshot Calculate(World world)
    {
        var population = 0;
        var withTraits = 0;
        var females = 0;

        var eyeColor = NewCounts<EyeColor>();
        var hairColor = NewCounts<HairColor>();
        var handedness = NewCounts<Handedness>();
        var morphotype = NewCounts<Morphotype>();
        var aptitude = NewCounts<Aptitude>();
        var height = NewCounts<HeightPotential>();
        var sex = NewCounts<Sex>();
        var temperament = NewCounts<Temperament>();

        var query = new QueryDescription().WithAll<Alive>().WithNone<Dead>();
        world.Query(in query, (Entity entity, ref Alive _) =>
        {
            population++;
            if (!world.Has<Traits>(entity))
                return;

            var traits = world.Get<Traits>(entity);
            withTraits++;
            if (traits.Sex == Sex.Female)
                females++;

            Increment(eyeColor, traits.EyeColor.ToString());
            Increment(hairColor, traits.HairColor.ToString());
            Increment(handedness, traits.Handedness.ToString());
            Increment(morphotype, traits.Morphotype.ToString());
            Increment(aptitude, traits.Aptitude.ToString());
            Increment(height, traits.HeightPotential.ToString());
            Increment(sex, traits.Sex.ToString());
            Increment(temperament, traits.Temperament.ToString());
        });

        return new TraitDistributionSnapshot
        {
            Population = population,
            WithTraits = withTraits,
            MissingTraits = population - withTraits,
            FemaleShare = withTraits == 0 ? 0m : females / (decimal)withTraits,
            Counts = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
            {
                [nameof(Traits.EyeColor)] = eyeColor,
                [nameof(Traits.HairColor)] = hairColor,
                [nameof(Traits.Handedness)] = handedness,
                [nameof(Traits.Morphotype)] = morphotype,
                [nameof(Traits.Aptitude)] = aptitude,
                [nameof(Traits.HeightPotential)] = height,
                [nameof(Traits.Sex)] = sex,
                [nameof(Traits.Temperament)] = temperament
            }
        };
    }

    private static void Increment(SortedDictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out var current);
        counts[key] = current + 1;
    }

    private static SortedDictionary<string, int> NewCounts<T>() where T : struct, Enum
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in Enum.GetNames<T>())
            counts[value] = 0;
        return counts;
    }
}
