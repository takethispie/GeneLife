namespace Genelife.Messages.Events;

public record ApplicationSubmitted(ulong PostingId, ulong SimId, string SimName);