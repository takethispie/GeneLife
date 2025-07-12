using FluentAssertions;
using Genelife.Domain;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class CompanyTests
{

    [Fact]
    public void Company_ShouldCreateWithDefaultEmployeeConstraints()
    {
        // Arrange & Act
        var company = TestDataBuilder.CreateCompany();

        // Assert
        company.MinEmployees.Should().BeGreaterThan(0);
        company.MaxEmployees.Should().BeGreaterThan(company.MinEmployees);
    }
}