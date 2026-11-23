namespace Genelife.Api.DTOs;

/// <summary>
/// Asynchronous feedback for an intent that passed admission in
/// <c>SubmitIntent</c> but was later rejected while the simulation applied it.
/// The client should surface <see cref="Code"/> and keep its previous state; the
/// authoritative world snapshot is the only source of movement.
/// </summary>
public sealed class IntentRejected
{
    public string PlayerId { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
