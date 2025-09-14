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
        // Arrange
        var skills = new List<string> { "C#", ".NET", "SQL", "Azure", "Docker", "Kubernetes" };

        // Act
        var application = TestDataBuilder.CreateJobApplication(skills: skills);

        // Assert
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
        // Arrange & Act
        var application = TestDataBuilder.CreateJobApplication(yearsOfExperience: years);

        // Assert
        application.YearsOfExperience.Should().Be(years);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void JobApplication_ShouldAcceptValidMatchScores(float score)
    {
        // Arrange & Act
        var application = TestDataBuilder.CreateJobApplication(matchScore: score);

        // Assert
        application.MatchScore.Should().Be(score);
    }
}
