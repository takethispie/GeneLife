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
    }
}