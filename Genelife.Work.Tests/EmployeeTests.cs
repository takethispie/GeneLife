using FluentAssertions;
using Xunit;

namespace Genelife.Work.Tests;

public class EmployeeTests
{
    [Fact]
    public void Employee_ShouldCreateWithDefaultProductivityScore()
    {
        var employee = TestDataBuilder.CreateEmployee();
        employee.ProductivityScore.Should().BeGreaterThan(0f);
    }
}
