using Arch.Core;
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

    public void Add(string name, int age)
    {
        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new Location(0f, 0f)
        );

        world.Add(entity, networkIds.Allocate(entity));
    }

    public void Add(string name, int age, string playerId)
    {
        var entity = world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age),
            new PlayerControlled(playerId),
            new Location(0f, 0f)
        );

        world.Add(entity, networkIds.Allocate(entity));
    }

    public List<SimStatus> GetAll()
    {
        var sims = new List<SimStatus>();
        var query = new QueryDescription().WithAll<GenName, Needs, CurrentAction, NetworkId>();

        world.Query(in query, (ref GenName name, ref Needs needs, ref CurrentAction action, ref NetworkId networkId) =>
        {
            sims.Add(new SimStatus
            {
                NetworkId = networkId.Value,
                Name = name.Name,
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
