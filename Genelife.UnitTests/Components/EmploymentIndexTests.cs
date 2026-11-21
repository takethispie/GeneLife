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

    [Fact]
    public void RemoveByCompany_RemovesAllEmployeesUnderCompany()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var company = world.Create();
        var e1 = world.Create();
        var e2 = world.Create();

        index.Add(company, e1);
        index.Add(company, e2);

        index.RemoveByCompany(company);

        Assert.Equal(0, index.CountEmployees(company));
        Assert.Equal(0, index.TotalEmployees());
        Assert.Empty(index.CompanyByEmployee);
        Assert.Empty(index.EmployeesByCompany);
    }

    [Fact]
    public void RemoveByCompany_UnknownCompany_DoesNothing()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        index.RemoveByCompany(world.Create());
        Assert.Equal(0, index.TotalEmployees());
    }

    [Fact]
    public void TotalEmployees_MatchesCompanyByEmployeeCount()
    {
        var world = World.Create();
        var index = new EmploymentIndex();
        var company = world.Create();
        var e1 = world.Create();
        var e2 = world.Create();

        Assert.Equal(0, index.TotalEmployees());
        index.Add(company, e1);
        Assert.Equal(1, index.TotalEmployees());
        index.Add(company, e2);
        Assert.Equal(2, index.TotalEmployees());
    }
}