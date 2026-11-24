using System.Text.Json;
using System.Text.Json.Serialization;

namespace Genelife.Api.Commands;

/// <summary>
/// Stable player action codes shared by the browser, the hub and the HTTP
/// admission endpoint. Values are persisted in command results and must not be
/// renumbered.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayerActionKind : byte
{
    ApplyForJob = 1,
    AcceptJobOffer = 2,
    QuitJob = 3,
    RequestHousing = 4,
    AcceptLease = 5,
    MoveOut = 6,
    PayArrears = 7,
    BuyItem = 8,
    ConsumeItem = 9,
    InvitePartner = 10,
    RespondToInvitation = 11,
    TryForChild = 12,
    NameChild = 13,
    ChooseContinuation = 14,
    InviteCohabit = 15,
    EndPartnership = 16,
    PerformVenueActivity = 17
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayerCommandStatus : byte
{
    Accepted = 1,
    Rejected = 2,
    Applied = 3,
    Superseded = 4
}

/// <summary>
/// Transport-neutral player command envelope. The payload is typed per action
/// by <see cref="PlayerActionPayloads"/>; the client never sends authoritative
/// balances, positions or family state.
/// </summary>
public sealed record PlayerActionIntent : Intent
{
    public ulong SimNetworkId { get; init; }
    public PlayerActionKind Kind { get; init; }
    public JsonElement Payload { get; init; }
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public long SubmittedTick { get; init; }
}

public sealed record PlayerCommandResult(
    Guid CommandId,
    string PlayerId,
    PlayerActionKind Kind,
    PlayerCommandStatus Status,
    string? Code,
    string? Message,
    long AppliedTick);

/// <summary>
/// Typed payload shapes for every player action. Unknown JSON members are
/// ignored; missing required members are reported as <c>malformed_payload</c>.
/// </summary>
public static class PlayerActionPayloads
{
    public sealed record ApplyForJob(ulong PostingNetworkId, ulong OfficeNetworkId);
    public sealed record AcceptJobOffer(Guid OfferId);
    public sealed record QuitJob(string? Reason);
    public sealed record RequestHousing(ulong UnitNetworkId, ulong PropertyNetworkId);
    public sealed record AcceptLease(Guid OfferId);
    public sealed record MoveOut(ulong DestinationUnitNetworkId);
    public sealed record PayArrears(long AmountCents, bool RentOnly);
    public sealed record BuyItem(ulong StoreNetworkId, string CatalogItemId, int Quantity);
    public sealed record ConsumeItem(string CatalogItemId, int Quantity);
    public sealed record InvitePartner(ulong TargetSimNetworkId, string? TargetPlayerId = null);
    public sealed record RespondToInvitation(Guid InvitationId, bool Accept);
    public sealed record TryForChild(ulong PartnerSimNetworkId);
    public sealed record NameChild(ulong ChildNetworkId, string Name);
    public sealed record ChooseContinuation(string Policy);
    public sealed record InviteCohabit(string TargetPlayerId);
    public sealed record EndPartnership(string PartnerPlayerId, bool SplitHousehold = true);
    public sealed record PerformVenueActivity(string Activity);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool TryParse<T>(JsonElement payload, out T? value, out string? error)
        where T : class
    {
        value = null;
        error = null;

        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            error = "A payload object is required.";
            return false;
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            error = "The payload must be a JSON object.";
            return false;
        }

        try
        {
            value = payload.Deserialize<T>(JsonOptions);
        }
        catch (JsonException ex)
        {
            error = $"Malformed payload: {ex.Message}";
            return false;
        }

        if (value is null)
        {
            error = "The payload could not be read.";
            return false;
        }

        return true;
    }
}
