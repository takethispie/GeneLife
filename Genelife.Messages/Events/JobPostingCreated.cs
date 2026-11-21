namespace Genelife.Messages.Events;

public record JobPostingCreated(ulong CompanyId, ulong PostingId, string Position, float Salary, int OpenSeats);