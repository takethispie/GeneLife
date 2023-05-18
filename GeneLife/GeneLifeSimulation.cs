using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Utils;
using GeneLife.Common.Data;
using GeneLife.Common.Entities;
using GeneLife.Common.Entities.Factories;
using GeneLife.Common.Entities.Generators;
using GeneLife.Common.Systems;
using GeneLife.Core.Commands;
using GeneLife.Core.Events;
using GeneLife.Core.Items;
using GeneLife.Demeter;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Oracle;
using GeneLife.Sibyl;
using GeneLife.Utils;
using LogSystem = GeneLife.Common.Systems.LogSystem;

namespace GeneLife;

public class GeneLifeSimulation : IDisposable
{
    private Arch.System.Group<float> Systems;
    private ArchetypeFactory _archetypeFactory;
    private global::JobScheduler.JobScheduler _jobScheduler;
    private World _overworld { get; init; }
    public LogSystem LogSystem { get; init; }
    public List<Entity> Entities { get; init; }

    public GeneLifeSimulation()
    {
        _overworld = World.Create();
        Systems = new Arch.System.Group<float>();
        _archetypeFactory = new ArchetypeFactory();
        LogSystem = new LogSystem(false);
        _jobScheduler = new global::JobScheduler.JobScheduler("glife");
        Entities = new List<Entity>();
    }

    public void AddSystem(BaseSystem<World, float> system) => Systems.Add(system);

    public void Initialize(bool overrideDefaultSystems = false, bool overrideDefaultArchetypes = false)
    {
        Systems.Initialize();
        
        if (!overrideDefaultArchetypes)
        {
            _archetypeFactory.RegisterFactory(new NpcArchetypeFactory());
            _archetypeFactory.RegisterFactory(new VehicleArchetypeFactory());
            _archetypeFactory.RegisterFactory(new BuildingsArchetypeBuilderFactory());
            _archetypeFactory.RegisterFactory(new LiquidsArchetypeBuilderFactory());
            EventBus.Send(new LogEvent { Message = "All archetypes factories loaded" });
        }
        
        if (!overrideDefaultSystems)
        {
            SibylSystem.Register(_overworld, Systems);
            OracleSystem.Register(_overworld, Systems);
            DemeterSystem.Register(_overworld, Systems);
            EventBus.Send(new LogEvent { Message = "All systems loaded" });
        }
        
        EventBus.Send(new LogEvent { Message = "Simulation Initialized" });
    }

    public string AddNPC(Sex sex, int startAge = 0)
    {
        var entity = PersonGenerator.CreatePure(_overworld, sex, startAge);
        Entities.Add(entity);
        var identity = entity.Get<Identity>();
        return $"{identity.FullName()} was created";
    }

    public List<Entity> GetLivingNPC()
    {
        var query = new QueryDescription().WithAll<Living, Identity>();
        var entities = new List<Entity>();
        _overworld.GetEntities(query, entities);
        return entities;
    }

    public void SendCommand(GiveCommand command)
    {
        var livingEntities = new QueryDescription().WithAll<Living, Identity, Inventory>();
        _overworld.Query(in livingEntities, (ref Living living, ref Identity identity, ref Inventory inventory) =>
        {
            if (identity.FirstName.ToLower() != command.TargetFirstName ||
                identity.LastName.ToLower() != command.TargetLastName) return;
            var idx = inventory.items.ToList().FindIndex(x => x.Type == ItemType.None);
            if (idx == -1) return;
            inventory.items[idx] = command.Item;
            EventBus.Send(new LogEvent
            {
                Message =
                    $"item with id {command.Item.Id} of type {command.Item.Type} was given to {identity.FullName()}"
            });
        });
    }

    /// <summary>
    /// Update loop
    /// </summary>
    /// <param name="delta">elapsed time in seconds</param>
    public void Update(float delta)
    {
        Systems.BeforeUpdate(delta);    
        Systems.Update(delta);          
        Systems.AfterUpdate(delta);     
    }

    public ComponentType[] GetArchetype(string name) => _archetypeFactory.Build(name);

    public void Dispose()
    {
        _overworld.Dispose();
        Systems.Dispose();
    }
}