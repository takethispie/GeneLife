using FluentAssertions;
using Genelife.Tests.TestData;

namespace Genelife.Tests.Domain;

public class EmployeeTests
{
    [Fact]
    public void Employee_ShouldCreateWithDefaultProductivityScore()
    {
        // Arrange & Act
        var employee = TestDataBuilder.CreateEmployee();

        // Assert
        employee.ProductivityScore.Should().BeGreaterThan(0f);
    }
}
