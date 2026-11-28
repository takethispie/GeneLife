using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Genelife.Api.Commands;
using Genelife.Api.Services;
using Xunit;

namespace Genelife.UnitTests.Players;

/// <summary>
/// Freezes the closed player-action refusal vocabulary (GDD chapter 08 §8.5(1); chapter 07 §7.6
/// AC-4a, rules 2 and 3).
///
/// The wire stays a string: <c>PlayerCommandResult.Code</c> is what the shipped browser client keys
/// its message map and its notification cue off. So a rename that keeps the byte value passes a
/// byte-only test and still breaks the player's experience — this guard therefore pins the
/// <b>canonical string</b> and the member together, and the action path's call sites on top of that.
///
/// Scope is deliberate: the closed set is the player-action path only. Codes produced by
/// <c>SimulationEngine</c> or the social surfaces are mapped consumer-side by wire string in
/// <c>GDD/mockups/assets/refusal-reasons.json</c> and are NOT required to be members here.
/// </summary>
[Trait("Category", "Documentation")]
public class PlayerRefusalTests
{
    private const string ActionServicePath = "Genelife.Application/Services/PlayerActionService.cs";
    private const string CommandServicePath = "Genelife.Application/Services/PlayerCommandService.cs";

    /// <summary>
    /// The closed set, hand-written on purpose: this is the freeze. Adding, removing, renaming or
    /// re-pointing a member must be a deliberate edit here as well as in the enum, so a wire string
    /// can never drift silently. Values are pinned because nothing on the wire depends on them and
    /// a shift would otherwise be invisible.
    /// </summary>
    private static readonly (PlayerRefusal Refusal, byte Value, string Code)[] Frozen =
    {
        (PlayerRefusal.GenosNotFound, 1, "genos_not_found"),
        (PlayerRefusal.NotOwner, 2, "not_owner"),
        (PlayerRefusal.NotAvailable, 3, "not_available"),
        (PlayerRefusal.OutOfRange, 4, "out_of_range"),
        (PlayerRefusal.AlreadyEmployed, 5, "already_employed"),
        (PlayerRefusal.InvalidState, 6, "invalid_state"),
        (PlayerRefusal.OfferNotFound, 7, "offer_not_found"),
        (PlayerRefusal.OfferExpired, 8, "offer_expired"),
        (PlayerRefusal.NotEmployed, 9, "not_employed"),
        (PlayerRefusal.InsufficientFunds, 10, "insufficient_funds"),
        (PlayerRefusal.NoHousehold, 11, "no_household"),
        (PlayerRefusal.OutOfStock, 12, "out_of_stock"),
        (PlayerRefusal.PantryEmpty, 13, "pantry_empty"),
        (PlayerRefusal.TargetNotFound, 14, "target_not_found"),
        (PlayerRefusal.TargetRequired, 15, "target_required"),
        (PlayerRefusal.SelfTarget, 16, "self_target"),
        (PlayerRefusal.InvalidActivity, 17, "invalid_activity"),
        (PlayerRefusal.NotInVenue, 18, "not_in_venue"),
        (PlayerRefusal.Declined, 19, "declined"),
        (PlayerRefusal.NotTarget, 20, "not_target"),
        (PlayerRefusal.NotPartner, 21, "not_partner"),
        (PlayerRefusal.NotConceived, 22, "not_conceived"),
        (PlayerRefusal.MalformedPayload, 23, "malformed_payload"),
        (PlayerRefusal.WorldNotReady, 24, "world_not_ready"),
        (PlayerRefusal.RateLimited, 25, "rate_limited"),
        (PlayerRefusal.Unauthenticated, 26, "unauthenticated"),
        (PlayerRefusal.QueueFull, 27, "queue_full"),
        (PlayerRefusal.PayloadTooLarge, 28, "payload_too_large")
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeneLife.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

    /// <summary>An independent copy of the version hash, so the guard does not pass by tautology.</summary>
    private static string Hash(IEnumerable<string> codes)
    {
        var payload = string.Join('\n', codes.OrderBy(code => code, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()[..16];
    }

    [Fact]
    public void The_closed_set_is_exactly_the_frozen_vocabulary()
    {
        var declared = Enum.GetValues<PlayerRefusal>().ToHashSet();
        var frozen = Frozen.Select(entry => entry.Refusal).ToHashSet();

        Assert.Equal(
            frozen.OrderBy(value => (byte)value).ToArray(),
            declared.OrderBy(value => (byte)value).ToArray());

        Assert.All(Frozen, entry =>
        {
            Assert.Equal(entry.Value, (byte)entry.Refusal);
            Assert.Equal(entry.Code, entry.Refusal.WireCode());
            Assert.Matches("^[a-z][a-z0-9_]*$", entry.Code);
        });

        Assert.Equal(Frozen.Length, Frozen.Select(entry => entry.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Frozen.Length, Frozen.Select(entry => entry.Value).Distinct().Count());
        Assert.Equal(Frozen.Length, PlayerRefusalVocabulary.All.Count);
        Assert.False(PlayerRefusalVocabulary.IsDefined(default), "zero must not be a member — it is what an unset value looks like");
    }

    [Fact]
    public void Canonical_strings_round_trip_and_the_set_is_closed()
    {
        Assert.All(Frozen, entry =>
        {
            Assert.True(PlayerRefusalVocabulary.TryParse(entry.Code, out var parsed));
            Assert.Equal(entry.Refusal, parsed);
        });

        Assert.False(PlayerRefusalVocabulary.TryParse("not_a_refusal", out _));
        Assert.False(PlayerRefusalVocabulary.TryParse("NOT_AVAILABLE", out _));
        Assert.False(PlayerRefusalVocabulary.TryParse(null, out _));
        Assert.False(PlayerRefusalVocabulary.TryParse("", out _));
    }

    [Fact]
    public void RefusalEnumVersion_is_derived_from_the_canonical_strings()
    {
        var canonical = PlayerRefusalVocabulary.All.Select(refusal => refusal.WireCode()).ToArray();
        var version = PlayerRefusalVocabulary.RefusalEnumVersion;

        Assert.Matches("^[0-9a-f]{16}$", version);
        Assert.Equal(Hash(canonical), version);
        Assert.Equal(version, PlayerRefusalVocabulary.RefusalEnumVersion);

        // Derivation is order-independent: reordering the enum must not move the version.
        Assert.Equal(version, Hash(canonical.Reverse()));

        // ...but it DOES move when the set moves, which is the whole point of deriving it.
        var renamed = canonical.Select(code => code == "not_available" ? "not_available_v2" : code);
        Assert.NotEqual(version, Hash(renamed));

        var extended = canonical.Append("brand_new_refusal");
        Assert.NotEqual(version, Hash(extended));

        var removed = canonical.Where(code => code != "declined");
        Assert.NotEqual(version, Hash(removed));
    }

    [Fact]
    public void Every_member_is_reachable_from_the_action_path()
    {
        var referenced = ReferencedMembers(Read(ActionServicePath))
            .Concat(ReferencedMembers(Read(CommandServicePath)))
            .ToHashSet();

        var missing = Enum.GetNames<PlayerRefusal>()
            .Except(referenced, StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(missing.Length == 0,
            "These members of the closed set are not emitted by the player-action path — the enum carries a " +
            "code nothing can refuse with (or the call site was left behind):\n  " + string.Join("\n  ", missing));

        var unknown = referenced
            .Except(Enum.GetNames<PlayerRefusal>(), StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(unknown.Length == 0,
            "The action path names PlayerRefusal members the enum does not declare:\n  " + string.Join("\n  ", unknown));
    }

    /// <summary>
    /// The ratchet. The action path's vocabulary lives in the enum and nowhere else, so the next
    /// hand-written <c>Reject("…")</c> is a compile-time-looking edit that this fails on.
    /// </summary>
    [Fact]
    public void No_Reject_string_literal_survives_in_the_action_path()
    {
        Assert.Empty(StringLiterals(Read(ActionServicePath)));
        Assert.Empty(StringLiterals(Read(CommandServicePath)));

        // Negative control: the check can fail.
        Assert.NotEmpty(StringLiterals("    return PlayerActionResult.Reject(\"not_available\", \"nope\");"));
    }

    /// <summary>Both result types carry the canonical string on the wire; the enum is only the source.</summary>
    [Fact]
    public void The_result_types_put_the_canonical_string_on_the_wire()
    {
        var action = PlayerActionResult.Reject(PlayerRefusal.NotAvailable, "nope");
        Assert.False(action.Applied);
        Assert.Equal("not_available", action.Code);
        Assert.Equal("nope", action.Message);

        var admission = PlayerCommandAdmission.Reject(PlayerRefusal.RateLimited, "slow down");
        Assert.False(admission.Accepted);
        Assert.Equal(Guid.Empty, admission.CommandId);
        Assert.Equal("rate_limited", admission.Code);
        Assert.Equal("slow down", admission.Message);
    }

    private static string[] StringLiterals(string source) =>
        Regex.Matches(source, @"Reject\(\s*""")
            .Select(match => source[..match.Index].Count(c => c == '\n') + 1)
            .Select(line => $"line {line}")
            .ToArray();

    private static IEnumerable<string> ReferencedMembers(string source) =>
        Regex.Matches(source, @"PlayerRefusal\.(?<member>[A-Za-z][A-Za-z0-9]*)")
            .Select(match => match.Groups["member"].Value)
            .Distinct(StringComparer.Ordinal);
}
