using FluentAssertions;
using Genelife.Domain;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class CompanyTests
{

    [Fact]
    public void Company_ShouldCreateWithDefaultEmployeeConstraints()
    {
        var company = TestDataBuilder.CreateCompany();
        company.MinEmployees.Should().BeGreaterThan(0);
        company.MaxEmployees.Should().BeGreaterThan(company.MinEmployees);
    }
}