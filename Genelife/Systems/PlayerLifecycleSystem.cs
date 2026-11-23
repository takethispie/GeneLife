using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Family;
using Genelife.Messages.Events;
using Genelife.Player;

namespace Genelife.Systems;

/// <summary>
/// Detects player death, removes control and applies the configured
/// continuation policy once the estate has had a chance to settle.
/// </summary>
public sealed class PlayerLifecycleSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly PlayerRegistry players;
    private readonly RelationshipIndex? relationships;
    private readonly InheritanceOptions inheritanceOptions;
    private readonly Func<string, Entity?>? newSimFactory;
    private readonly int interval;

    public PlayerLifecycleSystem(
        World world,
        EconomyContext economy,
        PlayerRegistry players,
        RelationshipIndex? relationships = null,
        InheritanceOptions? inheritanceOptions = null,
        Func<string, Entity?>? newSimFactory = null,
        int interval = 100)
    {
        this.world = world;
        this.economy = economy;
        this.players = players;
        this.relationships = relationships;
        this.inheritanceOptions = inheritanceOptions ?? InheritanceOptions.Default;
        this.newSimFactory = newSimFactory;
        this.interval = Math.Max(1, interval);
    }

    public string Name => "PlayerLifecycle";
    public int Order => 405;
    public Cadence Cadence => new(interval);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var profiles = players.All()
            .OrderBy(profile => profile.PlayerId, StringComparer.Ordinal)
            .ToList();

        foreach (var profile in profiles)
        {
            var sim = profile.ControlledSim;
            var simAlive = sim != Entity.Null && world.IsAlive(sim);

            if (simAlive && world.Has<Dead>(sim))
            {
                if (world.Has<PlayerControlled>(sim))
                    world.Remove<PlayerControlled>(sim);

                profile.LastSimNetworkId = NetworkIdOf(sim);
                profile.ControlledSim = Entity.Null;
                profile.AwaitingContinuation = true;
                players.RecordDeath();
                economy.Emit(new PlayerAwaitingContinuation(
                    profile.LastSimNetworkId,
                    profile.PlayerId,
                    tick));
                continue;
            }

            if (!profile.AwaitingContinuation)
                continue;

            if (profile.Observer || profile.ContinuationPolicy == PlayerContinuationPolicy.Observer)
            {
                profile.Observer = true;
                continue;
            }

            var previousSimId = profile.LastSimNetworkId;

            if (profile.ContinuationPolicy == PlayerContinuationPolicy.Heir)
            {
                var heir = ResolveHeir(profile, sim);
                if (heir != Entity.Null)
                {
                    Attach(profile, heir, previousSimId, "heir", tick);
                    continue;
                }
            }

            var created = newSimFactory?.Invoke(profile.PlayerId);
            if (created is { } newSim && newSim != Entity.Null && world.IsAlive(newSim))
            {
                Attach(profile, newSim, previousSimId, "new-sim", tick);
                continue;
            }

            // No heir and the world could not create a new sim: keep the
            // profile awaiting rather than handing the player a dead sim.
        }
    }

    private Entity ResolveHeir(PlayerProfile profile, Entity previousSim)
    {
        if (relationships is null || previousSim == Entity.Null || !world.IsAlive(previousSim))
            return Entity.Null;

        var plan = HeirResolver.Resolve(
            world,
            relationships,
            previousSim,
            inheritanceOptions,
            economy.Life.Policy);

        foreach (var allocation in plan.Allocations)
        {
            var heir = allocation.Heir;
            if (heir == Entity.Null || heir == previousSim || !world.IsAlive(heir))
                continue;
            if (world.Has<Dead>(heir))
                continue;
            if (players.IsControlledByAnotherPlayer(heir, profile.PlayerId))
                continue;

            var age = world.Has<Alive>(heir) ? world.Get<Alive>(heir).Age : 0;
            if (age < economy.Life.Policy.WorkStartAge)
                continue;

            return heir;
        }

        return Entity.Null;
    }

    private void Attach(PlayerProfile profile, Entity sim, ulong previousSimId, string policy, long tick)
    {
        if (world.Has<PlayerControlled>(sim))
            world.Get<PlayerControlled>(sim) = new PlayerControlled(profile.PlayerId);
        else
            world.Add(sim, new PlayerControlled(profile.PlayerId));

        profile.Observer = false;
        players.AttachSim(profile, sim);
        profile.LastSimNetworkId = NetworkIdOf(sim);
        players.RecordContinuation();

        economy.Emit(new PlayerContinuationApplied(
            profile.PlayerId,
            previousSimId,
            NetworkIdOf(sim),
            policy,
            null,
            tick));
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}
