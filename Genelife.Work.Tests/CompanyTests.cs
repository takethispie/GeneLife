using Xunit;

namespace Genelife.Work.Tests;

public class CompanyTests
{

    [Fact]
    public void Company_ShouldCreateWithDefaultEmployeeConstraints()
    {
        var company = TestDataBuilder.CreateCompany();
    }
}