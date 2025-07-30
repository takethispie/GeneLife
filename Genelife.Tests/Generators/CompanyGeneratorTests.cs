using FluentAssertions;
using Genelife.Domain;
using Genelife.Domain.Generators;
using Genelife.Domain.Work;

namespace Genelife.Tests.Generators;

public class CompanyGeneratorTests
{
    [Theory]
    [InlineData(CompanyType.Technology)]
    [InlineData(CompanyType.Manufacturing)]
    [InlineData(CompanyType.Services)]
    [InlineData(CompanyType.Retail)]
    [InlineData(CompanyType.Healthcare)]
    public void Generate_ShouldCreateCompanyWithSpecificType(CompanyType type)
    {
        // Act
        var company = CompanyGenerator.Generate(type);

        // Assert
        company.Type.Should().Be(type);
        company.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Generate_ShouldCreateTechnologyCompanyWithExpectedCharacteristics()
    {
        // Act
        var company = CompanyGenerator.Generate(CompanyType.Technology);

        // Assert
        company.Type.Should().Be(CompanyType.Technology);
        company.Revenue.Should().BeInRange(50000, 200000);
        company.MinEmployees.Should().BeInRange(3, 8);
    }

    [Fact]
    public void Generate_ShouldCreateHealthcareCompanyWithExpectedCharacteristics()
    {
        // Act
        var company = CompanyGenerator.Generate(CompanyType.Healthcare);

        // Assert
        company.Type.Should().Be(CompanyType.Healthcare);
        company.Revenue.Should().BeInRange(60000, 250000);
        company.MinEmployees.Should().BeInRange(3, 8);
    }
}
