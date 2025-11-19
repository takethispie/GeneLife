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
        var jobPosting = TestDataBuilder.CreateJobPosting();
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
        var jobPosting = TestDataBuilder.CreateJobPosting(level: level);
        jobPosting.Level.Should().Be(level);
    }

    [Fact]
    public void JobPosting_ShouldValidateSalaryRange()
    {
        var salaryMin = 50000f;
        var salaryMax = 80000f;
        var jobPosting = TestDataBuilder.CreateJobPosting(salaryMin: salaryMin, salaryMax: salaryMax);
        jobPosting.SalaryMin.Should().Be(salaryMin);
        jobPosting.SalaryMax.Should().Be(salaryMax);
        jobPosting.SalaryMax.Should().BeGreaterThanOrEqualTo(jobPosting.SalaryMin);
    }
}