using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Family;

public readonly record struct EligibilityResult(bool Eligible, string? Code, string? Reason)
{
    public static EligibilityResult Allowed { get; } = new(true, null, null);

    public static EligibilityResult Rejected(string code, string reason) =>
        new(false, code, reason);
}

/// <summary>
/// Ordered eligibility checks. Every rejection returns a stable code so the
/// admin can explain a pair without inspecting internals.
/// </summary>
public static class EligibilityPolicy
{
    public static EligibilityResult Evaluate(
        World world,
        RelationshipIndex index,
        Entity first,
        Entity second,
        RelationshipOptions? options = null,
        Func<Entity, Entity, bool>? inCooldown = null)
    {
        options ??= RelationshipOptions.Default;

        if (first == second)
            return EligibilityResult.Rejected("self", "A sim cannot pair with itself.");
        if (!IsAlive(world, first) || !IsAlive(world, second))
            return EligibilityResult.Rejected("not-alive", "Both sims must be alive.");
        if (IsDependent(world, first) || IsDependent(world, second))
            return EligibilityResult.Rejected("dependent", "Dependants are not eligible for pairing.");

        var ageA = world.Get<Alive>(first).Age;
        var ageB = world.Get<Alive>(second).Age;

        if (ageA < options.MinimumPairingAge || ageB < options.MinimumPairingAge)
            return EligibilityResult.Rejected("minor", $"Both sims must be at least {options.MinimumPairingAge}.");
        if (ageA > options.MaximumPairingAge || ageB > options.MaximumPairingAge)
            return EligibilityResult.Rejected("too-old", $"Both sims must be at most {options.MaximumPairingAge}.");
        if (Math.Abs(ageA - ageB) > options.MaxAgeGap)
            return EligibilityResult.Rejected("age-gap", $"Age gap exceeds {options.MaxAgeGap} years.");

        if (index.HasPartner(first) || index.HasPartner(second))
            return EligibilityResult.Rejected("has-partner", "Both sims must be single.");

        if (IsCloseRelative(index, first, second, options))
            return EligibilityResult.Rejected("close-relative", "Close relatives may not pair.");

        if (!world.Has<Traits>(first) || !world.Has<Traits>(second))
            return EligibilityResult.Rejected("missing-traits", "Both sims need a trait set.");

        if (!options.AllowSameSexPairing &&
            world.Get<Traits>(first).Sex == world.Get<Traits>(second).Sex)
            return EligibilityResult.Rejected("same-sex-not-allowed", "Only opposite-sex pairs are eligible.");

        if (inCooldown is not null && inCooldown(first, second))
            return EligibilityResult.Rejected("cooldown", "The pair is in a pairing cooldown.");

        return EligibilityResult.Allowed;
    }

    public static bool IsAlive(World world, Entity sim) =>
        world.IsAlive(sim) && world.Has<Alive>(sim) && !world.Has<Dead>(sim);

    public static bool IsDependent(World world, Entity sim) =>
        world.Has<HouseholdMember>(sim) &&
        world.Get<HouseholdMember>(sim).Role == HouseholdRole.Child;

    public static bool IsCloseRelative(
        RelationshipIndex index,
        Entity first,
        Entity second,
        RelationshipOptions? options = null)
    {
        options ??= RelationshipOptions.Default;

        if (index.IsParentOf(first, second) || index.IsParentOf(second, first))
            return true;
        if (index.AreSiblings(first, second))
            return true;
        if (index.IsAncestorOf(first, second) || index.IsAncestorOf(second, first))
            return true;
        if (index.AreAuntUncle(first, second))
            return true;
        if (options.RejectFirstCousins && index.AreFirstCousins(first, second))
            return true;
        if (options.MaxConsanguinityDepth > 0 &&
            index.HasCommonAncestorWithin(first, second, options.MaxConsanguinityDepth))
            return true;

        return false;
    }
}
