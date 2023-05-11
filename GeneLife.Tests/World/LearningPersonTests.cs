using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using FluentAssertions;
using GeneLife.CommonComponents;
using GeneLife.Entities;
using GeneLife.Generators;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Sybil.Components;
using GeneLife.Sybil.Core;
using GeneLife.Sybil.Systems;

namespace GeneLife.Tests.World;

public class LearningPersonTests
{
    private Learning _learning;
    private List<(KnowledgeCategory knowledgeCategory, KnowledgeLevel level)> _knowledgeList;
    private Arch.Core.World _world;
    private Entity _human;
    private readonly Group<float> _systems;

    public LearningPersonTests()
    {
        _world = Arch.Core.World.Create();
        _human = PersonGenerator.CreatePure(_world, Sex.Male, 300000);
        _knowledgeList = new List<(KnowledgeCategory knowledgeCategory, KnowledgeLevel level)>
        {
            (KnowledgeCategory.Biology, KnowledgeLevel.Beginner),
        };
        
        _learning = new Learning()
        {
            CanLearn = false,
            Class = new Class
            {
                Category = KnowledgeCategory.Cooking,
                LearningRate = 2,
                TargetLearningLevel = 100f,
                Level = KnowledgeLevel.Beginner,
                MinRequiredLevel = KnowledgeLevel.None,
                Name = "beginner cooking classes"
            },
            CurrentLearningLevel = 0,
            Finished = false
        };
        _systems = new Group<float>(new LearningSystem(_world));
        _systems.Initialize();
    }

    public void RunSystemsOnce(float delta)
    {
        _systems.BeforeUpdate(delta);    
        _systems.Update(delta);          
        _systems.AfterUpdate(delta);     
    }

    [Fact]
    public void HumanShouldLearnCooking()
    {
        _human.Add(_learning);
        var allTheLearningNPCQuery = new QueryDescription().WithAll<Learning, Living, KnowledgeList>();
        var entities = new List<Entity>();
        _world.GetEntities(allTheLearningNPCQuery, entities);
        entities.Count.Should().BeGreaterThan(0);
        RunSystemsOnce(1f);
        _world.GetEntities(allTheLearningNPCQuery, entities);
        entities.Count.Should().BeGreaterThan(0);
        var human = entities.First();
        var learning = human.Get<Learning>();
        learning.Finished.Should().Be(false);
        learning.CurrentLearningLevel.Should().BeGreaterThan(0);
        RunSystemsOnce(1000f);
        human.Has<Learning>().Should().BeFalse();
        var knowledgeList = human.Get<KnowledgeList>();
        knowledgeList.KnownCategories.ToList()
            .All(x => x is { Category: KnowledgeCategory.Cooking, Level: KnowledgeLevel.Beginner })
            .Should().BeTrue();
        _systems.Dispose();
    }
}