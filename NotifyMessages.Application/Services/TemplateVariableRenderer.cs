using System.Text.RegularExpressions;

namespace NotifyMessages.Application.Services;

// Variáveis {{Nome}} e blocos condicionais (estilo Mustache, sem lógica):
//   {{#Var}} ... {{/Var}}  mostrado só quando Var tem valor (não vazio);
//   {{^Var}} ... {{/Var}}  mostrado só quando Var não tem valor (ou não existe).
// Permite duas versões do mesmo template (ex. com e sem referência Multibanco). Os blocos podem estar dentro de
// outros blocos de variáveis diferentes. Nomes sem distinguir maiúsculas.
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

        var result = RenderSections(template, variables);
        foreach (var kvp in variables)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", html ? EscaparHtml(kvp.Value) : kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    // Resolve os blocos de dentro para fora: cada passagem trata os que não têm outro bloco lá dentro.
    private static string RenderSections(string template, IDictionary<string, string> variables)
    {
        var valores = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
        string atual = template;
        for (int i = 0; i < 20; i++)
        {
            string seguinte = SectionPattern().Replace(atual, m =>
            {
                bool temValor = valores.TryGetValue(m.Groups["nome"].Value, out var v) && !string.IsNullOrWhiteSpace(v);
                bool mostrar = m.Groups["tipo"].Value == "#" ? temValor : !temValor;
                return mostrar ? m.Groups["conteudo"].Value : string.Empty;
            });
            if (seguinte == atual)
            {
                break;
            }
            atual = seguinte;
        }
        return atual;
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

    // Variáveis usadas no template, incluindo as que só controlam blocos ({{#Var}}, {{^Var}}).
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
            foreach (Match match in SectionTagPattern().Matches(template))
            {
                names.Add(match.Groups[1].Value);
            }
        }
        return names.ToList();
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariablePattern();

    [GeneratedRegex(@"\{\{[#^](\w+)\}\}")]
    private static partial Regex SectionTagPattern();

    // Um bloco sem outro bloco de abertura lá dentro (os interiores resolvem-se primeiro)
    [GeneratedRegex(@"\{\{(?<tipo>[#^])(?<nome>\w+)\}\}(?<conteudo>(?:(?!\{\{[#^]).)*?)\{\{/\k<nome>\}\}", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SectionPattern();
}
