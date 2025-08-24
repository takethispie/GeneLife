using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class JobPostingTests
{
    [Fact]
    public void JobPosting_ShouldCreateWithDefaultMaxApplications()
    {
        // Arrange & Act
        var jobPosting = TestDataBuilder.CreateJobPosting();

        // Assert
        jobPosting.MaxApplications.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(JobLevel.Entry)]
    [InlineData(JobLevel.Junior)]
    [InlineData(JobLevel.Mid)]
    [InlineData(JobLevel.Senior)]
    [InlineData(JobLevel.Lead)]
    [InlineData(JobLevel.Manager)]
    [InlineData(JobLevel.Director)]
    [InlineData(JobLevel.Executive)]
    public void JobPosting_ShouldAcceptAllJobLevels(JobLevel level)
    {
        // Arrange & Act
        var jobPosting = TestDataBuilder.CreateJobPosting(level: level);

        // Assert
        jobPosting.Level.Should().Be(level);
    }

    [Theory]
    [InlineData(JobPostingStatus.Draft)]
    [InlineData(JobPostingStatus.Active)]
    [InlineData(JobPostingStatus.Paused)]
    [InlineData(JobPostingStatus.Filled)]
    [InlineData(JobPostingStatus.Expired)]
    [InlineData(JobPostingStatus.Cancelled)]
    public void JobPosting_ShouldAcceptAllStatuses(JobPostingStatus status)
    {
        // Arrange & Act
        var jobPosting = TestDataBuilder.CreateJobPosting(status: status);

        // Assert
        jobPosting.Status.Should().Be(status);
    }

    [Fact]
    public void JobPosting_ShouldAllowEmptyRequirements()
    {
        // Arrange & Act
        var jobPosting = TestDataBuilder.CreateJobPosting(requirements: new List<string>());

        // Assert
        jobPosting.Requirements.Should().BeEmpty();
    }

    [Fact]
    public void JobPosting_ShouldAllowMultipleRequirements()
    {
        // Arrange
        var requirements = new List<string> { "C#", ".NET", "SQL", "Azure", "Docker" };

        // Act
        var jobPosting = TestDataBuilder.CreateJobPosting(requirements: requirements);

        // Assert
        jobPosting.Requirements.Should().HaveCount(5);
        jobPosting.Requirements.Should().BeEquivalentTo(requirements);
    }

    [Fact]
    public void JobPosting_ShouldAllowNullExpiryDate()
    {
        // Arrange & Act
        var jobPosting = TestDataBuilder.CreateJobPosting(expiryDate: null);

        // Assert
        jobPosting.ExpiryDate.Should().BeNull();
    }

    [Fact]
    public void JobPosting_ShouldValidateSalaryRange()
    {
        // Arrange
        var salaryMin = 50000f;
        var salaryMax = 80000f;

        // Act
        var jobPosting = TestDataBuilder.CreateJobPosting(salaryMin: salaryMin, salaryMax: salaryMax);

        // Assert
        jobPosting.SalaryMin.Should().Be(salaryMin);
        jobPosting.SalaryMax.Should().Be(salaryMax);
        jobPosting.SalaryMax.Should().BeGreaterThanOrEqualTo(jobPosting.SalaryMin);
    }
}