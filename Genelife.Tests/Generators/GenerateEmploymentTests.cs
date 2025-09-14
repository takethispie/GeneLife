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
        var human = TestDataBuilder.CreateHuman();
        var employment = _generator.Execute(human);
        employment.Should().NotBeNull();
        employment.Skills.Should().NotBeEmpty();
        employment.YearsOfExperience.Should().BeGreaterThanOrEqualTo(0);
        employment.Skills.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Execute_ShouldGenerateExperienceBasedOnAge()
    {
        var youngHuman = TestDataBuilder.CreateHuman(birthday: DateTime.Now.AddYears(-20));
        var olderHuman = TestDataBuilder.CreateHuman(birthday: DateTime.Now.AddYears(-40));
        var youngEmployment = _generator.Execute(youngHuman);
        var olderEmployment = _generator.Execute(olderHuman);
        youngEmployment.YearsOfExperience.Should().BeLessOrEqualTo(2);
        olderEmployment.YearsOfExperience.Should().BeLessOrEqualTo(22);
    }

    [Fact]
    public void Execute_ShouldCapExperienceAt25Years()
    {
        var veryOldHuman = TestDataBuilder.CreateHuman(birthday: DateTime.Now.AddYears(-70));
        var employment = _generator.Execute(veryOldHuman);
        employment.YearsOfExperience.Should().BeLessOrEqualTo(25);
    }

    [Fact]
    public void Execute_ShouldGenerateMoreSkillsForExperiencedWorkers()
    {
        var experiencedHuman = TestDataBuilder.CreateHuman(birthday: DateTime.Now.AddYears(-45));
        var employment = _generator.Execute(experiencedHuman);
        employment.Skills.Should().HaveCountGreaterThan(0);
    }

    [Theory]
    [InlineData(JobLevel.Entry)]
    [InlineData(JobLevel.Senior)]
    [InlineData(JobLevel.Executive)]
    public void GenerateDesiredSalary_ShouldReturnReasonableSalary(JobLevel level)
    {
        var employment = TestDataBuilder.CreateEmployment([], 5, Guid.Empty);
        var jobPosting = TestDataBuilder.CreateJobPosting(level: level, salaryMin: 50000f, salaryMax: 100000f);
        var salary = _generator.GenerateDesiredSalary(employment, jobPosting);
        salary.Should().BeGreaterThan(0);
        salary.Should().BeLessOrEqualTo(jobPosting.SalaryMax * 1.3f);
    }

    [Fact]
    public void GenerateDesiredSalary_ShouldIncreaseWithExperience()
    {
        var juniorEmployment = TestDataBuilder.CreateEmployment([], 1, Guid.Empty);
        var seniorEmployment = TestDataBuilder.CreateEmployment([],10, Guid.Empty);
        var jobPosting = TestDataBuilder.CreateJobPosting(level: JobLevel.Mid, salaryMin: 30000f, salaryMax: 60000f);
        var juniorSalary = _generator.GenerateDesiredSalary(juniorEmployment, jobPosting);
        var seniorSalary = _generator.GenerateDesiredSalary(seniorEmployment, jobPosting);
        seniorSalary.Should().BeGreaterThan(juniorSalary);
    }
}