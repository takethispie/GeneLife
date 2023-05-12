using FluentAssertions;
using GeneLife.Sibyl.Components;
using GeneLife.Sibyl.Core;
using GeneLife.Sibyl.Services;

namespace GeneLife.Tests.Services;

public class KnowledgeServiceTests
{
    private Learning _learning;
    private List<Knowledge> _knowledgeList;

    public KnowledgeServiceTests()
    {
        _knowledgeList = new List<Knowledge>
        {
            new (KnowledgeCategory.Biology, KnowledgeLevel.Beginner),
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
    }
    
    [Fact]
    public void ShouldHaveNoneKnowledge() 
        => KnowledgeService.GetKnowledgeLevel(KnowledgeCategory.Communication, _knowledgeList).Should().Be(KnowledgeLevel.None);

    [Fact]
    public void ShouldHaveBeginnerKnowledge() 
        => KnowledgeService.GetKnowledgeLevel(KnowledgeCategory.Biology, _knowledgeList).Should().Be(KnowledgeLevel.Beginner);

    [Fact]
    public void ShouldStartLearning()
    {
        var kl = new KnowledgeList { KnownCategories = _knowledgeList.ToArray() };
        (_learning, kl) = KnowledgeService.LearningLoop(_learning, kl, 0.8f);
        kl.KnownCategories.Should().HaveCount(2);
        kl.KnownCategories.FirstOrDefault(x => x.Category == KnowledgeCategory.Cooking)
            .Level.Should().Be(KnowledgeLevel.None);
        _learning.CurrentLearningLevel.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ShouldFinishLearning()
    {
        var kl = new KnowledgeList { KnownCategories = _knowledgeList.ToArray() };
        (_learning, kl) = KnowledgeService.LearningLoop(_learning, kl, 100f);
        kl.KnownCategories.Should().HaveCount(2);
        kl.KnownCategories.FirstOrDefault(x => x.Category == KnowledgeCategory.Cooking)
            .Level.Should().Be(KnowledgeLevel.Beginner);
        _learning.CurrentLearningLevel.Should().BeGreaterThan(0);
    }
}