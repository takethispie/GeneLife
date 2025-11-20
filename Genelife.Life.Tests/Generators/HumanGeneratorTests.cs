using FluentAssertions;
using Genelife.Domain;
using Genelife.Life.Generators;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Tests.Generators;

public class HumanGeneratorTests
{
    [Fact]
    public void Build_ShouldCreateHumanWithDefaultAge()
    {
        var human = HumanGenerator.Build(Sex.Female);
        var expectedBirthYear = DateTime.Now.Year - 18;
        human.Birthday.Year.Should().Be(expectedBirthYear);
    }
}
