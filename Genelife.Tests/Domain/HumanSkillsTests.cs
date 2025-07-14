using FluentAssertions;
using Genelife.Domain;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class HumanSkillsTests
{
    [Fact]
    public void HumanSkills_ShouldAllowEmptySkillLists()
    {
        // Arrange & Act
        var humanSkills = TestDataBuilder.CreateHumanSkills(
            technicalSkills: new List<string>(),
            softSkills: new List<string>());

        // Assert
        humanSkills.TechnicalSkills.Should().BeEmpty();
        humanSkills.SoftSkills.Should().BeEmpty();
    }
}