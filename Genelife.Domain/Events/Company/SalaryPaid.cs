namespace Genelife.Domain.Events.Company;

public record SalaryPaid(Guid HumanId, decimal Amount, decimal TaxDeducted);