namespace Genelife.Domain.Work;

public class Company(
    Guid id,
    string name,
    float revenue,
    float taxRate,
    CompanyType type
)
{
    public Guid Id { get; init; } = id;
    public string Name { get; init; } = name;
    public List<Employee.Employee> Employees { get; private set; } = [];
    public CompanyType Type { get; private set; } = type;
    public float AverageProductivity { get; private set; } = 1.0F;
    public float Revenue => accountingDepartment.Revenue;
    
    private readonly Accounting.Accounting accountingDepartment = new(revenue, taxRate);

    public void AddEmployee(Employee.Employee employee)
    {
        if (Employees.Contains(employee)) return;
        Employees.Add(employee);
    }
    
    public void RemoveEmployee(Employee.Employee employee) 
        => Employees = Employees.Where(e => e.Id != employee.Id).ToList();

    public void CalculatePayroll()
    {
        var salaryPayments = accountingDepartment.CalculatePayroll(this);
    }

    public void CalculateProductivity()
    {
        AverageProductivity = accountingDepartment.CalculateProductivity(this);
    }
}