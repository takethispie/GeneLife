namespace Genelife.Messages.Events;

public record TickCompleted(long Tick, int EntityCount, double DurationMs);