using Arch.Core;
using Genelife.Api.DTOs;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.Api.Repositories;

public class SimRepository
{
    private readonly World world;

    public SimRepository(World world)
    {
        this.world = world;
    }

    public void Add(string name, int age)
    {
        world.Create(
            new GenName(name),
            new Needs(),
            new CurrentAction(ActionType.Idle, 0f),
            new Alive(age)
        );
    }

    public List<SimStatus> GetAll()
    {
        var sims = new List<SimStatus>();
        var query = new QueryDescription().WithAll<GenName, Needs, CurrentAction>();

        world.Query(in query, (ref GenName name, ref Needs needs, ref CurrentAction action) =>
        {
            sims.Add(new SimStatus
            {
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
