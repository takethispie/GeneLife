using FluentAssertions;
using Genelife.UnitTests.TestData;

namespace Genelife.UnitTests;

public class EmployeeTests
{
    [Fact]
    public void Employee_ShouldCreateWithDefaultProductivityScore()
    {
        var employee = TestDataBuilder.CreateEmployee();
        employee.ProductivityScore.Should().BeGreaterThan(0f);
    }
}
