namespace Genelife.Messages.Events;

public record JobPostingCreated(ulong CompanyId, ulong PostingId, string Position, long SalaryCents, int OpenSeats);
