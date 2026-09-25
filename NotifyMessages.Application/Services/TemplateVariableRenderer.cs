using System.Text.RegularExpressions;

namespace NotifyMessages.Application.Services;

public static partial class TemplateVariableRenderer
{
    public static string Render(string? template, IDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
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
