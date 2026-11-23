using System.Globalization;
using System.Text;
using Genelife.Api.DTOs;

namespace Genelife.Web.Appearance;

/// <summary>
/// Deterministic SVG portrait renderer from the shared layer contract. The
/// browser uses Pixi sprites; both consume the same part/palette ids.
/// </summary>
public static class AppearanceSvgRenderer
{
    private static readonly Dictionary<string, string> SkinColors = new(StringComparer.Ordinal)
    {
        ["skin-porcelain"] = "#f3d8c5",
        ["skin-fair"] = "#e8c3a5",
        ["skin-olive"] = "#cba37a",
        ["skin-tan"] = "#b07a4f",
        ["skin-brown"] = "#8a5a35",
        ["skin-deep"] = "#5c3a24",
        ["skin-neutral"] = "#cba37a"
    };

    private static readonly Dictionary<string, string> HairColors = new(StringComparer.Ordinal)
    {
        ["black"] = "#1f1a17",
        ["brown"] = "#5b3a24",
        ["blonde"] = "#d8b45f",
        ["ginger"] = "#b5522c",
        ["grey"] = "#c9c9c9"
    };

    private static readonly Dictionary<string, string> EyeColors = new(StringComparer.Ordinal)
    {
        ["brown"] = "#6b4226",
        ["blue"] = "#4b83b5",
        ["green"] = "#4c7a4a",
        ["grey"] = "#8a8f96",
        ["hazel"] = "#a68a4c"
    };

    private static readonly Dictionary<string, string> OutfitColors = new(StringComparer.Ordinal)
    {
        ["outfit-child"] = "#f2a65a",
        ["outfit-student"] = "#6f9bd1",
        ["outfit-worker"] = "#5a7d6b",
        ["outfit-senior"] = "#9a8fb8",
        ["outfit-player"] = "#67e8f9",
        ["outfit-fallback"] = "#8a8f96"
    };

    public static string Render(AppearanceSimStatus sim, int width = 120, int height = 160)
    {
        var layers = sim.Layers;
        var skin = Resolve(SkinColors, layers.SkinPalette, SkinColors["skin-neutral"]);
        var hairColor = HairColorFor(layers.HairPart);
        var eye = Resolve(EyeColors, layers.EyePart, EyeColors["brown"]);
        var outfit = Resolve(OutfitColors, layers.OutfitPart, OutfitColors["outfit-fallback"]);
        var (headRx, headRy) = FaceSize(layers.FacePart);
        var bodyWidth = BodyWidth(layers.BodyPart);
        var scale = Math.Clamp(layers.HeightScale, 0.5f, 1.1f);

        var sb = new StringBuilder(2048);
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\" role=\"img\" aria-label=\"Portrait of {Escape(sim.Name)}\">");
        sb.Append("<rect width=\"120\" height=\"160\" fill=\"#0b1520\"/>");

        if (sim.IsPlayerControlled)
            sb.Append("<circle cx=\"60\" cy=\"82\" r=\"54\" fill=\"none\" stroke=\"#67e8f9\" stroke-width=\"3\" stroke-dasharray=\"6 4\"/>");

        sb.Append(CultureInfo.InvariantCulture,
            $"<g transform=\"translate(60 150) scale({scale:0.##}) translate(-60 -150)\">");

        // body + outfit share a silhouette; outfit is the shared palette ramp.
        sb.Append(CultureInfo.InvariantCulture,
            $"<path d=\"M{60 - bodyWidth} 150 Q{60 - bodyWidth} 104 60 104 Q{60 + bodyWidth} 104 {60 + bodyWidth} 150 Z\" fill=\"{outfit}\"/>");
        sb.Append(CultureInfo.InvariantCulture,
            $"<path d=\"M{60 - bodyWidth + 6} 150 Q{60 - bodyWidth + 6} 112 60 112 Q{60 + bodyWidth - 6} 112 {60 + bodyWidth - 6} 150 Z\" fill=\"{skin}\" opacity=\"0.25\"/>");

        // neck and head
        sb.Append(CultureInfo.InvariantCulture,
            $"<rect x=\"52\" y=\"88\" width=\"16\" height=\"18\" rx=\"6\" fill=\"{skin}\"/>");
        sb.Append(CultureInfo.InvariantCulture,
            $"<ellipse cx=\"60\" cy=\"62\" rx=\"{headRx}\" ry=\"{headRy}\" fill=\"{skin}\"/>");

        // eyes and expression
        sb.Append(CultureInfo.InvariantCulture,
            $"<circle cx=\"52\" cy=\"60\" r=\"2.6\" fill=\"{eye}\"/><circle cx=\"68\" cy=\"60\" r=\"2.6\" fill=\"{eye}\"/>");
        sb.Append(MouthPath(layers.ExpressionPart, eye));

        // hair is drawn after the head so it reads as a silhouette layer.
        if (!string.Equals(layers.HairPart, "hair-bald", StringComparison.Ordinal))
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"<path d=\"M{60 - headRx} 58 Q{60 - headRx} {62 - headRy} 60 {62 - headRy} Q{60 + headRx} {62 - headRy} {60 + headRx} 58 Q{60 + headRx - 6} 50 60 50 Q{60 - headRx + 6} 50 {60 - headRx} 58 Z\" fill=\"{hairColor}\"/>");
        }

        sb.Append("</g>");

        if (sim.IsPlayerControlled)
            sb.Append("<rect x=\"6\" y=\"6\" width=\"12\" height=\"12\" fill=\"#67e8f9\" transform=\"rotate(45 12 12)\"/>");

        sb.Append("</svg>");
        return sb.ToString();
    }

    public static string RenderFallback(string label) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 120 160\" width=\"120\" height=\"160\"><rect width=\"120\" height=\"160\" fill=\"#0b1520\"/><text x=\"60\" y=\"84\" text-anchor=\"middle\" fill=\"#cbd5e1\" font-family=\"system-ui, sans-serif\" font-size=\"11\">{Escape(label)}</text></svg>";

    private static string MouthPath(string expression, string eyeColor)
    {
        var stroke = expression switch
        {
            "expression-warm" => "#7f1d1d",
            "expression-alert" => "#7f1d1d",
            _ => "#5b3a24"
        };
        return expression switch
        {
            "expression-warm" => $"<path d=\"M54 72 Q60 78 66 72\" stroke=\"{stroke}\" stroke-width=\"2\" fill=\"none\"/>",
            "expression-serious" => $"<path d=\"M54 74 L66 74\" stroke=\"{stroke}\" stroke-width=\"2\"/>",
            "expression-alert" => $"<circle cx=\"60\" cy=\"74\" r=\"2.5\" fill=\"{stroke}\"/>",
            _ => $"<path d=\"M54 73 Q60 76 66 73\" stroke=\"{stroke}\" stroke-width=\"2\" fill=\"none\"/>"
        };
    }

    private static (int Rx, int Ry) FaceSize(string part) => part switch
    {
        "face-round" => (24, 24),
        "face-square" => (22, 22),
        "face-heart" => (24, 26),
        "face-long" => (19, 29),
        "face-fallback" => (22, 25),
        _ => (22, 26)
    };

    private static int BodyWidth(string part) =>
        part.Contains("sturdy", StringComparison.Ordinal) ? 34 :
        part.Contains("slim", StringComparison.Ordinal) ? 24 : 29;

    private static string HairColorFor(string hairPart)
    {
        foreach (var pair in HairColors)
        {
            if (hairPart.EndsWith(pair.Key, StringComparison.Ordinal))
                return pair.Value;
        }
        return "#5b3a24";
    }

    private static string Resolve(
        IReadOnlyDictionary<string, string> colors,
        string key,
        string fallback) =>
        colors.TryGetValue(key, out var color) ? color : fallback;

    private static string Escape(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;", StringComparison.Ordinal)
                   .Replace("<", "&lt;", StringComparison.Ordinal)
                   .Replace(">", "&gt;", StringComparison.Ordinal)
                   .Replace("\"", "&quot;", StringComparison.Ordinal);
}
