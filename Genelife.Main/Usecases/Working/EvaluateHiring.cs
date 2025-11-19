using Genelife.Domain.Work;

namespace Genelife.Main.Usecases.Working;

public class EvaluateHiring
{
    public int Execute(Company company) {
        
        var activeEmployeesCount = company.EmployeeIds.Count;
        if (activeEmployeesCount < company.MinEmployees)
            return company.MinEmployees - activeEmployeesCount;
        
        if (company.AverageProductivity < 0.7 && activeEmployeesCount < company.MaxEmployees)
        {
            var canAffordEmployees = (int)(company.Revenue / 5000);
            var maxNewHires = Math.Min(canAffordEmployees, company.MaxEmployees - activeEmployeesCount);
            if (maxNewHires > 0)
                return Math.Min(maxNewHires, 3);
        }
        
        if (company.Revenue > 50000 && activeEmployeesCount < company.MaxEmployees)
        {
            var revenueBasedHires = (int)(company.Revenue / 25000);
            var maxNewHires = Math.Min(revenueBasedHires, company.MaxEmployees - activeEmployeesCount);
            
            if (maxNewHires > 0)
                return Math.Min(maxNewHires, 2);
        }
        return 0;
    }
}