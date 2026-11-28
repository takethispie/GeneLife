using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genelife.Api.Commands;

/// <summary>
/// Versioning for the action catalogue envelope (GDD chapter 08 §8.5(1)). The shape version moves
/// when the <see cref="ActionCatalogue"/> or <see cref="ActionCatalogueEntry"/> shape changes; the
/// refusal enum version is derived from <see cref="PlayerRefusalVocabulary"/> so a dial that keys its
/// wording off a refusalCode can tell when the server's closed set moved underneath it.
/// </summary>
public static class ActionCatalogueVocabulary
{
    /// <summary>The wire shape of the catalogue envelope. Bump only with a client change.</summary>
    public const int ShapeVersion = 1;

    /// <summary>Mirror of the closed refusal set's derived version.</summary>
    public static string RefusalEnumVersion => PlayerRefusalVocabulary.RefusalEnumVersion;
}

/// <summary>
/// One dial verb in one context. <see cref="Available"/> is a rendering convenience; the actionable
/// half is <see cref="RefusalCode"/>: the dial and the dollhouse render from the code and must never
/// answer an unavailable verb with a bare boolean (GDD chapter 07 §7.6, AC-4a).
/// </summary>
public sealed record ActionCatalogueEntry(
    PlayerActionKind Kind,
    bool Available,
    string? RefusalCode,
    long CostCents,
    long DurationTicks,
    string? TargetHint,
    long AsOfTick);

/// <summary>
/// The catalogue read model for one player in one context. Entries are produced by the SAME
/// <c>PlayerActionService.Evaluate</c> the tick uses; the client never derives its own action list.
/// </summary>
public sealed record ActionCatalogue(
    string PlayerId,
    ulong GenosNetworkId,
    int ShapeVersion,
    string RefusalEnumVersion,
    long AsOfTick,
    IReadOnlyList<ActionCatalogueEntry> Entries);

/// <summary>
/// A candidate target for one kind, supplied by the caller as context. The payload has the same shape
/// the client would submit to <c>/api/player/actions</c>; the server evaluates it and decides.
/// </summary>
public sealed record ActionCatalogueTarget(
    PlayerActionKind Kind,
    JsonElement Payload);

/// <summary>
/// Request context for the catalogue read. Targets are optional: a kind without one is evaluated
/// against the typed empty payload for its kind, so an absent target reads as a refusal with a reason
/// rather than as a silent withholding.
/// </summary>
public sealed record ActionCatalogueRequest(
    ulong GenosNetworkId,
    long? AsOfTick = null,
    IReadOnlyList<ActionCatalogueTarget>? Targets = null);

/// <summary>
/// The typed empty payload for a kind, used when the catalogue has no caller-supplied target. Every
/// payload shape has a well-formed zero value, so a missing target surfaces as the domain's own
/// refusal (not_available, target_required, …) instead of a malformed_payload from an absent body.
/// </summary>
public static class ActionCataloguePayloads
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static JsonElement Empty(PlayerActionKind kind)
    {
        object? value = kind switch
        {
            PlayerActionKind.ApplyForJob => new PlayerActionPayloads.ApplyForJob(0, 0),
            PlayerActionKind.AcceptJobOffer => new PlayerActionPayloads.AcceptJobOffer(Guid.Empty),
            PlayerActionKind.QuitJob => new PlayerActionPayloads.QuitJob(null),
            PlayerActionKind.RequestHousing => new PlayerActionPayloads.RequestHousing(0, 0),
            PlayerActionKind.AcceptLease => new PlayerActionPayloads.AcceptLease(Guid.Empty),
            PlayerActionKind.MoveOut => new PlayerActionPayloads.MoveOut(0),
            PlayerActionKind.PayArrears => new PlayerActionPayloads.PayArrears(0, false),
            PlayerActionKind.BuyItem => new PlayerActionPayloads.BuyItem(0, string.Empty, 1),
            PlayerActionKind.ConsumeItem => new PlayerActionPayloads.ConsumeItem(string.Empty, 1),
            PlayerActionKind.InvitePartner => new PlayerActionPayloads.InvitePartner(0, null),
            PlayerActionKind.RespondToInvitation => new PlayerActionPayloads.RespondToInvitation(Guid.Empty, false),
            PlayerActionKind.TryForChild => new PlayerActionPayloads.TryForChild(0),
            PlayerActionKind.NameChild => new PlayerActionPayloads.NameChild(0, string.Empty),
            PlayerActionKind.ChooseContinuation => new PlayerActionPayloads.ChooseContinuation(string.Empty),
            PlayerActionKind.InviteCohabit => new PlayerActionPayloads.InviteCohabit(string.Empty),
            PlayerActionKind.EndPartnership => new PlayerActionPayloads.EndPartnership(string.Empty, true),
            PlayerActionKind.PerformVenueActivity => new PlayerActionPayloads.PerformVenueActivity(string.Empty),
            _ => null
        };

        return value is null
            ? JsonSerializer.SerializeToElement(new { }, JsonOptions)
            : JsonSerializer.SerializeToElement(value, value.GetType(), JsonOptions);
    }
}