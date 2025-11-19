using Genelife.Domain.Events.Company;
using Genelife.Domain.Work;

namespace Genelife.Main.Usecases.Working;

public class CalculatePayroll
{
    public (float totalPaid, float totalTaxes, List<SalaryPaid> salaryPayments) Execute(
        Company company, 
        List<Employee> employees)
    {
        var salaryPayments = new List<SalaryPaid>();
        float totalPaid = 0;
        float totalTaxes = 0;

        foreach (var employment in employees.Where(e => e.Status == EmploymentStatus.Active))
        {
            var grossSalary = employment.Salary;
            var taxDeducted = grossSalary * company.TaxRate;
            var netSalary = grossSalary - taxDeducted;
            salaryPayments.Add(new SalaryPaid(employment.HumanId, netSalary, taxDeducted));
            totalPaid += netSalary;
            totalTaxes += taxDeducted;
        }

        return (totalPaid, totalTaxes, salaryPayments);
    }
}