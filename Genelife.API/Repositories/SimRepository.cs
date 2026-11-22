using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.DTOs;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.Api.Repositories;

public class SimRepository
{
    private readonly World world;
    private readonly NetworkIdRegistry networkIds;

    public SimRepository(World world, NetworkIdRegistry networkIds)
    {
        this.world = world;
        this.networkIds = networkIds;
    }

    public NetworkId Add(string name, int age)
    {
        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new Location(0f, 0f)
        );

        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);
        return networkId;
    }

    public NetworkId Add(string name, int age, string playerId)
    {
        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new PlayerControlled(playerId),
            new Location(0f, 0f)
        );

        var networkId = networkIds.Allocate(entity);
        world.Add(entity, networkId);
        return networkId;
    }

    public bool Delete(ulong networkId)
    {
        if (!networkIds.TryGetEntity(new NetworkId(networkId), out var entity))
            return false;

        if (!world.IsAlive(entity))
            return false;

        networkIds.Remove(new NetworkId(networkId));
        world.Destroy(entity);
        return true;
    }

    public bool SetAction(ulong networkId, string actionType)
    {
        if (!networkIds.TryGetEntity(new NetworkId(networkId), out var entity))
            throw new InvalidOperationException($"Sim with NetworkId {networkId} not found");

        if (!world.IsAlive(entity))
            throw new InvalidOperationException($"Sim with NetworkId {networkId} is no longer alive");

        if (!Enum.TryParse<ActionType>(actionType, ignoreCase: true, out var action))
            throw new InvalidOperationException($"Unknown action type: {actionType}");

        if (world.Has<CurrentAction>(entity))
        {
            ref var currentAction = ref world.Get<CurrentAction>(entity);
            currentAction = new CurrentAction(action, 0f);
        }
        else
        {
            world.Add(entity, new CurrentAction(action, 0f));
        }

        return true;
    }

    public List<SimStatus> GetAll()
    {
        var sims = new List<SimStatus>();
        var query = new QueryDescription().WithAll<GenName, Needs, CurrentAction, NetworkId>();

        world.Query(in query, (Entity entity, ref GenName name, ref Needs needs, ref CurrentAction action, ref NetworkId networkId) =>
        {
            var isPlayerControlled = world.Has<PlayerControlled>(entity);
            sims.Add(new SimStatus
            {
                NetworkId = networkId.Value,
                Name = name.Name,
                Age = world.TryGet<Alive>(entity, out var alive) ? alive.Age : 0,
                IsPlayerControlled = isPlayerControlled,
                Hunger = needs.Hunger,
                Energy = needs.Energy,
                Hygiene = needs.Hygiene,
                Bladder = needs.Bladder,
                CurrentAction = action.Type.ToString(),
                TimeRemaining = action.TimeRemaining
            });
        });

        return sims;
    }
}
