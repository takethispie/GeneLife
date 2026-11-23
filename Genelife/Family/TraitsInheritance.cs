using Genelife.Components.Gen;

namespace Genelife.Family;

public readonly record struct TraitInheritanceResult(Traits Traits, int Mutations);

/// <summary>
/// V4a inheritance: each trait independently copies one parent's value and then
/// mutates with a bounded probability. Alleles and dominance are deliberately
/// deferred; a future Genome layer can project into the same <see cref="Traits"/>
/// read surface.
/// </summary>
public static class TraitsInheritance
{
    public static Traits Create(
        Traits parentA,
        Traits parentB,
        int worldSeed,
        ulong childNetworkId,
        GeneticsOptions? options = null)
    {
        return CreateDetailed(parentA, 0, parentB, 0, worldSeed, childNetworkId, options).Traits;
    }

    /// <summary>
    /// Canonicalizing overload. Parents are ordered by network id so the result
    /// does not depend on caller enumeration or query order.
    /// </summary>
    public static Traits Create(
        Traits parentA,
        ulong parentANetworkId,
        Traits parentB,
        ulong parentBNetworkId,
        int worldSeed,
        ulong childNetworkId,
        GeneticsOptions? options = null)
    {
        return CreateDetailed(
            parentA, parentANetworkId, parentB, parentBNetworkId,
            worldSeed, childNetworkId, options).Traits;
    }

    public static TraitInheritanceResult CreateDetailed(
        Traits parentA,
        Traits parentB,
        int worldSeed,
        ulong childNetworkId,
        GeneticsOptions? options = null)
    {
        return CreateDetailed(parentA, 0, parentB, 0, worldSeed, childNetworkId, options);
    }

    public static TraitInheritanceResult CreateDetailed(
        Traits parentA,
        ulong parentANetworkId,
        Traits parentB,
        ulong parentBNetworkId,
        int worldSeed,
        ulong childNetworkId,
        GeneticsOptions? options = null)
    {
        options ??= GeneticsOptions.Default;
        options.Validate();

        if (parentANetworkId > parentBNetworkId)
        {
            (parentA, parentB) = (parentB, parentA);
            (parentANetworkId, parentBNetworkId) = (parentBNetworkId, parentANetworkId);
        }

        return CreateWithOptionalParents(
            parentA, parentANetworkId, parentB, parentBNetworkId,
            worldSeed, childNetworkId, options);
    }

    public static TraitInheritanceResult CreateWithOptionalParents(
        Traits? parentA,
        ulong? parentANetworkId,
        Traits? parentB,
        ulong? parentBNetworkId,
        int worldSeed,
        ulong childNetworkId,
        GeneticsOptions? options = null)
    {
        options ??= GeneticsOptions.Default;
        options.Validate();

        if (parentA is null && parentB is null)
        {
            return new TraitInheritanceResult(
                TraitsGenerator.Create(worldSeed, childNetworkId, TraitSource.Migration, options),
                0);
        }

        if (parentA is null)
            (parentA, parentANetworkId, parentB, parentBNetworkId) =
                (parentB, parentBNetworkId, null, null);
        else if (parentB is not null && parentANetworkId > parentBNetworkId)
            (parentA, parentB) = (parentB, parentA);

        var mutations = 0;
        var traits = new Traits
        {
            EyeColor = Inherit(parentA!.Value.EyeColor, parentB?.EyeColor, nameof(Traits.EyeColor), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            HairColor = Inherit(parentA.Value.HairColor, parentB?.HairColor, nameof(Traits.HairColor), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            Handedness = Inherit(parentA.Value.Handedness, parentB?.Handedness, nameof(Traits.Handedness), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            Morphotype = Inherit(parentA.Value.Morphotype, parentB?.Morphotype, nameof(Traits.Morphotype), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            Aptitude = Inherit(parentA.Value.Aptitude, parentB?.Aptitude, nameof(Traits.Aptitude), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            HeightPotential = Inherit(parentA.Value.HeightPotential, parentB?.HeightPotential, nameof(Traits.HeightPotential), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            Sex = Inherit(parentA.Value.Sex, parentB?.Sex, nameof(Traits.Sex), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            Temperament = Inherit(parentA.Value.Temperament, parentB?.Temperament, nameof(Traits.Temperament), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            SkinTone = Inherit(parentA.Value.SkinTone, parentB?.SkinTone, nameof(Traits.SkinTone), worldSeed, childNetworkId, options.MutationRate, ref mutations),
            FaceShape = Inherit(parentA.Value.FaceShape, parentB?.FaceShape, nameof(Traits.FaceShape), worldSeed, childNetworkId, options.MutationRate, ref mutations)
        };

        return new TraitInheritanceResult(traits, mutations);
    }

    private static T Inherit<T>(
        T parentA,
        T? parentB,
        string traitKey,
        int worldSeed,
        ulong childNetworkId,
        decimal mutationRate,
        ref int mutations)
        where T : struct, Enum
    {
        var choice = new Random(TraitsGenerator.DeriveSeed(
            worldSeed, TraitSourceStreams.For(TraitSource.Inheritance), childNetworkId, $"coin.{traitKey}"));

        var value = parentB is { } second && choice.Next(2) == 1 ? second : parentA;

        if (mutationRate <= 0m)
            return value;

        var mutation = new Random(TraitsGenerator.DeriveSeed(
            worldSeed, TraitSourceStreams.For(TraitSource.Inheritance), childNetworkId, $"mutation.{traitKey}"));

        if (mutation.NextDouble() >= (double)mutationRate)
            return value;

        var values = EnumValues<T>.Items;
        if (values.Length <= 1)
            return value;

        var current = Convert.ToInt32(value);
        var next = (current + 1 + mutation.Next(values.Length - 1)) % values.Length;
        mutations++;
        return values[next];
    }
}
