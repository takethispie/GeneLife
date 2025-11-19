using FluentAssertions;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class EmployeeTests
{
    [Fact]
    public void Employee_ShouldCreateWithDefaultProductivityScore()
    {
        var employee = TestDataBuilder.CreateEmployee();
        employee.ProductivityScore.Should().BeGreaterThan(0f);
    }
}
