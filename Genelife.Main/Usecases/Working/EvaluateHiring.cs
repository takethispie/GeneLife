using Genelife.Domain.Work;

namespace Genelife.Main.Usecases.Working;

public class EvaluateHiring
{
    public int Execute(Company company) {
        var activeEmployeesCount = company.EmployeeIds.Count;
        var maxHire =  (activeEmployeesCount, company.Revenue, company.AverageProductivity) switch {
            (< 5, _, _) => 5 - activeEmployeesCount,
            (< 50, _, < 0.7f) or (< 50, > 50000, _)  => 
                RevenueBasedHire(company.Revenue, 50, activeEmployeesCount),
            _ => 0
        };
        return Math.Min(maxHire, 3);
    }

    private static int RevenueBasedHire(float revenue, int maxEmployees, int activeEmployees) {
        var revenueBasedHires = (int)(revenue / 25000);
        return Math.Min(revenueBasedHires, maxEmployees - activeEmployees);
    }
}