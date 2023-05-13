using Arch.Core;
using Arch.Core.Utils;
using Arch.System;
using GeneLife.Entities;
using GeneLife.Entities.Factories;
using GeneLife.Generators;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Sibyl;

namespace GeneLife;

public class GeneLife : IDisposable
{
    public World Main { get; init; }
    private Arch.System.Group<float> Systems;

    private ArchetypeFactory _archetypeFactory;

    public GeneLife()
    {
        Main = World.Create();
        Systems = new Arch.System.Group<float>();
        _archetypeFactory = new ArchetypeFactory();
        
    }

    public void AddSystem(BaseSystem<World, float> system) => Systems.Add(system);

    public void Initialize()
    {
        Systems.Initialize();
        Sybil.Register(Main, Systems);
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

    public void AddNewHuman(Sex sex)
    {
        PersonGenerator.CreatePure(Main, sex);
    }

    public ComponentType[] GetArchetype(string name) => _archetypeFactory.Build(name);

    public void Dispose()
    {
        Main.Dispose();
        Systems.Dispose();
    }
}