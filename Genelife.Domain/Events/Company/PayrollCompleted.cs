namespace Genelife.Domain.Events.Company;

public record PayrollCompleted(Guid CompanyId, decimal TotalPaid, decimal TaxesPaid);