using FluentAssertions;
using Genelife.Domain;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class JobPostingTests
{
    [Fact]
    public void JobPosting_ShouldCreateWithValidProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var title = "Senior Software Engineer";
        var description = "We are looking for a senior software engineer...";
        var requirements = new List<string> { "C#", ".NET", "SQL" };
        var salaryMin = 80000m;
        var salaryMax = 120000m;
        var level = JobLevel.Senior;
        var industry = CompanyType.Technology;
        var postedDate = DateTime.UtcNow.AddDays(-1);
        var expiryDate = DateTime.UtcNow.AddDays(30);
        var status = JobPostingStatus.Active;
        var maxApplications = 150;

        // Act
        var jobPosting = new JobPosting(
            id, companyId, title, description, requirements,
            salaryMin, salaryMax, level, industry, postedDate,
            expiryDate, status, maxApplications);

        // Assert
        jobPosting.Id.Should().Be(id);
        jobPosting.CompanyId.Should().Be(companyId);
        jobPosting.Title.Should().Be(title);
        jobPosting.Description.Should().Be(description);
        jobPosting.Requirements.Should().BeEquivalentTo(requirements);
        jobPosting.SalaryMin.Should().Be(salaryMin);
        jobPosting.SalaryMax.Should().Be(salaryMax);
        jobPosting.Level.Should().Be(level);
        jobPosting.Industry.Should().Be(industry);
        jobPosting.PostedDate.Should().Be(postedDate);
        jobPosting.ExpiryDate.Should().Be(expiryDate);
        jobPosting.Status.Should().Be(status);
        jobPosting.MaxApplications.Should().Be(maxApplications);
    }

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
    public void JobPosting_ShouldSupportRecordEquality()
    {
        // Arrange
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var requirements = new List<string> { "C#", ".NET", "SQL" };
        var postedDate = new DateTime(2023, 1, 1);
        var expiryDate = new DateTime(2023, 12, 31);
        var jobPosting1 = TestDataBuilder.CreateJobPosting(id: id, companyId: companyId, title: "Developer", description: "Test description", requirements: requirements, salaryMin: 50000m, salaryMax: 80000m, level: JobLevel.Mid, industry: CompanyType.Technology, postedDate: postedDate, expiryDate: expiryDate, status: JobPostingStatus.Active, maxApplications: 100);
        var jobPosting2 = TestDataBuilder.CreateJobPosting(id: id, companyId: companyId, title: "Developer", description: "Test description", requirements: requirements, salaryMin: 50000m, salaryMax: 80000m, level: JobLevel.Mid, industry: CompanyType.Technology, postedDate: postedDate, expiryDate: expiryDate, status: JobPostingStatus.Active, maxApplications: 100);
        var jobPosting3 = TestDataBuilder.CreateJobPosting(id: Guid.NewGuid(), companyId: companyId, title: "Developer", description: "Test description", requirements: requirements, salaryMin: 50000m, salaryMax: 80000m, level: JobLevel.Mid, industry: CompanyType.Technology, postedDate: postedDate, expiryDate: expiryDate, status: JobPostingStatus.Active, maxApplications: 100);

        // Act & Assert
        jobPosting1.Should().Be(jobPosting2);
        jobPosting1.Should().NotBe(jobPosting3);
    }

    [Fact]
    public void JobPosting_ShouldSupportWithExpressions()
    {
        // Arrange
        var originalJobPosting = TestDataBuilder.CreateJobPosting(status: JobPostingStatus.Draft);

        // Act
        var updatedJobPosting = originalJobPosting with { Status = JobPostingStatus.Active };

        // Assert
        updatedJobPosting.Status.Should().Be(JobPostingStatus.Active);
        updatedJobPosting.Title.Should().Be(originalJobPosting.Title);
        updatedJobPosting.Id.Should().Be(originalJobPosting.Id);
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
        var salaryMin = 50000m;
        var salaryMax = 80000m;

        // Act
        var jobPosting = TestDataBuilder.CreateJobPosting(salaryMin: salaryMin, salaryMax: salaryMax);

        // Assert
        jobPosting.SalaryMin.Should().Be(salaryMin);
        jobPosting.SalaryMax.Should().Be(salaryMax);
        jobPosting.SalaryMax.Should().BeGreaterThanOrEqualTo(jobPosting.SalaryMin);
    }
}