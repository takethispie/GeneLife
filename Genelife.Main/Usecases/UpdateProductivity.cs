using Genelife.Domain;
using Genelife.Domain.Work;

namespace Genelife.Main.Usecases;

public class UpdateProductivity
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
    
    public Employee UpdateEmployeeProductivity(Employee employment, Random random)
    {
        // Simulate productivity changes based on various factors
        var productivityChange = random.NextSingle() * 0.4 - 0.2; // -0.2 to +0.2
        var newProductivity = Math.Max(0.1f, Math.Min(2.0f, employment.ProductivityScore + productivityChange));
        return employment with { ProductivityScore = Convert.ToSingle(newProductivity) };
    }
}