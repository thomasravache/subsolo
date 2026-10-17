using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Subsolo.Notifications.UnitTests.Infrastructure;

/// <summary>Leitura do HTML do e-mail como um cliente de e-mail veria: texto visível, botão do link e cores inline.</summary>
internal static partial class EmailHtml
{
    /// <summary>Texto visível: sem tags, entidades decodificadas e espaços colapsados.</summary>
    public static string VisibleText(string html) =>
        Whitespace().Replace(WebUtility.HtmlDecode(Tag().Replace(html, " ")), " ").Trim();

    /// <summary>
    /// O <c>&lt;a&gt;</c> cujo <c>href</c> (decodificado) é exatamente <paramref name="link"/>: os atributos da tag de
    /// abertura e o HTML interno; <see langword="null"/> se não houver.
    /// </summary>
    public static (string Attributes, string Inner)? AnchorTo(string html, string link)
    {
        foreach (Match anchor in Anchor().Matches(html))
        {
            var attributes = anchor.Groups["attrs"].Value;
            var href = Href().Match(attributes);
            if (href.Success && WebUtility.HtmlDecode(href.Groups["value"].Value) == link)
            {
                return (attributes, anchor.Groups["inner"].Value);
            }
        }

        return null;
    }

    /// <summary>Declarações do atributo <c>style</c> (propriedade em minúsculas → valor).</summary>
    public static Dictionary<string, string> InlineStyle(string attributes)
    {
        var style = Style().Match(attributes);
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!style.Success)
        {
            return declarations;
        }

        foreach (var declaration in WebUtility.HtmlDecode(style.Groups["value"].Value).Split(';'))
        {
            var parts = declaration.Split(':', 2);
            if (parts.Length == 2)
            {
                declarations[parts[0].Trim().ToLowerInvariant()] = parts[1].Trim();
            }
        }

        return declarations;
    }

    /// <summary>A primeira cor hexadecimal (<c>#rgb</c> ou <c>#rrggbb</c>) de <paramref name="value"/>, ou <see langword="null"/>.</summary>
    public static (double R, double G, double B)? HexColor(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var match = Hex().Match(value);
        if (!match.Success)
        {
            return value.Contains("white", StringComparison.OrdinalIgnoreCase) ? (255, 255, 255) : null;
        }

        var hex = match.Groups[1].Value;
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        }

        return (Channel(hex, 0), Channel(hex, 2), Channel(hex, 4));
    }

    /// <summary>Razão de contraste do WCAG 2.x entre duas cores (de 1 a 21).</summary>
    public static double ContrastRatio((double R, double G, double B) first, (double R, double G, double B) second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Channel(string hex, int start) =>
        int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static double Luminance((double R, double G, double B) color) =>
        (0.2126 * Linear(color.R)) + (0.7152 * Linear(color.G)) + (0.0722 * Linear(color.B));

    private static double Linear(double channel)
    {
        var c = channel / 255;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"<a\b(?<attrs>[^>]*)>(?<inner>.*?)</a\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Anchor();

    [GeneratedRegex("""\bhref\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    [GeneratedRegex("""\bstyle\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.IgnoreCase)]
    private static partial Regex Style();

    [GeneratedRegex("#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})(?![0-9a-fA-F])")]
    private static partial Regex Hex();
}
