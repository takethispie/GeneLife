namespace Genelife.Messages.Events;

public record SimActionStarted(ulong NetworkId, string ActionType, float Duration);