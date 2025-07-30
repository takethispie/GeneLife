using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Generators;
using Genelife.Domain.Work;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Generators;

public class GenerateEmploymentTests
{
    private readonly GenerateEmployment _generator = new();

    [Fact]
    public void Execute_ShouldCreateValidEmployment()
    {
        // Arrange
        var human = TestDataBuilder.CreateHuman();

        // Act
        var employment = _generator.Execute(human);

        // Assert
        employment.Should().NotBeNull();
        employment.Skills.Should().NotBeEmpty();
        employment.YearsOfExperience.Should().BeGreaterThanOrEqualTo(0);
        employment.Skills.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Execute_ShouldGenerateExperienceBasedOnAge()
    {
        // Arrange
        var youngHuman = TestDataBuilder.CreateHuman(birthday: DateTime.Now.AddYears(-20)); // 20 years old
        var olderHuman = TestDataBuilder.CreateHuman(birthday: DateTime.Now.AddYears(-40)); // 40 years old

        // Act
        var youngEmployment = _generator.Execute(youngHuman);
        var olderEmployment = _generator.Execute(olderHuman);

        // Assert
        youngEmployment.YearsOfExperience.Should().BeLessOrEqualTo(2); // Max 2 years for 20-year-old
        olderEmployment.YearsOfExperience.Should().BeLessOrEqualTo(22); // Max 22 years for 40-year-old
    }

    [Fact]
    public void Execute_ShouldCapExperienceAt25Years()
    {
        // Arrange
        var veryOldHuman = TestDataBuilder.CreateHuman(birthday: DateTime.Now.AddYears(-70)); // 70 years old

        // Act
        var employment = _generator.Execute(veryOldHuman);

        // Assert
        employment.YearsOfExperience.Should().BeLessOrEqualTo(25);
    }

    [Fact]
    public void Execute_ShouldGenerateMoreSkillsForExperiencedWorkers()
    {
        // Arrange
        var experiencedHuman = TestDataBuilder.CreateHuman(birthday: DateTime.Now.AddYears(-45)); // 45 years old

        // Act
        var employment = _generator.Execute(experiencedHuman);

        // Assert
        employment.Skills.Should().HaveCountGreaterThan(0); // Should have many skills
    }

    [Theory]
    [InlineData(JobLevel.Entry)]
    [InlineData(JobLevel.Senior)]
    [InlineData(JobLevel.Executive)]
    public void GenerateDesiredSalary_ShouldReturnReasonableSalary(JobLevel level)
    {
        // Arrange
        var employment = TestDataBuilder.CreateEmployment([], 5, Guid.Empty);
        var jobPosting = TestDataBuilder.CreateJobPosting(level: level, salaryMin: 50000m, salaryMax: 100000m);

        // Act
        var salary = _generator.GenerateDesiredSalary(employment, jobPosting);

        // Assert
        salary.Should().BeGreaterThan(0);
        salary.Should().BeLessOrEqualTo(jobPosting.SalaryMax * 1.3m); // Within reasonable range
    }

    [Fact]
    public void GenerateDesiredSalary_ShouldIncreaseWithExperience()
    {
        // Arrange
        var juniorEmployment = TestDataBuilder.CreateEmployment([], 1, Guid.Empty);
        var seniorEmployment = TestDataBuilder.CreateEmployment([],10, Guid.Empty);
        var jobPosting = TestDataBuilder.CreateJobPosting(level: JobLevel.Mid, salaryMin: 30000m, salaryMax: 60000m);

        // Act
        var juniorSalary = _generator.GenerateDesiredSalary(juniorEmployment, jobPosting);
        var seniorSalary = _generator.GenerateDesiredSalary(seniorEmployment, jobPosting);

        // Assert
        seniorSalary.Should().BeGreaterThan(juniorSalary);
    }
}