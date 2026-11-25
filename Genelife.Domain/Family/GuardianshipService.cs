using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Life;
using Genelife.Messages.Events;
using Genelife.Values;

namespace Genelife.Family;

/// <summary>
/// Selects and records a guardian for a minor. A living parent needs no guardian
/// edge; other guardians get a Guardian/Ward edge and the child moves with them.
/// If nobody is available, an explicit StateCare record is created.
/// </summary>
public sealed class GuardianshipService
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipIndex relationships;
    private readonly RelationshipService service;
    private readonly GuardianshipOptions options;

    public GuardianshipService(
        World world,
        EconomyContext economy,
        RelationshipIndex relationships,
        RelationshipService service,
        GuardianshipOptions? options = null)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.service = service;
        this.options = options ?? GuardianshipOptions.Default;
    }

    public bool EnsureGuardian(Entity minor, long tick, out Entity guardian, out bool stateCare)
    {
        guardian = Entity.Null;
        stateCare = false;
        var policy = economy.Life.Policy;
        if (!world.IsAlive(minor) || world.Has<Dead>(minor) ||
            !world.Has<Alive>(minor) || world.Get<Alive>(minor).Age >= policy.WorkStartAge)
            return false;

        guardian = CandidateFor(minor);
        if (guardian != Entity.Null && (!world.IsAlive(guardian) || world.Has<Dead>(guardian)))
            guardian = Entity.Null;

        if (guardian != Entity.Null)
        {
            if (!relationships.IsParentOf(guardian, minor) &&
                !relationships.TryGetEdge(guardian, minor, RelationshipKind.Guardian, out _))
            {
                service.TryCreatePair(
                    guardian, minor, RelationshipKind.Guardian,
                    RelationshipSource.Guardianship, tick, out _, out _, out _);
            }

            MoveToGuardian(minor, guardian);
            if (world.Has<StateCare>(minor))
            {
                world.Remove<StateCare>(minor);
                economy.Emit(new StateCareClosed(
                    NetworkIdOf(minor), NetworkIdOf(guardian), "guardian-assigned", tick));
            }
            economy.Emit(new GuardianshipAssigned(
                NetworkIdOf(minor), NetworkIdOf(guardian), "relative", tick));
            return true;
        }

        if (!world.Has<StateCare>(minor))
        {
            world.Add(minor, new StateCare
            {
                SinceTick = tick,
                CaseWorker = Entity.Null,
                MonthlyCost = options.StateCareMonthlyCost
            });
            economy.Emit(new StateCareOpened(
                NetworkIdOf(minor), options.StateCareMonthlyCost.Cents, tick));
            economy.Emit(new GuardianshipAssigned(
                NetworkIdOf(minor), 0, "state-care", tick));
        }
        stateCare = true;
        return true;
    }

    /// <summary>
    /// Every minor who depended on the deceased needs a fallback, not just
    /// explicit wards. Children linked by Parent/Child edges may live in another
    /// household, so relying on the household-minor pass alone would leave them
    /// without a guardian once the dead parent's edges are removed.
    /// </summary>
    public void ReassignAfterGuardianDeath(Entity guardian, long tick)
    {
        var workStartAge = economy.Life.Policy.WorkStartAge;
        var dependents = new HashSet<Entity>();

        foreach (var ward in relationships.WardsOf(guardian))
            dependents.Add(ward);

        foreach (var child in relationships.ChildrenOf(guardian))
        {
            if (!world.IsAlive(child) || world.Has<Dead>(child) ||
                !world.Has<Alive>(child) ||
                world.Get<Alive>(child).Age >= workStartAge)
                continue;
            dependents.Add(child);
        }

        foreach (var dependent in dependents)
            EnsureGuardian(dependent, tick, out _, out _);
    }

    /// <summary>
    /// Explicit admin assignment. Unlike <see cref="EnsureGuardian"/> this honors
    /// the requested guardian, but it still enforces the same safety rules,
    /// replaces any existing guardian edge and moves the child with the guardian.
    /// </summary>
    public bool AssignGuardian(Entity minor, Entity guardian, long tick, string reason, out string error)
    {
        error = string.Empty;
        var policy = economy.Life.Policy;
        if (!world.IsAlive(minor) || world.Has<Dead>(minor) || !world.Has<Alive>(minor) ||
            world.Get<Alive>(minor).Age >= policy.WorkStartAge)
        {
            error = "minor-not-eligible";
            return false;
        }

        if (guardian == Entity.Null || guardian == minor ||
            !world.IsAlive(guardian) || world.Has<Dead>(guardian) || !world.Has<Alive>(guardian))
        {
            error = "guardian-not-found";
            return false;
        }

        if (world.Get<Alive>(guardian).Age < policy.WorkStartAge)
        {
            error = "guardian-minor";
            return false;
        }

        if (PregnancyService.IsDependent(world, guardian))
        {
            error = "guardian-dependent";
            return false;
        }

        if (economy.Housing.HouseholdOf(guardian) == Entity.Null)
        {
            error = "guardian-no-household";
            return false;
        }

        foreach (var existing in relationships.GuardiansOf(minor).ToList())
        {
            if (existing == guardian)
                continue;
            if (relationships.TryGetEdge(existing, minor, RelationshipKind.Guardian, out var edge))
                service.RemoveEdge(edge);
        }

        if (!relationships.IsParentOf(guardian, minor) &&
            !relationships.TryGetEdge(guardian, minor, RelationshipKind.Guardian, out _) &&
            !service.TryCreatePair(
                guardian, minor, RelationshipKind.Guardian,
                RelationshipSource.Admin, tick, out _, out _, out var createError))
        {
            error = createError.ToString();
            return false;
        }

        MoveToGuardian(minor, guardian);
        if (world.Has<StateCare>(minor))
        {
            world.Remove<StateCare>(minor);
            economy.Emit(new StateCareClosed(
                NetworkIdOf(minor), NetworkIdOf(guardian), reason, tick));
        }
        economy.Emit(new GuardianshipAssigned(
            NetworkIdOf(minor), NetworkIdOf(guardian), "admin", tick));
        return true;
    }

    public void EnsureHouseholdMinors(Entity household, long tick)
    {
        if (household == Entity.Null || !world.IsAlive(household))
            return;

        foreach (var member in economy.Housing.GetMembers(household).ToList())
        {
            if (!world.IsAlive(member) || !world.Has<Alive>(member) ||
                world.Get<Alive>(member).Age >= economy.Life.Policy.WorkStartAge)
                continue;
            EnsureGuardian(member, tick, out _, out _);
        }
    }

    private Entity CandidateFor(Entity minor)
    {
        var policy = economy.Life.Policy;
        var oldHousehold = economy.Housing.HouseholdOf(minor);
        var candidates = new List<Entity>();

        candidates.AddRange(relationships.ParentsOf(minor));
        foreach (var parent in relationships.ParentsOf(minor))
        {
            candidates.AddRange(relationships.ParentsOf(parent));
            foreach (var grandparent in relationships.ParentsOf(parent))
                candidates.AddRange(relationships.ChildrenOf(grandparent));
            candidates.AddRange(relationships.ChildrenOf(parent));
            foreach (var sibling in relationships.ChildrenOf(parent))
                candidates.AddRange(relationships.ChildrenOf(sibling));
        }
        candidates.AddRange(relationships.AncestorsOf(minor, 2));

        var valid = candidates
            .Where(candidate => candidate != minor && candidate != Entity.Null)
            .Where(candidate => world.IsAlive(candidate) && !world.Has<Dead>(candidate))
            .Where(candidate => world.Has<Alive>(candidate) && world.Get<Alive>(candidate).Age >= policy.WorkStartAge)
            .Where(candidate => !PregnancyService.IsDependent(world, candidate))
            .Where(candidate => economy.Housing.HouseholdOf(candidate) != Entity.Null)
            .Distinct()
            .ToList();

        var preferred = valid
            .Where(candidate => oldHousehold != Entity.Null &&
                                economy.Housing.HouseholdOf(candidate) == oldHousehold)
            .ToList();
        var pool = preferred.Count > 0 ? preferred : valid;
        return pool
            .OrderBy(NetworkIdOf)
            .ThenBy(candidate => candidate.Id)
            .FirstOrNull();
    }

    private void MoveToGuardian(Entity minor, Entity guardian)
    {
        var oldHousehold = economy.Housing.HouseholdOf(minor);
        var newHousehold = economy.Housing.HouseholdOf(guardian);
        if (newHousehold == Entity.Null || newHousehold == oldHousehold)
            return;

        economy.Housing.RemoveMember(minor);
        if (world.Has<HouseholdMember>(minor))
            world.Remove<HouseholdMember>(minor);
        world.Add(minor, new HouseholdMember(newHousehold, HouseholdRole.Child));
        economy.Housing.AddMember(minor, newHousehold);

        if (oldHousehold != Entity.Null && world.IsAlive(oldHousehold) &&
            economy.Housing.MemberCount(oldHousehold) == 0)
        {
            var oldUnit = economy.Housing.UnitOf(oldHousehold);
            if (oldUnit != Entity.Null)
                economy.Housing.MoveOut(oldUnit, oldHousehold);
        }
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
