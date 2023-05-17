﻿using Arch.Bus;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Common.Components.Utils;
using GeneLife.Common.Data;
using GeneLife.Common.Entities;
using GeneLife.Common.Entities.Factories;
using GeneLife.Common.Entities.Generators;
using GeneLife.Demeter;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Oracle;
using GeneLife.Sibyl;
using LogSystem = GeneLife.Common.Systems.LogSystem;

namespace GeneLife;

public class GeneLifeSimulation : IDisposable
{
    public World Overworld { get; init; }
    
    private Arch.System.Group<float> Systems;
    private ArchetypeFactory _archetypeFactory;
    public LogSystem LogSystem;
    private global::JobScheduler.JobScheduler _jobScheduler;

    public GeneLifeSimulation()
    {
        Overworld = World.Create();
        Systems = new Arch.System.Group<float>();
        _archetypeFactory = new ArchetypeFactory();
        LogSystem = new LogSystem(false);
        _jobScheduler = new global::JobScheduler.JobScheduler("glife");
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
            SibylSystem.Register(Overworld, Systems);
            OracleSystem.Register(Overworld, Systems);
            DemeterSystem.Register(Overworld, Systems);
            EventBus.Send(new LogEvent { Message = "All systems loaded" });
        }
        
        EventBus.Send(new LogEvent { Message = "Simulation Initialized" });
    }

    public string AddNPC(Sex sex, int startAge = 0)
    {
        var entity = PersonGenerator.CreatePure(Overworld, sex, startAge);
        var identity = entity.Get<Identity>();
        return $"{identity.FirstName} {identity.LastName} was created";
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
        Overworld.Dispose();
        Systems.Dispose();
    }
}