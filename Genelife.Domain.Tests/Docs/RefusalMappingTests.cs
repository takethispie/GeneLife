using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Genelife.Api.Commands;
using Xunit;

namespace Genelife.UnitTests.Docs;

/// <summary>
/// Guards the player-facing refusal vocabulary (GDD chapter 07 §7.6, acceptance check AC-4a's
/// design half): every refusal the action path can emit has exactly one reason in the UI's closed
/// set, and every code the shipped client already phrases is covered.
///
/// One source: <c>GDD/mockups/assets/refusal-reasons.json</c>. The counts are never written down —
/// the <b>dial</b> floor is derived from the action path's own closed set (<see cref="PlayerRefusal"/>),
/// so a new member fails this test until someone decides what the player should be told, and a
/// renamed code fails until the mapping moves with it.
///
/// Scope is deliberate, and the guard is <b>per surface</b>. The dial floor is the 28-member
/// <see cref="PlayerRefusal"/> enum the two action-path services now emit: every one of its wire
/// strings must be mapped, and the mapping must name no dial code outside it. Codes produced by
/// <c>SimulationEngine</c> (movement) or the social surfaces are <i>outside</i> the enum by design
/// — the mapping is consumer-side and keyed by the wire string — so they keep their own
/// producer-contains-the-string floor and this test never forces a refactor of the engine or the
/// chat/social services.
/// </summary>
[Trait("Category", "Documentation")]
public class RefusalMappingTests
{
    private const string MappingPath = "GDD/mockups/assets/refusal-reasons.json";
    private const string BrowserClientPath = "clients/browser/src/app/bootstrap.ts";

    /// <summary>Surfaces the mapping may declare. Only `dial` is required to be inside the enum.</summary>
    private static readonly string[] Surfaces = { "dial", "social", "engine", "system" };

    internal sealed record Reason(string Key, string Label, string? Affordance);
    internal sealed record Code(string Value, string Reason, string Surface, string Producer);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeneLife.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

    private static bool Exists(string relativePath) =>
        File.Exists(Path.Combine(RepositoryRoot(), relativePath));

    internal static IReadOnlyList<Reason> ParseReasons(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("reasons", out var reasons), "the mapping must declare `reasons`");

        return reasons.EnumerateArray()
            .Select(r => new Reason(
                r.GetProperty("key").GetString() ?? "",
                r.GetProperty("label").GetString() ?? "",
                r.TryGetProperty("affordance", out var a) ? a.GetString() : null))
            .ToArray();
    }

    internal static IReadOnlyList<Code> ParseCodes(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("codes", out var codes), "the mapping must declare `codes`");

        return codes.EnumerateArray()
            .Select(c => new Code(
                c.GetProperty("code").GetString() ?? "",
                c.GetProperty("reason").GetString() ?? "",
                c.GetProperty("surface").GetString() ?? "",
                c.GetProperty("producer").GetString() ?? ""))
            .ToArray();
    }

    /// <summary>
    /// The dial floor, derived: every canonical wire string of the action path's closed refusal set
    /// (<see cref="PlayerRefusal"/>). The enum is the compile-time source — a new <c>Reject</c> is a
    /// new member — but the wire is what the client keys its wording off, so the mapping must match
    /// the members' <see cref="PlayerRefusalVocabulary.WireCode(PlayerRefusal)"/> strings exactly.
    /// </summary>
    internal static ISet<string> DialFloor() =>
        PlayerRefusalVocabulary.All
            .Select(refusal => refusal.WireCode())
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>The dial-surface codes the mapping declares.</summary>
    internal static ISet<string> MappedDialCodes() =>
        ParseCodes(Read(MappingPath))
            .Where(c => c.Surface == "dial")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Both directions of the dial floor: codes the action path can emit with no reason mapped
    /// (<c>Unmapped</c>), and dial codes the mapping names that the action path no longer emits
    /// (<c>Orphaned</c>). Kept separate so a failure says which way the floor moved.
    /// </summary>
    internal static (string[] Unmapped, string[] Orphaned) CompareDialFloor(ISet<string> produced, ISet<string> mapped) =>
        (produced.Except(mapped).OrderBy(c => c, StringComparer.Ordinal).ToArray(),
         mapped.Except(produced).OrderBy(c => c, StringComparer.Ordinal).ToArray());

    /// <summary>Keys of the shipped client's phrasing map — the consumer that must be covered.</summary>
    internal static ISet<string> ClientPhrasedCodes(string source)
    {
        var block = Regex.Match(source, @"messages:\s*Record<string,\s*string>\s*=\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline);
        Assert.True(block.Success, $"{BrowserClientPath}: could not find the rejection phrasing map.");

        return Regex.Matches(block.Groups["body"].Value, @"(?<code>[a-z_]+)\s*:")
            .Select(m => m.Groups["code"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void Mapping_is_well_formed()
    {
        var json = Read(MappingPath);
        var reasons = ParseReasons(json);
        var codes = ParseCodes(json);

        Assert.True(reasons.Count > 0, "the mapping must declare at least one reason");
        Assert.Equal(reasons.Count, reasons.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(reasons, r =>
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", r.Key);
            Assert.False(string.IsNullOrWhiteSpace(r.Label), $"{r.Key} needs a player-facing label");
        });

        var reasonKeys = reasons.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(codes.Count, codes.Select(c => c.Value).Distinct(StringComparer.Ordinal).Count());

        Assert.All(codes, c =>
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", c.Value);
            Assert.True(reasonKeys.Contains(c.Reason), $"{c.Value} maps to unknown reason '{c.Reason}'");
            Assert.Contains(c.Surface, Surfaces);
            Assert.True(Exists(c.Producer), $"{c.Value} names a producer that does not exist: {c.Producer}");
        });
    }

    /// <summary>
    /// The dial floor, derived from the action path's own closed set rather than scraped from
    /// literals: the dial surface's mapped codes and the <see cref="PlayerRefusal"/> vocabulary are
    /// the same set. A new member without a reason fails; a mapping left behind after a rename also
    /// fails, so the canonical strings stay frozen by construction.
    /// </summary>
    [Fact]
    public void Every_action_path_refusal_has_exactly_one_reason()
    {
        var (unmapped, orphaned) = CompareDialFloor(DialFloor(), MappedDialCodes());

        Assert.True(unmapped.Length == 0,
            "The action path can emit refusals the design has no reason for (GDD 07 §7.6 — explain, never " +
            "grey out). Map them in refusal-reasons.json:\n  " + string.Join("\n  ", unmapped));

        Assert.True(orphaned.Length == 0,
            "The mapping names dial codes the action path no longer emits — a rename moved without the " +
            "mapping, and the client's message would silently stop matching:\n  " + string.Join("\n  ", orphaned));
    }

    /// <summary>
    /// Codes produced outside the action path (engine, social) are mapped but not converted to enum
    /// members. Their surface keeps its own floor: the canonical string must still exist in the
    /// producer, so a rename there is caught too — without the enum having to cover them.
    /// </summary>
    [Fact]
    public void Non_dial_codes_still_exist_in_their_producer()
    {
        foreach (var code in ParseCodes(Read(MappingPath)).Where(c => c.Surface != "dial"))
        {
            var source = Read(code.Producer);
            Assert.True(source.Contains($"\"{code.Value}\"", StringComparison.Ordinal),
                $"{code.Value} is mapped against {code.Producer}, but that file no longer contains the " +
                "canonical string. Update the mapping with the rename (GDD 07 §7.6).");
        }
    }

    /// <summary>The shipped client already phrases codes; every one of them must have a reason.</summary>
    [Fact]
    public void Every_code_the_client_phrases_is_mapped()
    {
        var phrased = ClientPhrasedCodes(Read(BrowserClientPath));
        var mapped = ParseCodes(Read(MappingPath)).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);

        var uncovered = phrased.Except(mapped).OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Assert.True(uncovered.Length == 0,
            $"{BrowserClientPath} phrases codes the mapping does not cover. Add them to " +
            "refusal-reasons.json (the client should key its wording off the reason, not the raw code):\n  " +
            string.Join("\n  ", uncovered));
    }

    [Fact]
    public void No_reason_is_dead()
    {
        var codes = ParseCodes(Read(MappingPath));
        var unused = ParseReasons(Read(MappingPath))
            .Select(r => r.Key)
            .Except(codes.Select(c => c.Reason), StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();

        Assert.True(unused.Length == 0,
            "Reasons declared but never mapped mean the design language promises a distinction the " +
            "simulation cannot make:\n  " + string.Join("\n  ", unused));
    }

    /// <summary>
    /// Negative control: the check can fail in both directions. A fabricated code answers to no
    /// member and no mapping entry; inventing it into the mapping is reported <c>Orphaned</c>, and
    /// dropping a real member is reported <c>Unmapped</c>. Without this the green above could be a
    /// comparison that can never disagree.
    /// </summary>
    [Fact]
    public void The_check_can_fail()
    {
        var produced = DialFloor();
        var mapped = MappedDialCodes();

        const string fabricated = "definitely_not_a_real_refusal";
        Assert.DoesNotContain(fabricated, produced);
        Assert.DoesNotContain(fabricated, mapped);

        // A mapping that invents a dial code must be caught as orphaned (and nothing else moves).
        var invented = mapped.Append(fabricated).ToHashSet(StringComparer.Ordinal);
        var (inventedUnmapped, inventedOrphaned) = CompareDialFloor(produced, invented);
        Assert.Contains(fabricated, inventedOrphaned);
        Assert.Empty(inventedUnmapped);

        // A mapping that drops a real dial code must be caught as unmapped (and nothing else moves).
        var dropped = mapped.First();
        var missing = mapped.Where(c => c != dropped).ToHashSet(StringComparer.Ordinal);
        var (droppedUnmapped, droppedOrphaned) = CompareDialFloor(produced, missing);
        Assert.Contains(dropped, droppedUnmapped);
        Assert.Empty(droppedOrphaned);
    }
}