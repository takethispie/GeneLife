using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Athena;
using GeneLife.Core.Commands;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Components.Characters;
using GeneLife.Core.Data;
using GeneLife.Core.Entities;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Entities.Generators;
using GeneLife.Core.Events;
using GeneLife.Core.Exceptions;
using GeneLife.Core.Extensions;
using GeneLife.Core.Items;
using GeneLife.Demeter;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Oracle;
using GeneLife.Sibyl;
using LogSystem = GeneLife.Core.LogSystem;

namespace GeneLife;

public class GeneLifeSimulation : IDisposable
{
    private Arch.System.Group<float> Systems;
    private ArchetypeFactory _archetypeFactory;
    private global::JobScheduler.JobScheduler _jobScheduler;
    private bool DefaultSystemsOverridden, DefaultArchetypesOverridden;
    private World _overworld { get; }
    public LogSystem LogSystem { get; }
    public Item[] Items { get; }
    public ItemWithPrice[] ItemsWithPrices { get; }

    public GeneLifeSimulation()
    {
        _overworld = World.Create();
        Systems = new Arch.System.Group<float>();
        _archetypeFactory = new ArchetypeFactory();
        LogSystem = new LogSystem(false);
        _jobScheduler = new global::JobScheduler.JobScheduler("glife");
        Items = new BaseItemGenerator().GetItemList();
        ItemsWithPrices = new BaseItemWithWithPriceGenerator().GetItemsWithPrice(Items);
        DefaultArchetypesOverridden = false;
        DefaultSystemsOverridden = false;
    }

    /// <summary>
    /// add an Arch system 
    /// </summary>
    /// <param name="system">system to add to the simulation</param>
    public void AddSystem(BaseSystem<World, float> system) => Systems.Add(system);

    /// <summary>
    /// initialize the simulation systems
    /// </summary>
    /// <param name="overrideDefaultSystems">override loading default systems, 
    /// this will remove any baked in system, might break the simulation</param>
    /// <param name="overrideDefaultArchetypes">override loading default archetypes, 
    /// this will remove any baked in archetypes, might break the simulation</param>
    public void Initialize(bool overrideDefaultSystems = false, bool overrideDefaultArchetypes = false)
    {
        Systems.Initialize();
        DefaultArchetypesOverridden = overrideDefaultArchetypes;
        DefaultSystemsOverridden = overrideDefaultSystems;

        if (!overrideDefaultArchetypes)
        {
            _archetypeFactory.RegisterFactory(new NpcArchetypeFactory());
            _archetypeFactory.RegisterFactory(new VehicleArchetypeFactory());
            _archetypeFactory.RegisterFactory(new BuildingsArchetypeFactory());
            _archetypeFactory.RegisterFactory(new LiquidsArchetypeFactory());
            EventBus.Send(new LogEvent { Message = "All archetypes factories loaded" });
        }
        
        if (!overrideDefaultSystems)
        {
            SibylSystem.Register(_overworld, Systems);
            OracleSystem.Register(_overworld, Systems);
            DemeterSystem.Register(_overworld, Systems, _archetypeFactory, ItemsWithPrices);
            AthenaSystem.Register(_overworld, Systems, _archetypeFactory);
            EventBus.Send(new LogEvent { Message = "All systems loaded" });
        }

        EventBus.Send(new LogEvent { Message = "Simulation Initialized" });
    }

    public Entity AddNPC(Sex sex, int startAge = 0)
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden)
            throw new DefaultArchetypesAndSystemNotAvailableException("Can't create NPC when default archetypes and systems are overridden");
        var entity = PersonGenerator.CreatePure(_overworld, sex, startAge);
        return entity;
    }

    public List<Entity> GetAllLivingNPC()
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden) return new List<Entity>();
        var query = new QueryDescription().WithAll<Living, Identity>();
        var entities = new List<Entity>();
        _overworld.GetEntities(query, entities);
        return entities;
    }

    public List<Entity> GetAllBuildings()
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden) return new List<Entity>();
        var query = new QueryDescription().WithAll<Adress, Position>();
        var entities = new List<Entity>();
        _overworld.GetEntities(query, entities);
        return entities;
    }

    public void SendCommand(GiveCommand command)
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden) return;
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

    public string SendCommand(CreateCityCommand command)
    {
        switch (command.Size)
        {
            case TemplateCitySize.Small:
                TemplateCityGenerator.CreateSmallCity(_overworld);
                return "Created Small City";
            
            default: return "";
        } 
    }

    public string SendCommand(SetTicksPerDayCommand command)
    {
        Constants.TicksPerDay = command.Ticks;
        return "";
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

    public Entity[] GetLivingEntities()
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden) return Array.Empty<Entity>();
        var entities = new List<Entity>();
        _overworld.GetEntities(in new QueryDescription().WithAll<Living, Position>(), entities);
        return entities.ToArray();
    }
}