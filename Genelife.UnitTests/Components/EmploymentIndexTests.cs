using Arch.Core;
using Genelife.Components.Employment;

namespace Genelife.UnitTests.Components;

public class EmploymentIndexTests
{
    [Fact]
    public void Add_RegistersEmployeeUnderCompany()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var company = world.Create();
        var employee = world.Create();

        index.Add(company, employee);

        Assert.Equal(1, index.CountEmployees(company));
        Assert.Equal(company, index.CompanyByEmployee[employee]);
    }

    [Fact]
    public void Add_MultipleEmployees_CountsAll()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var company = world.Create();
        var e1 = world.Create();
        var e2 = world.Create();

        index.Add(company, e1);
        index.Add(company, e2);

        Assert.Equal(2, index.CountEmployees(company));
    }

    [Fact]
    public void CountEmployees_UnknownCompany_ReturnsZero()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        Assert.Equal(0, index.CountEmployees(world.Create()));
    }

    [Fact]
    public void Remove_RemovesEmployeeFromCompany()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var company = world.Create();
        var employee = world.Create();

        index.Add(company, employee);
        index.Remove(employee);

        Assert.Equal(0, index.CountEmployees(company));
        Assert.False(index.CompanyByEmployee.ContainsKey(employee));
    }

    [Fact]
    public void Remove_UnknownEmployee_DoesNothing()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        index.Remove(world.Create());
        Assert.Empty(index.CompanyByEmployee);
    }
}