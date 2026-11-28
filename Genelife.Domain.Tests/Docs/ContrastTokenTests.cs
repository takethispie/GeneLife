using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Genelife.UnitTests.Docs;

/// <summary>
/// Enforces the "Civic Paper" accessibility rule (GDD chapter 07 §7.5.1, acceptance check AC-6):
/// every text-on-surface colour pair the design language permits must meet its WCAG 2.1 ratio.
///
/// The tokens are read out of <c>GDD/mockups/assets/mockups.css</c> — the single place the palette
/// is declared — so a palette regression fails CI instead of shipping. This lives in the unit test
/// project rather than as a workflow step because the required <c>unit</c> check already runs this
/// project: no new CI plumbing, and the assertion cannot be skipped by forgetting to type a command.
///
/// <see cref="The_check_can_fail"/> is a deliberate negative control: it proves the assertion is
/// capable of failing, so a future "everything passes" result is evidence rather than noise.
/// </summary>
[Trait("Category", "Documentation")]
public class ContrastTokenTests
{
    private const string TokensPath = "GDD/mockups/assets/mockups.css";

    /// <summary>
    /// (foreground token, background token, minimum ratio, what the pair is used for).
    /// The minimums restate GDD §7.5.2: body text ≥ 7:1, everything else ≥ 4.5:1.
    /// </summary>
    private static readonly (string Foreground, string Background, double Minimum, string Use)[] Pairs =
    {
        ("--ink", "--paper", 7.0, "body text"),
        ("--ink-2", "--paper", 7.0, "secondary body text"),
        ("--ink-2", "--paper-2", 4.5, "secondary text on recessed surface"),
        ("--ink-3", "--paper", 4.5, "labels / tertiary text"),
        ("--ink-3", "--paper-2", 4.5, "labels on recessed surface"),
        ("--money", "--paper", 4.5, "money figures"),
        ("--accent", "--paper", 4.5, "accent text and links"),
        ("--body", "--paper", 4.5, "needs domain"),
        ("--work", "--paper", 4.5, "work domain"),
        ("--social", "--paper", 4.5, "social domain"),
        ("--legacy", "--paper", 4.5, "legacy domain"),
        ("--warn", "--paper", 4.5, "warning text"),
        ("--ok", "--paper", 4.5, "success text"),
        ("--paper", "--ink", 4.5, "text on primary button / dark chip"),
        ("--paper", "--accent", 4.5, "text on accent button")
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeneLife.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>Parses the <c>:root</c> block of a stylesheet into token → "#RRGGBB".</summary>
    internal static IReadOnlyDictionary<string, string> ParseTokens(string css)
    {
        var root = Regex.Match(css, @":root\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(root.Success, "ContrastTokenTests: could not find a :root block in the stylesheet.");

        return Regex.Matches(root.Groups["body"].Value, @"(?<token>--[a-z0-9-]+)\s*:\s*(?<colour>#[0-9A-Fa-f]{6})")
            .ToDictionary(m => m.Groups["token"].Value, m => m.Groups["colour"].Value);
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
        var tokens = ParseTokens(File.ReadAllText(Path.Combine(RepositoryRoot(), TokensPath)));

        var missing = Pairs
            .SelectMany(p => new[] { p.Foreground, p.Background })
            .Distinct()
            .Where(token => !tokens.ContainsKey(token))
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0,
            $"{TokensPath} no longer declares: {string.Join(", ", missing)}. " +
            "If a token was renamed, update the pair table in this test in the same change (GDD 07 §7.5).");
    }

    [Fact]
    public void Every_permitted_text_pair_meets_its_wcag_ratio()
    {
        var tokens = ParseTokens(File.ReadAllText(Path.Combine(RepositoryRoot(), TokensPath)));

        var failures = new List<string>();
        foreach (var (foreground, background, minimum, use) in Pairs)
        {
            var ratio = ContrastRatio(tokens[foreground], tokens[background]);
            if (ratio < minimum)
            {
                failures.Add(
                    $"{use}: {foreground} {tokens[foreground]} on {background} {tokens[background]} " +
                    $"= {ratio:0.00}:1, needs {minimum:0.0}:1");
            }
        }

        Assert.True(failures.Count == 0,
            "The design language no longer meets its own contrast rule (GDD 07 §7.5.1). " +
            "Darken or lighten the token — do not relax the threshold:\n  " + string.Join("\n  ", failures));
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