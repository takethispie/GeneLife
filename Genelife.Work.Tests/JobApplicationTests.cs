using FluentAssertions;
using Genelife.Work.Messages.DTOs.Skills;
using Xunit;

namespace Genelife.Work.Tests;

public class JobApplicationTests
{
    [Fact]
    public void JobApplication_ShouldCreateWithDefaultMatchScore()
    {
        var skillSet = new SkillSet();
        var application = TestDataBuilder.CreateJobApplication(skillSet);
        application.MatchScore.Should().BeGreaterThanOrEqualTo(0.0f);
        application.MatchScore.Should().BeLessThanOrEqualTo(1.0f);
    }

    [Fact]
    public void JobApplication_ShouldAllowMultipleSkills()
    {
        var skillSet = new SkillSet() {
            TechnicalSkills = {
                TechnicalSkill.Agile,
                TechnicalSkill.Angular,
                TechnicalSkill.CICD,
                TechnicalSkill.Git,
                TechnicalSkill.CSharp,
                TechnicalSkill.JavaScript
            }
        };
        var application = TestDataBuilder.CreateJobApplication(skillSet);
        application.Skills.Count.Should().Be(6);
        application.Skills.Should().BeEquivalentTo(skillSet);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(40)]
    public void JobApplication_ShouldAcceptValidYearsOfExperience(int years) {
        var skillSet = new SkillSet();
        var application = TestDataBuilder.CreateJobApplication(skillSet, yearsOfExperience: years);
        application.YearsOfExperience.Should().Be(years);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void JobApplication_ShouldAcceptValidMatchScores(float score)
    {
        var skillSet = new SkillSet();
        var application = TestDataBuilder.CreateJobApplication(skillSet, matchScore: score);
        application.MatchScore.Should().Be(score);
    }
}
