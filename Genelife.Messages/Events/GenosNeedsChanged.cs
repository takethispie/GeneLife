namespace Genelife.Messages.Events;

public record GenosNeedsChanged(ulong NetworkId, float Hunger, float Energy, float Hygiene, float Bladder);