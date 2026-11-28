using Genelife.Api.Commands;

namespace Genelife.Api.Services;

/// <summary>
/// The single precondition evaluation behind both the action catalogue and the applying path
/// (GDD chapter 08 §8.5(1); chapter 07 §7.6 AC-4a). Produced by
/// <see cref="PlayerActionService.Evaluate"/>: pure with respect to world state — it reads the world,
/// mutates nothing, consumes no randomness — so the tick drain and the catalogue read model can call
/// the same rule source without either of them drifting.
///
/// <para><see cref="Plan"/> carries the resolved facts (entities, amounts, expiry) the apply phase
/// needs so the apply path does not have to re-derive them, and cannot disagree with the evaluation
/// that admitted the action.</para>
/// </summary>
public readonly record struct ActionEvaluation(
    bool Allowed,
    string? RefusalCode,
    string Message,
    long CostCents,
    long DurationTicks,
    string? TargetHint,
    object? Plan)
{
    public static ActionEvaluation Allow(
        long costCents = 0,
        long durationTicks = 0,
        string? targetHint = null,
        object? plan = null) =>
        new(true, null, string.Empty, costCents, durationTicks, targetHint, plan);

    /// <summary>Refuses with a member of the closed player-action refusal set.</summary>
    public static ActionEvaluation Refuse(
        PlayerRefusal refusal,
        string message,
        long costCents = 0,
        long durationTicks = 0,
        string? targetHint = null) =>
        new(false, refusal.WireCode(), message, costCents, durationTicks, targetHint, null);

    /// <summary>
    /// Refuses with a code produced outside the closed set (eligibility, relationship and venue
    /// results still carry raw strings, mapped consumer-side by wire string per GDD chapter 07 §7.6).
    /// </summary>
    public static ActionEvaluation RefuseRuntime(
        string? code,
        string message,
        long costCents = 0,
        long durationTicks = 0,
        string? targetHint = null) =>
        new(false, code ?? PlayerRefusal.InvalidState.WireCode(), message, costCents, durationTicks, targetHint, null);

    /// <summary>Projects a refusal back onto the applying path's result, preserving the wire string.</summary>
#pragma warning disable CS0618 // the string overload is the deliberate escape hatch for runtime codes
    public PlayerActionResult ToActionResult() =>
        PlayerRefusalVocabulary.TryParse(RefusalCode, out var refusal)
            ? PlayerActionResult.Reject(refusal, Message)
            : PlayerActionResult.Reject(RefusalCode ?? PlayerRefusal.InvalidState.WireCode(), Message);
#pragma warning restore CS0618
}