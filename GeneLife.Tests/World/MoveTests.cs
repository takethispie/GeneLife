using Arch.Core;
using Arch.System;
using GeneLife.Athena;
using GeneLife.Core.Entities.Factories;
using GeneLife.Tests.Factories;
using GeneLife.Core.Entities.Generators;
using GeneLife.Genetic.GeneticTraits;
using Arch.Core.Extensions;
using GeneLife.Core.Components;
using System.Numerics;
using FluentAssertions;

namespace GeneLife.Tests.World;
public class MoveTests
{
    private Arch.Core.World world;
    private Entity human;
    private readonly Group<float> systems;
    private ArchetypeFactory archetypeFactory;

    public void RunSystemsOnce(float delta)
    {
        systems.BeforeUpdate(delta);
        systems.Update(delta);
        systems.AfterUpdate(delta);
    }

    public MoveTests()
    {
        world = Arch.Core.World.Create();
        archetypeFactory = new ArchetypeFactory();
        archetypeFactory.RegisterFactory(new NpcArchetypeFactory());
        archetypeFactory.RegisterFactory(new VehicleArchetypeFactory());
        archetypeFactory.RegisterFactory(new BuildingsArchetypeFactory());
        archetypeFactory.RegisterFactory(new LiquidsArchetypeFactory());
        systems = new Group<float>();
        AthenaSystem.Register(world, systems, archetypeFactory);
        systems.Initialize();
    }

    [Fact]
    public void ShouldMove()
    {
        human = PersonGenerator.CreatePure(world, Sex.Male, 20);
        human.Add(new Moving { Target = new Vector3(20, 20, 20), Velocity = 1 });
        RunSystemsOnce(20);
        human.Should().NotBeNull();
        var position = human.Get<Position>();
        position.Should().NotBeNull();
        position.Coordinates.X.Should().BeGreaterThan(0);
    }
}
