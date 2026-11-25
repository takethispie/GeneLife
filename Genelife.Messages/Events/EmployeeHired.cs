namespace Genelife.Messages.Events;

public record EmployeeHired(ulong CompanyId, ulong GenosId, string Position, long SalaryCents);
