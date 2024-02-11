using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using FluentAssertions;
using GeneLife.Core.Components;
using GeneLife.Core.Entities.Generators;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Knowledge;
using GeneLife.Knowledge.Components;
using GeneLife.Survival.Components;

namespace GeneLife.Tests.World
{
    public class LearningPersonTests
    {
        private Learning learning;
        private List<(KnowledgeCategory knowledgeCategory, KnowledgeLevel level)> knowledgeList;
        private Arch.Core.World world;
        private Entity human;
        private readonly Group<float> systems;

        public LearningPersonTests()
        {
            world = Arch.Core.World.Create();
            knowledgeList = new List<(KnowledgeCategory knowledgeCategory, KnowledgeLevel level)>
        {
            (KnowledgeCategory.Biology, KnowledgeLevel.Beginner),
        };

            learning = new Learning()
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
            systems = new Group<float>();
            systems.Initialize();
        }

        public void RunSystemsOnce(float delta)
        {
            systems.BeforeUpdate(delta);
            systems.Update(delta);
            systems.AfterUpdate(delta);
        }

        [Fact]
        public void HumanShouldLearnCooking()
        {
            this.human = PersonGenerator.CreatePure(world, Sex.Male, 20);
            this.human.Add(this.learning);
            var allTheLearningNPCQuery = new QueryDescription().WithAll<Learning, Living, KnowledgeList>();
            var entities = new List<Entity>();
            world.GetEntities(allTheLearningNPCQuery, entities);
            entities.Count.Should().BeGreaterThan(0);
            RunSystemsOnce(1f);
            world.GetEntities(allTheLearningNPCQuery, entities);
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
            systems.Dispose();
        }
    }
}