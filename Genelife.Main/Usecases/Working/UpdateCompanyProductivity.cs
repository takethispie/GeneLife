using Genelife.Domain.Work;

namespace Genelife.Main.Usecases.Working;

public class UpdateCompanyProductivity
{
    public (float averageProductivity, float revenueChange) Execute(
        Company company, 
        List<Employee> employees)
    {
        var activeEmployees = employees.Where(e => e.Status == EmploymentStatus.Active).ToList();
        
        if (activeEmployees.Count == 0)
            return (0, 0);
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
        var dailyOperationalCosts = activeEmployees.Sum(e => e.Salary / 30) + (activeEmployees.Count * 50); // 50 per employee overhead
        var revenueChange = dailyRevenue - dailyOperationalCosts;
        return (averageProductivity, revenueChange);
    }
}