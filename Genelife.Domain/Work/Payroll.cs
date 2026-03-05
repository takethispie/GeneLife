namespace Genelife.Domain.Work;

public record Payroll(float TotalRevenue, float  TotalTaxes, IEnumerable<Salary> Salaries);