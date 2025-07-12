using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Generators;

namespace Genelife.Tests.Generators;

public class HumanGeneratorTests
{
    [Fact]
    public void Build_ShouldCreateHumanWithDefaultAge()
    {
        // Act
        var human = HumanGenerator.Build(Sex.Female);

        // Assert
        var expectedBirthYear = DateTime.Now.Year - 18;
        human.Birthday.Year.Should().Be(expectedBirthYear);
    }
}
