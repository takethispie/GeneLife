using Genelife.Domain.Work.Accounting;

namespace Genelife.Domain.Work;

public class Company(
    Guid id,
    string name,
    AccountingDepartment accounting,
    CompanyType type
)
{
    public Guid Id { get; init; } = id;
    public string Name { get; init; } = name;
    public List<Employee.Employee> Employees { get; private set; } = [];
    public CompanyType Type { get; private set; } = type;
    public float AverageProductivity { get; private set; } = 1.0F;
    public AccountingDepartment Accounting { get; init; } = accounting;

    public void AddEmployee(Employee.Employee employee)
    {
        if (Employees.Contains(employee)) return;
        Employees.Add(employee);
    }
    
    public void RemoveEmployee(Employee.Employee employee) 
        => Employees = Employees.Where(e => e.Id != employee.Id).ToList();

    public IEnumerable<Salary> CalculatePayroll() => Accounting.CalculatePayroll(this);

    public void CalculateProductivity()
    {
        AverageProductivity = Accounting.CalculateProductivity(this);
    }
    
    public int GetHiringPotential() {
        var activeEmployeesCount = Employees.Count;
        var maxHire =  (activeEmployeesCount, Accounting.Revenue, AverageProductivity) switch {
            (< 5, _, _) => 5 - activeEmployeesCount,
            (< 50, _, < 0.7f) or (< 50, > 50000, _)  => 
                RevenueBasedHire(Accounting.Revenue, 50, activeEmployeesCount),
            _ => 0
        };
        return Math.Min(maxHire, 3);
    }

    private static int RevenueBasedHire(float revenue, int maxEmployees, int activeEmployees) {
        var revenueBasedHires = (int)(revenue / 25000);
        return Math.Min(revenueBasedHires, maxEmployees - activeEmployees);
    }
}