namespace Genelife.Domain.Work;

public class Accounting(float revenue, float taxRate)
{
    public float Revenue { get; private set; } = revenue;
    public float TaxRate { get; private set; } = taxRate;
    
    public IEnumerable<Salary> CalculatePayroll(Company company)
    {
        var salaryPayments = new List<Salary>();
        float totalPaid = 0;
        float totalTaxes = 0;

        foreach (var employment in company.Employees.Where(e => e.Status == EmploymentStatus.Active))
        {
            var grossSalary = employment.Salary;
            var taxDeducted = grossSalary * TaxRate;
            var netSalary = grossSalary - taxDeducted;
            salaryPayments.Add(new Salary(employment.Id, netSalary, taxDeducted));
            totalPaid += netSalary;
            totalTaxes += taxDeducted;
        }

        Revenue -= totalPaid + totalTaxes;
        return salaryPayments;
    }
    
    public float CalculateProductivity(Company company)
    {
        var activeEmployees = company.Employees.Where(e => e.Status == EmploymentStatus.Active).ToList();

        if (activeEmployees.Count == 0) return 0;
        var averageProductivity = activeEmployees.Average(e => e.ProductivityScore);
        var baseRevenuePerEmployee = company.Type switch
        {
            CompanyType.Technology => 500,
            CompanyType.Manufacturing => 300,
            CompanyType.Services => 400,
            CompanyType.Retail => 250,
            CompanyType.Healthcare => 600,
            _ => 350
        };
        
        // Revenue is affected by productivity and number of employees
        var dailyRevenue = activeEmployees.Count * baseRevenuePerEmployee * averageProductivity;
        // Subtract operational costs (salaries, overhead)
        var dailyOperationalCosts = activeEmployees.Sum(e => e.Salary / 30) + (activeEmployees.Count * 50);
        Revenue += dailyRevenue - dailyOperationalCosts;
        return averageProductivity;
    }
}