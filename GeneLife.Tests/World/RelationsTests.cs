using System.Numerics;
using Arch.Core.Extensions;
using Arch.System;
using Arch.Bus;
using FluentAssertions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Characters;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Oracle.Components;
using GeneLife.Oracle.Core;
using GeneLife.Oracle.Systems;
using GeneLife.Core.Data;
using GeneLife.Core.Entities.Generators;
using Xunit;
using Xunit.Abstractions;

namespace GeneLife.Tests.World;

public class RelationsTests
{
    private Arch.Core.World _world;
    private readonly Group<float> _systems;
    private readonly ITestOutputHelper output;
    private global::JobScheduler.JobScheduler _jobScheduler;

    public RelationsTests(ITestOutputHelper output)
    {
        _world = Arch.Core.World.Create();
        _systems = new Group<float>(new LoveInterestSystem(_world), new HobbySystem(_world));
        _systems.Initialize();
        this.output = output;
        _jobScheduler = new global::JobScheduler.JobScheduler("glife");
    }

    private void RunSystemsOnce(float delta)
    {
        _systems.BeforeUpdate(delta);
        _systems.Update(delta);
        _systems.AfterUpdate(delta);
    }

    [Fact]
    public void ShouldComputeAttractivenessOnCloseHumans()
    {
        var man = PersonGenerator.CreatePure(_world, Sex.Male, 20);
        man.Set(new Position(new Vector3(0, 2, 0)));
        var woman = PersonGenerator.CreatePure(_world, Sex.Female, 20);
        woman.Set(new Position(new Vector3(3, 0, 0)));
        RunSystemsOnce(800f);
        RunSystemsOnce(800f);
        RunSystemsOnce(800f);
        RunSystemsOnce(800f);
        if (man.Has<Relation>())
        {
            woman.Has<Relation>().Should().BeTrue();
            woman.Get<Relation>().PartnerId.Should().Be(man.Id);
        }

        if (woman.Has<Relation>())
        {
            man.Has<Relation>().Should().BeTrue();
            man.Get<Relation>().PartnerId.Should().Be(woman.Id);
        }
    }

    [Fact]
    public void ShouldNeverComputeAttractivenessOnFarFromEachOtherHumans()
    {
        var man = PersonGenerator.CreatePure(_world, Sex.Male, 20);
        man.Set(new Position(new Vector3(0, 40, 0)));
        var woman = PersonGenerator.CreatePure(_world, Sex.Female, 20);
        woman.Set(new Position(new Vector3(3, 0, 0)));
        RunSystemsOnce(800f);
        RunSystemsOnce(800f);
        RunSystemsOnce(800f);
        RunSystemsOnce(800f);
        woman.Has<Relation>().Should().BeFalse();
        man.Has<Relation>().Should().BeFalse();
    }

    [Fact(Skip = "performance only test")]
    public void ShouldComputeAttractivenessOnCloseHumansBigSample()
    {
        var man = PersonGenerator.CreatePure(_world, Sex.Male, 20);
        man.Set(new Position(new Vector3(0, 2, 0)));
        var woman = PersonGenerator.CreatePure(_world, Sex.Female, 20);
        woman.Set(new Position(new Vector3(3, 0, 0)));
        var rnd = new Random();
        const int factor = 3;
        for (var i = 0; i < 10000 * factor; i++)
        {
            var t = PersonGenerator.CreatePure(_world, Sex.Male, 20);
            t.Set(new Position(new Vector3(rnd.Next(0, 6000 * factor), rnd.Next(0, 6000 * factor),
                rnd.Next(0, 20))));
            var t2 = PersonGenerator.CreatePure(_world, Sex.Female, 20);
            t2.Set(new Position(new Vector3(rnd.Next(0, 6000 * factor), rnd.Next(0, 6000 * factor),
                rnd.Next(0, 20))));
        }
        RunSystemsOnce(800f);
    }

    [Fact]
    public void ShouldHaveEnoughMoneyForHobby()
    {
        Constants.HobbyChangeChances = 0;
        var man = PersonGenerator.CreatePure(_world, Sex.Male, 20);
        man.Add(new Wallet { Money = 1000f });
        man.Add(new Hobby { Type = HobbyType.Biking, NeedsMoney = true, MoneyPerWeek = 100, Started = DateTime.Now });
        RunSystemsOnce(Constants.TicksPerDay * 10);
        man.Get<Wallet>().Money.Should().BeLessThan(1000f);
    }
    
    [Fact]
    public void ShouldNotHaveEnoughMoneyForHobby()
    {
        Constants.HobbyChangeChances = 0;
        Constants.GettingHobbyChances = 0;
        var man = PersonGenerator.CreatePure(_world, Sex.Male, 20);
        man.Add(new Wallet { Money = 0 });
        man.Add(new Hobby { Type = HobbyType.Biking, NeedsMoney = true, MoneyPerWeek = 100, Started = DateTime.Now });
        RunSystemsOnce(Constants.TicksPerDay * 10);
        man.Has<Hobby>().Should().BeFalse();
    }
}