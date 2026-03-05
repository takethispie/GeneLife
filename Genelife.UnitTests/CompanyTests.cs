using Genelife.UnitTests.TestData;

namespace Genelife.UnitTests;

public class CompanyTests
{

    [Fact]
    public void Company_ShouldCreateWithDefaultEmployeeConstraints()
    {
        var company = TestDataBuilder.CreateCompany();
    }
}