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