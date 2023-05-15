using Arch.Core;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Common.Entities;
using GeneLife.Common.Entities.Factories;
using GeneLife.Oracle;
using GeneLife.Sibyl;
using LogSystem = GeneLife.Common.Systems.LogSystem;

namespace GeneLife;

public class GeneLife : IDisposable
{
    public World Main { get; init; }
    
    private Arch.System.Group<float> Systems;
    private ArchetypeFactory _archetypeFactory;
    private LogSystem _logSystem;
    private global::JobScheduler.JobScheduler _jobScheduler;

    public GeneLife()
    {
        Main = World.Create();
        Systems = new Arch.System.Group<float>();
        _archetypeFactory = new ArchetypeFactory();
        _logSystem = new LogSystem();
        _jobScheduler = new global::JobScheduler.JobScheduler("glife");
    }

    public void AddSystem(BaseSystem<World, float> system) => Systems.Add(system);

    public void Initialize()
    {
        Systems.Initialize();
        SibylSystem.Register(Main, Systems);
        OracleSystem.Register(Main, Systems);
        _archetypeFactory.RegisterFactory(new NpcArchetypeFactory());
        _archetypeFactory.RegisterFactory(new VehicleArchetypeFactory());
        _archetypeFactory.RegisterFactory(new BuildingsArchetypeBuilderFactory());
        _archetypeFactory.RegisterFactory(new LiquidsArchetypeBuilderFactory());
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
        Main.Dispose();
        Systems.Dispose();
    }
}