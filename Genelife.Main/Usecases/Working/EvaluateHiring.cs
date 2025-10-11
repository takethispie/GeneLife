using Genelife.Domain.Work;

namespace Genelife.Main.Usecases.Working;

public class EvaluateHiring
{
    public int Execute(Company company, List<Employee> employees, float averageProductivity)
    {
        var activeEmployees = employees.Count(e => e.Status == EmploymentStatus.Active);
        
        if (activeEmployees < company.MinEmployees)
            return company.MinEmployees - activeEmployees;
        
        if (averageProductivity < 0.7 && activeEmployees < company.MaxEmployees)
        {
            var canAffordEmployees = (int)(company.Revenue / 5000);
            var maxNewHires = Math.Min(canAffordEmployees, company.MaxEmployees - activeEmployees);
            if (maxNewHires > 0)
                return Math.Min(maxNewHires, 3);
        }
        
        if (company.Revenue > 50000 && activeEmployees < company.MaxEmployees)
        {
            var revenueBasedHires = (int)(company.Revenue / 25000);
            var maxNewHires = Math.Min(revenueBasedHires, company.MaxEmployees - activeEmployees);
            
            if (maxNewHires > 0)
                return Math.Min(maxNewHires, 2);
        }
        
        return 0;
    }
}