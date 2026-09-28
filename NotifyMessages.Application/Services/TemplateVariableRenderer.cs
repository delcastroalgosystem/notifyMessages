using System.Text.RegularExpressions;

namespace NotifyMessages.Application.Services;

public static partial class TemplateVariableRenderer
{
    // Texto simples (assunto, SMS): os valores entram tal como vêm.
    public static string Render(string? template, IDictionary<string, string> variables)
        => Render(template, variables, html: false);

    // Corpo HTML: os valores são escapados (&, <, >, ", '), para que um nome como "Silva & Filhos" ou um valor
    // com "<" vindo da origem não parta o layout nem injete HTML. O HTML do próprio template não é tocado.
    // Os acentos e o € ficam como estão (o e-mail vai em UTF-8).
    public static string RenderHtml(string? template, IDictionary<string, string> variables)
        => Render(template, variables, html: true);

    private static string Render(string? template, IDictionary<string, string> variables, bool html)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", html ? EscaparHtml(kvp.Value) : kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    internal static string EscaparHtml(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return valor ?? string.Empty;
        }
        return valor
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    public static IReadOnlyList<string> ExtractVariableNames(params string?[] templates)
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            if (string.IsNullOrEmpty(template))
            {
                continue;
            }

            foreach (Match match in VariablePattern().Matches(template))
            {
                names.Add(match.Groups[1].Value);
            }
        }
        return names.ToList();
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariablePattern();
}
