using Genelife.Components.Finance;
using Genelife.Values;

namespace Genelife.Economy;

/// <summary>
/// Append-only in-memory ledger with idempotent transaction ids. The ring buffer
/// is a cache for the admin; the event stream is the durable source of truth.
/// </summary>
public sealed class FinanceLedger
{
    public const int BufferCapacity = 10_000;
    public const int IdempotencyCapacity = 100_000;

    private readonly object gate = new();
    private readonly LedgerEntry[] buffer = new LedgerEntry[BufferCapacity];
    private readonly Dictionary<Guid, LedgerEntry> seen = new(IdempotencyCapacity);
    private readonly Queue<Guid> seenOrder = new(IdempotencyCapacity);
    private int head;
    private int count;

    public long RecordedCount { get; private set; }
    public Money TotalRecorded { get; private set; }
    public Money SourceTotal { get; private set; }
    public Money SinkTotal { get; private set; }
    public Action<LedgerEntry>? Sink { get; set; }

    public LedgerEntry Record(
        Guid transactionId,
        long tick,
        LedgerKind kind,
        ulong? fromNetworkId,
        ulong? toNetworkId,
        Money amount,
        string reference,
        string? memo = null)
    {
        lock (gate)
        {
            if (seen.TryGetValue(transactionId, out var existing))
                return existing;

            var entry = new LedgerEntry(
                transactionId,
                tick,
                kind,
                fromNetworkId,
                toNetworkId,
                amount,
                reference,
                memo);

            if (count == BufferCapacity)
            {
                buffer[head] = entry;
                head = (head + 1) % BufferCapacity;
            }
            else
            {
                buffer[(head + count) % BufferCapacity] = entry;
                count++;
            }

            seen[transactionId] = entry;
            seenOrder.Enqueue(transactionId);
            if (seenOrder.Count > IdempotencyCapacity)
                seen.Remove(seenOrder.Dequeue());

            RecordedCount++;
            TotalRecorded += amount;
            if (entry.IsSource) SourceTotal += amount;
            if (entry.IsSink) SinkTotal += amount;

            Sink?.Invoke(entry);
            return entry;
        }
    }

    public IReadOnlyList<LedgerEntry> Recent(
        int take = 100,
        ulong? entityId = null,
        LedgerKind? kind = null,
        long? fromTick = null,
        long? toTick = null)
    {
        if (take <= 0)
            return Array.Empty<LedgerEntry>();

        lock (gate)
        {
            var result = new List<LedgerEntry>(Math.Min(take, count));
            for (var i = 0; i < count && result.Count < take; i++)
            {
                var index = (head + count - 1 - i) % BufferCapacity;
                if (index < 0)
                    index += BufferCapacity;

                var entry = buffer[index];
                if (entityId is not null &&
                    entry.FromNetworkId != entityId &&
                    entry.ToNetworkId != entityId)
                    continue;
                if (kind is not null && entry.Kind != kind)
                    continue;
                if (fromTick is not null && entry.Tick < fromTick)
                    continue;
                if (toTick is not null && entry.Tick > toTick)
                    continue;

                result.Add(entry);
            }
            return result;
        }
    }

    public bool Contains(Guid transactionId)
    {
        lock (gate)
            return seen.ContainsKey(transactionId);
    }

    public void Clear()
    {
        lock (gate)
        {
            Array.Clear(buffer);
            seen.Clear();
            seenOrder.Clear();
            head = 0;
            count = 0;
            RecordedCount = 0;
            TotalRecorded = Money.Zero;
            SourceTotal = Money.Zero;
            SinkTotal = Money.Zero;
        }
    }
}
