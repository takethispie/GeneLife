using FluentAssertions;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class JobApplicationTests
{
    [Fact]
    public void JobApplication_ShouldCreateWithDefaultMatchScore()
    {
        // Arrange & Act
        var application = TestDataBuilder.CreateJobApplication();

        // Assert
        application.MatchScore.Should().BeGreaterThanOrEqualTo(0.0f);
        application.MatchScore.Should().BeLessThanOrEqualTo(1.0f);
    }

    [Fact]
    public void JobApplication_ShouldAllowEmptySkills()
    {
        // Arrange & Act
        var application = TestDataBuilder.CreateJobApplication(skills: new List<string>());

        // Assert
        application.Skills.Should().BeEmpty();
    }

    [Fact]
    public void JobApplication_ShouldAllowMultipleSkills()
    {
        var skills = new List<string> { "C#", ".NET", "SQL", "Azure", "Docker", "Kubernetes" };
        var application = TestDataBuilder.CreateJobApplication(skills: skills);
        application.Skills.Should().HaveCount(6);
        application.Skills.Should().BeEquivalentTo(skills);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(40)]
    public void JobApplication_ShouldAcceptValidYearsOfExperience(int years)
    {
        var application = TestDataBuilder.CreateJobApplication(yearsOfExperience: years);
        application.YearsOfExperience.Should().Be(years);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void JobApplication_ShouldAcceptValidMatchScores(float score)
    {
        var application = TestDataBuilder.CreateJobApplication(matchScore: score);
        application.MatchScore.Should().Be(score);
    }
}
