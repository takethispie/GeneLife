using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Genelife.UnitTests.Docs;

/// <summary>
/// Guards the player-facing refusal vocabulary (GDD chapter 07 §7.6, acceptance check AC-4a's
/// design half): every refusal the action path can emit has exactly one reason in the UI's closed
/// set, and every code the shipped client already phrases is covered.
///
/// One source: <c>GDD/mockups/assets/refusal-reasons.json</c>. The counts are never written down —
/// they are derived from the producers, so a new <c>Reject("…")</c> fails this test until someone
/// decides what the player should be told, and a renamed code fails until the mapping moves with it.
///
/// Scope is deliberate: the <b>dial</b> floor is the two action-path services. Codes produced by
/// <c>SimulationEngine</c> (movement) or the social surfaces are mapped too — the mapping is
/// consumer-side and keyed by the wire string — but they are not required to live in a converted
/// enum, so this test never forces a refactor of the engine.
/// </summary>
[Trait("Category", "Documentation")]
public class RefusalMappingTests
{
    private const string MappingPath = "GDD/mockups/assets/refusal-reasons.json";
    private const string ActionServicePath = "Genelife.Application/Services/PlayerActionService.cs";
    private const string CommandServicePath = "Genelife.Application/Services/PlayerCommandService.cs";
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

    /// <summary>The wire codes a service can actually emit: <c>Reject("code", …)</c> call sites.</summary>
    internal static ISet<string> ProducedCodes(string source) =>
        Regex.Matches(source, @"Reject\(""(?<code>[a-z_]+)""")
            .Select(m => m.Groups["code"].Value)
            .ToHashSet(StringComparer.Ordinal);

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
    /// The dial floor, derived: the action path's emittable codes and the dial's mapped codes are the
    /// same set. Adding a <c>Reject("…")</c> without a reason fails; leaving a mapping behind after a
    /// rename also fails, so the canonical strings stay frozen by construction.
    /// </summary>
    [Fact]
    public void Every_action_path_refusal_has_exactly_one_reason()
    {
        var produced = ProducedCodes(Read(ActionServicePath))
            .Concat(ProducedCodes(Read(CommandServicePath)))
            .ToHashSet(StringComparer.Ordinal);

        var mapped = ParseCodes(Read(MappingPath))
            .Where(c => c.Surface == "dial")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        var unmapped = produced.Except(mapped).OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Assert.True(unmapped.Length == 0,
            "The action path can emit refusals the design has no reason for (GDD 07 §7.6 — explain, never " +
            "grey out). Map them in refusal-reasons.json:\n  " + string.Join("\n  ", unmapped));

        var orphaned = mapped.Except(produced).OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Assert.True(orphaned.Length == 0,
            "The mapping names dial codes the action path no longer emits — a rename moved without the " +
            "mapping, and the client's message would silently stop matching:\n  " + string.Join("\n  ", orphaned));
    }

    /// <summary>
    /// Codes produced outside the action path (engine, social) are mapped but not converted. Their
    /// canonical string must still exist in the producer, so a rename there is caught too.
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

    /// <summary>Negative control: the check can fail. A code that exists nowhere must not resolve.</summary>
    [Fact]
    public void The_check_can_fail()
    {
        var mapped = ParseCodes(Read(MappingPath)).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        var produced = ProducedCodes(Read(ActionServicePath))
            .Concat(ProducedCodes(Read(CommandServicePath)))
            .ToHashSet(StringComparer.Ordinal);

        const string fabricated = "definitely_not_a_real_refusal";
        Assert.DoesNotContain(fabricated, mapped);
        Assert.DoesNotContain(fabricated, produced);
        Assert.NotEmpty(produced.Except(mapped).Concat(mapped.Except(produced)).DefaultIfEmpty("none-missing"));
    }
}