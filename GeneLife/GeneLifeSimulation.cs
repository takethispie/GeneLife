using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Core;
using GeneLife.Core.Commands;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Entities.Generators;
using GeneLife.Core.Events;
using GeneLife.Core.Exceptions;
using GeneLife.Core.Items;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Hobbies.Systems;
using GeneLife.Knowledge.Systems;
using GeneLife.Survival;
using GeneLife.Survival.Components;
using LogSystem = GeneLife.Core.LogSystem;

namespace GeneLife;

public class GeneLifeSimulation : IDisposable
{
    private Arch.System.Group<float> Systems;
    private readonly ArchetypeFactory archetypeFactory;
    private global::JobScheduler.JobScheduler jobScheduler;
    private bool DefaultSystemsOverridden, DefaultArchetypesOverridden;
    private World overworld;
    public LogSystem LogSystem { get; }

    public GeneLifeSimulation()
    {
        overworld = World.Create();
        Systems = new Arch.System.Group<float>();
        archetypeFactory = new ArchetypeFactory();
        LogSystem = new LogSystem(false);
        jobScheduler = new global::JobScheduler.JobScheduler("glife");
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
            archetypeFactory.RegisterFactory(new NpcArchetypeFactory());
            archetypeFactory.RegisterFactory(new BuildingsArchetypeFactory());
            EventBus.Send(new LogEvent { Message = "All archetypes factories loaded" });
        }

        if (!overrideDefaultSystems)
        {
            CoreSystem.Register(overworld, Systems, archetypeFactory);
            //Systems.Add(new LearningSystem(overworld));
            //Systems.Add(new HobbySystem(overworld));
            SurvivalSystem.Register(overworld, Systems, archetypeFactory);
            EventBus.Send(new LogEvent { Message = "All systems loaded" });
        }

        EventBus.Send(new LogEvent { Message = "Simulation Initialized" });
    }

    public Entity AddNPC(Sex sex, int startAge = 0)
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden)
            throw new DefaultArchetypesAndSystemNotAvailableException("Can't create NPC when default archetypes and systems are overridden");
        var entity = PersonGenerator.CreatePure(overworld, sex, startAge);
        return entity;
    }

    public List<Entity> GetAllLivingNPC()
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden) return [];
        var query = new QueryDescription().WithAll<Living, Human>();
        var entities = new List<Entity>();
        overworld.GetEntities(query, entities);
        return entities;
    }

    public List<Entity> GetAllBuildings()
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden) return [];
        var query = new QueryDescription().WithAll<Adress, Position>();
        var entities = new List<Entity>();
        overworld.GetEntities(query, entities);
        return entities;
    }

    public void SendCommand(GiveCommand command)
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden) return;
        var livingEntities = new QueryDescription().WithAll<Living, Human, Inventory>();
        overworld.Query(in livingEntities, (ref Living living, ref Human human, ref Inventory inventory) =>
        {
            if (!human.FirstName.Equals(command.TargetFirstName, StringComparison.CurrentCultureIgnoreCase) ||
                !human.LastName.Equals(command.TargetLastName, StringComparison.CurrentCultureIgnoreCase)) return;
            var idx = inventory.GetItems().ToList().FindIndex(x => x.Type == ItemType.None);
            if (idx == -1) return;
            inventory.Store(command.Item);
            EventBus.Send(new LogEvent
            {
                Message =
                    $"item with id {command.Item.Id} of type {command.Item.Type} was given to {human.FullName()}"
            });
        });
    }

    public string SendCommand(CreateCityCommand command)
    {
        switch (command.Size)
        {
            case TemplateCitySize.Small:
                TemplateCityGenerator.CreateSmallCity(overworld);
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

    public ComponentType[] GetArchetype(string name) => archetypeFactory.Build(name);

    public void Dispose()
    {
        overworld.Dispose();
        Systems.Dispose();
    }

    public Entity[] GetLivingEntities()
    {
        if (DefaultArchetypesOverridden || DefaultSystemsOverridden) return [];
        var entities = new List<Entity>();
        overworld.GetEntities(in new QueryDescription().WithAll<Living, Position>(), entities);
        return [..entities];
    }
}