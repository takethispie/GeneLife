using Genelife.Values;

namespace Genelife.Components.Finance;

/// <summary>
/// Immutable record of one money movement. A transfer has both a payer and a
/// payee; sources have no payer and sinks have no payee.
/// </summary>
public readonly record struct LedgerEntry(
    Guid TransactionId,
    long Tick,
    LedgerKind Kind,
    ulong? FromNetworkId,
    ulong? ToNetworkId,
    Money Amount,
    string Reference,
    string? Memo)
{
    public bool IsSource => FromNetworkId is null;
    public bool IsSink => ToNetworkId is null;
}
