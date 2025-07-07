using Genelife.Domain;

namespace Genelife.Main.Usecases;

public class UpdateProductivity
{
    public (decimal averageProductivity, decimal revenueChange) Execute(
        Company company, 
        List<Employment> employees)
    {
        var activeEmployees = employees.Where(e => e.Status == EmploymentStatus.Active).ToList();
        
        if (activeEmployees.Count == 0)
            return (0m, 0m);
        var averageProductivity = activeEmployees.Average(e => e.ProductivityScore);
        var baseRevenuePerEmployee = company.Type switch
        {
            CompanyType.Technology => 500m,
            CompanyType.Manufacturing => 300m,
            CompanyType.Services => 400m,
            CompanyType.Retail => 250m,
            CompanyType.Healthcare => 600m,
            _ => 350m
        };
        
        // Revenue is affected by productivity and number of employees
        var dailyRevenue = activeEmployees.Count * baseRevenuePerEmployee * averageProductivity;
        // Subtract operational costs (salaries, overhead)
        var dailyOperationalCosts = activeEmployees.Sum(e => e.Salary / 30m) + (activeEmployees.Count * 50m); // 50 per employee overhead
        var revenueChange = dailyRevenue - dailyOperationalCosts;
        return (averageProductivity, revenueChange);
    }
    
    public Employment UpdateEmployeeProductivity(Employment employment, Random random)
    {
        // Simulate productivity changes based on various factors
        var productivityChange = (decimal)(random.NextDouble() * 0.4 - 0.2); // -0.2 to +0.2
        var newProductivity = Math.Max(0.1m, Math.Min(2.0m, employment.ProductivityScore + productivityChange));
        return employment with { ProductivityScore = newProductivity };
    }
}