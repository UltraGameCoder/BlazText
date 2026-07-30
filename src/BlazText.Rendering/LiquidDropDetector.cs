using System.Text.RegularExpressions;
using BlazText.Models;

namespace BlazText.Rendering;

/// <summary>
/// Heuristic detector for Liquid drop usage in template text. Works on partially written or
/// invalid templates (unlike a strict parse), which makes it usable while the user is typing.
/// </summary>
public static partial class LiquidDropDetector
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "and", "or", "contains", "in", "true", "false", "nil", "null", "empty", "blank",
        "forloop", "tablerowloop", "else", "elsif", "if", "endif", "unless", "endunless",
        "case", "endcase", "when", "for", "endfor", "assign", "capture", "endcapture",
        "with", "as", "limit", "offset", "reversed", "break", "continue", "increment",
        "decrement", "include", "render", "raw", "endraw", "comment", "endcomment",
        "echo", "liquid", "cycle", "tablerow", "endtablerow",
    };

    [GeneratedRegex(@"\{\{-?\s*(?<expr>.+?)\s*-?\}\}", RegexOptions.Singleline)]
    private static partial Regex OutputTag();

    [GeneratedRegex(@"\{%-?\s*(?<expr>.+?)\s*-?%\}", RegexOptions.Singleline)]
    private static partial Regex StatementTag();

    [GeneratedRegex(@"(?<![\w.'""])(?<path>[a-zA-Z_][a-zA-Z0-9_]*(?:\.[a-zA-Z_][a-zA-Z0-9_]*)*)")]
    private static partial Regex IdentifierPath();

    [GeneratedRegex(@"'[^']*'|""[^""]*""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"\|\s*[a-zA-Z_][a-zA-Z0-9_]*")]
    private static partial Regex FilterName();

    [GeneratedRegex(@"\bfor\s+(?<var>[a-zA-Z_][a-zA-Z0-9_]*)\s+in\b")]
    private static partial Regex ForLoopVariable();

    [GeneratedRegex(@"\b(?:assign|capture|increment|decrement)\s+(?<var>[a-zA-Z_][a-zA-Z0-9_]*)")]
    private static partial Regex AssignedVariable();

    /// <summary>Finds identifier paths used inside Liquid output and statement tags.</summary>
    public static List<DetectedDrop> Detect(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        // Locally introduced variables (loop vars, assigns) are not drops the developer supplies.
        var localNames = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        var expressions = new List<string>();

        foreach (Match match in StatementTag().Matches(content))
        {
            var expression = match.Groups["expr"].Value;

            foreach (Match loop in ForLoopVariable().Matches(expression))
            {
                localNames.Add(loop.Groups["var"].Value);
            }

            foreach (Match assigned in AssignedVariable().Matches(expression))
            {
                localNames.Add(assigned.Groups["var"].Value);
            }

            expressions.Add(expression);
        }

        foreach (Match match in OutputTag().Matches(content))
        {
            // Only the expression before the first filter pipe references drops directly;
            // filter arguments can too, so keep the whole expression and rely on keyword filtering.
            expressions.Add(match.Groups["expr"].Value);
        }

        foreach (var expression in expressions)
        {
            var withoutStrings = FilterName().Replace(StringLiteral().Replace(expression, " "), " ");

            foreach (Match identifier in IdentifierPath().Matches(withoutStrings))
            {
                var path = identifier.Groups["path"].Value;
                var root = path.Split('.', 2)[0];

                if (Keywords.Contains(root) || localNames.Contains(root))
                {
                    continue;
                }

                counts[path] = counts.GetValueOrDefault(path) + 1;
            }
        }

        return counts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new DetectedDrop
            {
                Name = pair.Key.Split('.', 2)[0],
                Path = pair.Key,
                Occurrences = pair.Value,
            })
            .ToList();
    }
}
