namespace Genelife.Messages.Commands;

public record CreateJobPosting(ulong CompanyId, string Position, float Salary, List<string> Requirements);