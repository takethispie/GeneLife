using System.Security.Cryptography;
using System.Text;

namespace Genelife.Api.Commands;

/// <summary>
/// The canonical wire string a <see cref="PlayerRefusal"/> member carries. The wire is a string
/// (<c>PlayerCommandResult.Code</c>), so the string is the durable half of the member: renaming the
/// member is free, changing this string breaks the shipped client.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class RefusalCodeAttribute : Attribute
{
    public RefusalCodeAttribute(string code) => Code = code;

    /// <summary>The canonical wire string, byte-identical to the literal it replaced.</summary>
    public string Code { get; }
}

/// <summary>
/// The closed refusal vocabulary of the PLAYER-ACTION PATH ONLY (GDD chapter 08 §8.5(1);
/// chapter 07 §7.6 AC-4a): every code the player-action path can refuse with —
/// <c>PlayerActionService</c> and <c>PlayerCommandService</c> in <c>Genelife.Application</c>.
///
/// <para>Scope is deliberate. Codes produced outside the action path — the movement engine's
/// (<c>SimulationEngine.cs</c>), the chat/social/moderation surfaces (<c>ChatSendResult</c>,
/// <c>EmoteSendResult</c>) — are NOT members here; they are mapped consumer-side by wire string in
/// <c>GDD/mockups/assets/refusal-reasons.json</c>. Widening this enum to cover them would make the
/// design-side mapping a blanket assertion instead of a per-surface one.</para>
///
/// <para>The enum is the internal compile-time source; the wire stays a string. Each member carries a
/// <see cref="RefusalCodeAttribute"/> whose value is the canonical string, so a member and its wire
/// string can only move together when <see cref="PlayerRefusalVocabulary.RefusalEnumVersion"/> is
/// allowed to move with them.</para>
///
/// <para>Values are explicit and must never be renumbered: a member inserts with the next free value
/// rather than shifting the ones after it. Nothing on the wire depends on the number — the canonical
/// string does — so renumbering is a silent trap for anything that persists the byte.</para>
/// </summary>
public enum PlayerRefusal : byte
{
    [RefusalCode("genos_not_found")] GenosNotFound = 1,
    [RefusalCode("not_owner")] NotOwner = 2,
    [RefusalCode("not_available")] NotAvailable = 3,
    [RefusalCode("out_of_range")] OutOfRange = 4,
    [RefusalCode("already_employed")] AlreadyEmployed = 5,
    [RefusalCode("invalid_state")] InvalidState = 6,
    [RefusalCode("offer_not_found")] OfferNotFound = 7,
    [RefusalCode("offer_expired")] OfferExpired = 8,
    [RefusalCode("not_employed")] NotEmployed = 9,
    [RefusalCode("insufficient_funds")] InsufficientFunds = 10,
    [RefusalCode("no_household")] NoHousehold = 11,
    [RefusalCode("out_of_stock")] OutOfStock = 12,
    [RefusalCode("pantry_empty")] PantryEmpty = 13,
    [RefusalCode("target_not_found")] TargetNotFound = 14,
    [RefusalCode("target_required")] TargetRequired = 15,
    [RefusalCode("self_target")] SelfTarget = 16,
    [RefusalCode("invalid_activity")] InvalidActivity = 17,
    [RefusalCode("not_in_venue")] NotInVenue = 18,
    [RefusalCode("declined")] Declined = 19,
    [RefusalCode("not_target")] NotTarget = 20,
    [RefusalCode("not_partner")] NotPartner = 21,
    [RefusalCode("not_conceived")] NotConceived = 22,
    [RefusalCode("malformed_payload")] MalformedPayload = 23,
    [RefusalCode("world_not_ready")] WorldNotReady = 24,
    [RefusalCode("rate_limited")] RateLimited = 25,
    [RefusalCode("unauthenticated")] Unauthenticated = 26,
    [RefusalCode("queue_full")] QueueFull = 27,
    [RefusalCode("payload_too_large")] PayloadTooLarge = 28
}

/// <summary>
/// The vocabulary around <see cref="PlayerRefusal"/>: the canonical strings, the closed set itself
/// and the version that moves when the set moves.
/// </summary>
public static class PlayerRefusalVocabulary
{
    private static readonly IReadOnlyDictionary<PlayerRefusal, string> WireCodes = BuildWireCodes();
    private static readonly IReadOnlyDictionary<string, PlayerRefusal> ByCode = WireCodes
        .ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>Every member of the closed set, in value order. Never hand-counted.</summary>
    public static IReadOnlyList<PlayerRefusal> All { get; } = Enum
        .GetValues<PlayerRefusal>()
        .OrderBy(value => (byte)value)
        .ToArray();

    /// <summary>
    /// The canonical wire string, byte-identical to the literal this member replaced. This is what
    /// goes into <c>PlayerCommandResult.Code</c> and what the client keys its wording and its
    /// notification cue off, so it must not change without a client change.
    /// </summary>
    public static string WireCode(this PlayerRefusal refusal) =>
        WireCodes.TryGetValue(refusal, out var code)
            ? code
            : throw new ArgumentOutOfRangeException(
                nameof(refusal), refusal, "Not a member of the closed player-action refusal set.");

    /// <summary>Resolves a wire string back to its member; false for a code outside the closed set.</summary>
    public static bool TryParse(string? code, out PlayerRefusal refusal)
    {
        if (code is not null)
            return ByCode.TryGetValue(code, out refusal);

        refusal = default;
        return false;
    }

    /// <summary>True when <paramref name="value"/> is a declared member (so <c>default</c> is not one).</summary>
    public static bool IsDefined(PlayerRefusal value) => WireCodes.ContainsKey(value);

    /// <summary>
    /// A stable hash of the canonical strings — the refusal set's version, derived rather than
    /// hand-typed, so adding, removing or renaming a member moves it automatically. A member rename
    /// that leaves the wire string alone deliberately does NOT move it: the wire is unchanged.
    /// </summary>
    public static string RefusalEnumVersion { get; } = ComputeVersion(All.Select(WireCode));

    private static string ComputeVersion(IEnumerable<string> canonicalCodes)
    {
        var payload = string.Join('\n', canonicalCodes.OrderBy(code => code, StringComparer.Ordinal));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    private static IReadOnlyDictionary<PlayerRefusal, string> BuildWireCodes()
    {
        var codes = new Dictionary<PlayerRefusal, string>();
        foreach (var member in Enum.GetValues<PlayerRefusal>())
        {
            var code = typeof(PlayerRefusal)
                .GetField(member.ToString())
                ?.GetCustomAttributes(typeof(RefusalCodeAttribute), inherit: false)
                .OfType<RefusalCodeAttribute>()
                .SingleOrDefault()
                ?.Code;

            if (string.IsNullOrEmpty(code))
                throw new InvalidOperationException(
                    $"PlayerRefusal.{member} carries no RefusalCode; the closed set has no wire string for it.");

            codes.Add(member, code);
        }

        return codes;
    }
}
