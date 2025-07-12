using Genelife.Domain;
using Genelife.Domain.Events.Company;

namespace Genelife.Main.Usecases;

public class CalculatePayroll
{
    public (decimal totalPaid, decimal totalTaxes, List<SalaryPaid> salaryPayments) Execute(
        Company company, 
        List<Employee> employees)
    {
        var salaryPayments = new List<SalaryPaid>();
        decimal totalPaid = 0;
        decimal totalTaxes = 0;

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