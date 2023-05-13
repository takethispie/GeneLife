using Arch.Core.Extensions;
using FluentAssertions;
using GeneLife.Entities.Generators;
using GeneLife.Genetic;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Oracle.Services;


namespace GeneLife.Tests.Services;

public class RelationServiceTests
{
    [Fact]
    public void ShouldComputeAttractiveness()
    {
        var world = Arch.Core.World.Create();
        var male = PersonGenerator.CreatePure(world, Sex.Male, 20);
        var female = PersonGenerator.CreatePure(world, Sex.Female, 20);
        var result = RelationService.ComputeAttractivenessChances(male.Get<Genome>(), female.Get<Genome>());
        result.Should().NotBe(null);
    }
}
