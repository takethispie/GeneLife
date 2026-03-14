namespace Genelife.Domain.Work.Accounting;

public record Payroll(float TotalRevenue, float  TotalTaxes, IEnumerable<Salary> Salaries);