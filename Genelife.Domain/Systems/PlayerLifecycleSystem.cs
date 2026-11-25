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
    private readonly Func<string, Entity?>? newGenosFactory;
    private readonly int interval;

    public PlayerLifecycleSystem(
        World world,
        EconomyContext economy,
        PlayerRegistry players,
        RelationshipIndex? relationships = null,
        InheritanceOptions? inheritanceOptions = null,
        Func<string, Entity?>? newGenosFactory = null,
        int interval = 100)
    {
        this.world = world;
        this.economy = economy;
        this.players = players;
        this.relationships = relationships;
        this.inheritanceOptions = inheritanceOptions ?? InheritanceOptions.Default;
        this.newGenosFactory = newGenosFactory;
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
            var genos = profile.ControlledGenos;
            var genosAlive = genos != Entity.Null && world.IsAlive(genos);

            if (genosAlive && world.Has<Dead>(genos))
            {
                if (world.Has<PlayerControlled>(genos))
                    world.Remove<PlayerControlled>(genos);

                CaptureDeath(profile, genos, tick);
                profile.LastGenosNetworkId = NetworkIdOf(genos);
                players.DetachGenos(profile);
                players.RecordDeath();
                economy.Emit(new PlayerAwaitingContinuation(
                    profile.LastGenosNetworkId,
                    profile.PlayerId,
                    tick,
                    profile.LastGenosName,
                    profile.LastGenosAge,
                    world.Has<Dead>(genos) ? world.Get<Dead>(genos).Cause.ToString() : null,
                    CanRespawn: profile.ContinuationPolicy != PlayerContinuationPolicy.Observer));
                continue;
            }

            if (!profile.AwaitingContinuation)
                continue;

            if (profile.Observer || profile.ContinuationPolicy == PlayerContinuationPolicy.Observer)
            {
                profile.Observer = true;
                continue;
            }

            // Respawn keeps the profile awaiting a player decision. The client
            // shows a prompt and calls the respawn endpoint to create a child.
            if (profile.ContinuationPolicy == PlayerContinuationPolicy.Respawn)
                continue;

            var previousGenosId = profile.LastGenosNetworkId;

            if (profile.ContinuationPolicy == PlayerContinuationPolicy.Heir)
            {
                var heir = ResolveHeir(profile, genos);
                if (heir != Entity.Null)
                {
                    Attach(profile, heir, previousGenosId, "heir", tick);
                    continue;
                }
            }

            var created = newGenosFactory?.Invoke(profile.PlayerId);
            if (created is { } newGenos && newGenos != Entity.Null && world.IsAlive(newGenos))
            {
                Attach(profile, newGenos, previousGenosId, "new-genos", tick);
                continue;
            }

            // No heir and the world could not create a new genos: keep the
            // profile awaiting rather than handing the player a dead genos.
        }
    }

    private void CaptureDeath(PlayerProfile profile, Entity genos, long tick)
    {
        if (profile.LastGenosDeathTick != 0)
            return;

        profile.LastGenosName = world.Has<GenName>(genos) ? world.Get<GenName>(genos).Name : null;
        profile.LastGenosAge = world.Has<Alive>(genos) ? world.Get<Alive>(genos).Age : 0;
        profile.LastGenosTraits = world.Has<Traits>(genos) ? world.Get<Traits>(genos) : null;
        var household = LifeSystemHelpers.HouseholdOf(world, economy, genos);
        profile.LastGenosHouseholdNetworkId = household != Entity.Null ? NetworkIdOf(household) : 0;
        profile.LastGenosDeathTick = tick;
    }

    private Entity ResolveHeir(PlayerProfile profile, Entity previousGenos)
    {
        if (relationships is null || previousGenos == Entity.Null || !world.IsAlive(previousGenos))
            return Entity.Null;

        var plan = HeirResolver.Resolve(
            world,
            relationships,
            previousGenos,
            inheritanceOptions,
            economy.Life.Policy);

        foreach (var allocation in plan.Allocations)
        {
            var heir = allocation.Heir;
            if (heir == Entity.Null || heir == previousGenos || !world.IsAlive(heir))
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

    private void Attach(PlayerProfile profile, Entity genos, ulong previousGenosId, string policy, long tick)
    {
        if (world.Has<PlayerControlled>(genos))
            world.Get<PlayerControlled>(genos) = new PlayerControlled(profile.PlayerId);
        else
            world.Add(genos, new PlayerControlled(profile.PlayerId));

        profile.Observer = false;
        players.AttachGenos(profile, genos);
        profile.LastGenosNetworkId = NetworkIdOf(genos);
        players.RecordContinuation();

        economy.Emit(new PlayerContinuationApplied(
            profile.PlayerId,
            previousGenosId,
            NetworkIdOf(genos),
            policy,
            null,
            tick,
            world.Has<GenName>(genos) ? world.Get<GenName>(genos).Name : null));
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.IsAlive(entity) && world.Has<NetworkId>(entity)
            ? world.Get<NetworkId>(entity).Value
            : 0UL;
}
