using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Genelife.UnitTests.Docs;

/// <summary>
/// Enforces the "Civic Paper" accessibility rule (GDD chapter 07 §7.5.1, acceptance check AC-6):
/// every text-on-surface colour pair the design language permits must meet its WCAG 2.1 ratio.
///
/// Two inputs, one copy each:
///   * the <b>colours</b> live in <c>GDD/mockups/assets/mockups.css</c>;
///   * the <b>rule</b> (pair, threshold, purpose) lives in <c>GDD/mockups/assets/contrast-pairs.json</c>.
/// The designer's script (<c>contrast_check.py</c>) reads the same two files, so a local pass and
/// this merge gate cannot disagree — the pair table is not duplicated anywhere.
///
/// The tests live in the unit test project rather than as a workflow step because the required
/// <c>unit</c> check already runs this project: no new CI plumbing, and the assertion cannot be
/// skipped by forgetting to type a command.
///
/// <see cref="The_check_can_fail"/> is a deliberate negative control, and
/// <see cref="The_rule_cannot_be_softened_or_emptied"/> guards the rule file itself, so a green
/// run stays evidence rather than noise.
/// </summary>
[Trait("Category", "Documentation")]
public class ContrastTokenTests
{
    private const string TokensPath = "GDD/mockups/assets/mockups.css";
    private const string RulesPath = "GDD/mockups/assets/contrast-pairs.json";

    /// <summary>GDD §7.5.2 permits exactly two thresholds: body text 7:1, everything else 4.5:1.</summary>
    private static readonly double[] PermittedMinimums = { 4.5, 7.0 };

    /// <summary>The domain colours the language defines; losing one is a design regression.</summary>
    private static readonly string[] RequiredTokens =
    {
        "--ink", "--ink-2", "--ink-3", "--paper", "--paper-2",
        "--accent", "--money", "--body", "--work", "--social", "--legacy", "--warn", "--ok"
    };

    internal sealed record Pair(string Foreground, string Background, double Minimum, string Use);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeneLife.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));

    /// <summary>Parses the <c>:root</c> block of a stylesheet into token → "#RRGGBB".</summary>
    internal static IReadOnlyDictionary<string, string> ParseTokens(string css)
    {
        var root = Regex.Match(css, @":root\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(root.Success, "ContrastTokenTests: could not find a :root block in the stylesheet.");

        return Regex.Matches(root.Groups["body"].Value, @"(?<token>--[a-z0-9-]+)\s*:\s*(?<colour>#[0-9A-Fa-f]{6})")
            .ToDictionary(m => m.Groups["token"].Value, m => m.Groups["colour"].Value);
    }

    internal static IReadOnlyList<Pair> ParsePairs(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("pairs", out var pairs),
            $"{RulesPath} must declare a \"pairs\" array.");

        return pairs.EnumerateArray()
            .Select(entry => new Pair(
                entry.GetProperty("foreground").GetString() ?? "",
                entry.GetProperty("background").GetString() ?? "",
                entry.GetProperty("minimum").GetDouble(),
                entry.GetProperty("use").GetString() ?? ""))
            .ToArray();
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            var value = Convert.ToInt32(hex.Substring(offset, 2), 16) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(1)) + (0.7152 * Channel(3)) + (0.0722 * Channel(5));
    }

    /// <summary>WCAG 2.1 contrast ratio between two "#RRGGBB" colours.</summary>
    internal static double ContrastRatio(string foreground, string background)
    {
        var a = Luminance(foreground);
        var b = Luminance(background);
        var (high, low) = a > b ? (a, b) : (b, a);
        return (high + 0.05) / (low + 0.05);
    }

    [Fact]
    public void Design_language_palette_declares_every_token_the_rule_checks()
    {
        var tokens = ParseTokens(Read(TokensPath));
        var pairs = ParsePairs(Read(RulesPath));

        var missing = pairs
            .SelectMany(p => new[] { p.Foreground, p.Background })
            .Distinct()
            .Where(token => !tokens.ContainsKey(token))
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0,
            $"{TokensPath} no longer declares: {string.Join(", ", missing)}. " +
            "If a token was renamed, update contrast-pairs.json in the same change (GDD 07 §7.5).");
    }

    [Fact]
    public void Every_permitted_text_pair_meets_its_wcag_ratio()
    {
        var tokens = ParseTokens(Read(TokensPath));

        var failures = new List<string>();
        foreach (var pair in ParsePairs(Read(RulesPath)))
        {
            var ratio = ContrastRatio(tokens[pair.Foreground], tokens[pair.Background]);
            if (ratio < pair.Minimum)
            {
                failures.Add(
                    $"{pair.Use}: {pair.Foreground} {tokens[pair.Foreground]} on " +
                    $"{pair.Background} {tokens[pair.Background]} = {ratio:0.00}:1, needs {pair.Minimum:0.0}:1");
            }
        }

        Assert.True(failures.Count == 0,
            "The design language no longer meets its own contrast rule (GDD 07 §7.5.1). " +
            "Darken or lighten the token — do not relax the threshold:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>
    /// Guards the rule file itself: a threshold cannot be lowered to make a failing palette pass, a
    /// pair cannot be deleted to shrink coverage, and the domain colours cannot disappear.
    /// </summary>
    [Fact]
    public void The_rule_cannot_be_softened_or_emptied()
    {
        var pairs = ParsePairs(Read(RulesPath));

        Assert.True(pairs.Count >= 15,
            $"{RulesPath} declares {pairs.Count} pairs; the design language requires at least 15 " +
            "(GDD 07 §7.5.1). Removing a pair to make the check pass is not a fix.");

        var badThresholds = pairs
            .Where(p => !PermittedMinimums.Contains(p.Minimum))
            .Select(p => $"{p.Use}: {p.Minimum}")
            .ToArray();
        Assert.True(badThresholds.Length == 0,
            "GDD 07 §7.5.2 permits only 4.5:1 and 7:1. Unexpected thresholds — lowering a threshold " +
            "does not make a palette accessible:\n  " + string.Join("\n  ", badThresholds));

        Assert.All(pairs, p =>
        {
            Assert.StartsWith("--", p.Foreground);
            Assert.StartsWith("--", p.Background);
            Assert.False(string.IsNullOrWhiteSpace(p.Use), "every pair needs a 'use' so failures are readable");
        });

        var tokens = ParseTokens(Read(TokensPath));
        Assert.All(RequiredTokens, token => Assert.True(tokens.ContainsKey(token),
            $"{TokensPath} must keep declaring {token} — it is part of the design language."));
    }

    [Fact]
    public void The_check_can_fail()
    {
        // Negative control: the light grey that shipped in chapter 07 v0.2 measured 3.23:1 on paper.
        // If this ever stops failing, the assertion above has become vacuous.
        var regression = ContrastRatio("#7D8794", "#F5F1E8");

        Assert.True(regression < 4.5,
            $"Expected the v0.2 label colour to fail the label threshold, but it measured {regression:0.00}:1.");
        Assert.InRange(regression, 3.0, 3.5);
    }
}
