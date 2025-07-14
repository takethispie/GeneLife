using Genelife.Domain;
using Genelife.Domain.Work;

namespace Genelife.Main.Usecases;

public class EvaluateHiring
{
    public (bool shouldHire, int positionsNeeded) Execute(Company company, List<Employee> employees, decimal averageProductivity)
    {
        var activeEmployees = employees.Count(e => e.Status == EmploymentStatus.Active);
        
        // Check if below minimum employees
        if (activeEmployees < company.MinEmployees)
            return (true, company.MinEmployees - activeEmployees);
        
        // Check if productivity is low and we can afford more employees
        if (averageProductivity < 0.7m && activeEmployees < company.MaxEmployees)
        {
            var canAffordEmployees = (int)(company.Revenue / 5000m); // Assume 5000 per employee cost
            var maxNewHires = Math.Min(canAffordEmployees, company.MaxEmployees - activeEmployees);
            if (maxNewHires > 0)
                return (true, Math.Min(maxNewHires, 3)); // Max 3 new hires at once
        }
        
        // Check if revenue is high and we're under capacity
        if (company.Revenue > 50000m && activeEmployees < company.MaxEmployees)
        {
            var revenueBasedHires = (int)(company.Revenue / 25000m); // 1 hire per 25k revenue
            var maxNewHires = Math.Min(revenueBasedHires, company.MaxEmployees - activeEmployees);
            
            if (maxNewHires > 0)
                return (true, Math.Min(maxNewHires, 2)); // Max 2 revenue-based hires
        }
        
        return (false, 0);
    }
}