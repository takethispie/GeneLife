namespace Genelife.Messages.Events;

public record SimNeedsChanged(ulong NetworkId, float Hunger, float Energy, float Hygiene, float Bladder);