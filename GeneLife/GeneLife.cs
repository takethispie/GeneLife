using Arch.Core;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Common.Entities;
using GeneLife.Common.Entities.Factories;
using GeneLife.Demeter;
using GeneLife.Oracle;
using GeneLife.Sibyl;
using LogSystem = GeneLife.Common.Systems.LogSystem;

namespace GeneLife;

public class GeneLife : IDisposable
{
    public World Overworld { get; init; }
    
    private Arch.System.Group<float> Systems;
    private ArchetypeFactory _archetypeFactory;
    private LogSystem _logSystem;
    private global::JobScheduler.JobScheduler _jobScheduler;

    public GeneLife()
    {
        Overworld = World.Create();
        Systems = new Arch.System.Group<float>();
        _archetypeFactory = new ArchetypeFactory();
        _logSystem = new LogSystem();
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
        }
        
        if (!overrideDefaultSystems)
        {
            SibylSystem.Register(Overworld, Systems);
            OracleSystem.Register(Overworld, Systems);
            DemeterSystem.Register(Overworld, Systems);
        }
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