using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class EmploymentTests
{
    [Fact]
    public void Employment_ShouldCreateWithDefaults()
    {
        var skills = new List<string> { "C#", ".NET" };
        var yearsOfExperience = 3;
        var employment = new Employment(skills, yearsOfExperience, Guid.Empty);
        employment.Skills.Should().BeEquivalentTo(skills);
        employment.YearsOfExperience.Should().Be(yearsOfExperience);
        employment.CurrentEmployerId.Should().BeEmpty();
        employment.CurrentSalary.Should().BeNull();
        employment.Status.Should().Be(EmploymentStatus.Unemployed);
        employment.LastJobSearchDate.Should().BeNull();
        employment.IsActivelyJobSeeking.Should().BeTrue();
    }

    [Fact]
    public void Employment_ShouldAllowEmptySkills()
    {
        var employment = TestDataBuilder.CreateEmployment([], 0, Guid.Empty);
        employment.Skills.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(40)]
    public void Employment_ShouldAcceptValidYearsOfExperience(int years)
    {
        var employment = TestDataBuilder.CreateEmployment([], years, Guid.Empty);
        employment.YearsOfExperience.Should().Be(years);
    }

    [Fact]
    public void Employment_ShouldAllowNullOptionalFields()
    {
        var employment = TestDataBuilder.CreateEmployment(
            [],
            0,
            Guid.Empty,
            currentSalary: null,
            lastJobSearchDate: null);

        employment.CurrentEmployerId.Should().BeEmpty();
        employment.CurrentSalary.Should().BeNull();
        employment.LastJobSearchDate.Should().BeNull();
    }
}