namespace Genelife.Messages.Events;

public record GenosActionStarted(ulong NetworkId, string ActionType, float Duration);