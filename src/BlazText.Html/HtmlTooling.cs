using System.Text;
using AngleSharp.Html;
using AngleSharp.Html.Parser;
using BlazText.Models;

namespace BlazText.Html;

/// <summary>AngleSharp-backed HTML validation, formatting, and sanitization for document content.</summary>
public static class HtmlTooling
{
    /// <summary>Parses <paramref name="html"/> and reports parser errors as validation issues.</summary>
    public static HtmlValidationResult Validate(string html)
    {
        var result = new HtmlValidationResult();
        var parser = new HtmlParser(new HtmlParserOptions { IsStrictMode = false });

        parser.Error += (_, ev) =>
        {
            if (ev is AngleSharp.Html.Dom.Events.HtmlErrorEvent error)
            {
                result.Issues.Add(new ValidationIssue
                {
                    // The HTML5 parser recovers from everything, so parser errors are warnings:
                    // the content still renders, just possibly not as intended.
                    Severity = ValidationSeverity.Warning,
                    Message = error.Message,
                    Line = error.Position.Line,
                    Column = error.Position.Column,
                });
            }
        };

        parser.ParseDocument($"<!DOCTYPE html><html><body>{html}</body></html>");
        return result;
    }

    /// <summary>Pretty-prints document content (a body fragment).</summary>
    public static string Format(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument($"<!DOCTYPE html><html><body>{html}</body></html>");
        var writer = new StringWriter();
        var formatter = new PrettyMarkupFormatter { Indentation = "  ", NewLine = "\n" };

        foreach (var node in document.Body!.ChildNodes)
        {
            node.ToHtml(writer, formatter);
        }

        return writer.ToString().Trim();
    }

    /// <summary>
    /// Strips active content (scripts, event handlers, script URLs) for safe previewing.
    /// <c>&lt;style&gt;</c> blocks are deliberately kept, because e-mail templates need them.
    /// </summary>
    public static string Sanitize(string html)
    {
        // IsScripting must match the consumer's environment. The parser defaults to false, but a
        // browser has scripting on and treats <noscript> as raw text — so with the default, a
        // payload hidden inside what this parser reads as a <noscript> attribute round-trips
        // unexamined and becomes live markup when the output is parsed again.
        var parser = new HtmlParser(new HtmlParserOptions { IsScripting = true });
        var document = parser.ParseDocument($"<!DOCTYPE html><html><body>{html}</body></html>");

        // template content lives in a separate DocumentFragment that QuerySelectorAll does not
        // descend into, so it cannot be sanitized in place — drop it wholesale instead.
        foreach (var element in document.QuerySelectorAll("script, iframe, object, embed, form, base, link, template").ToList())
        {
            element.Remove();
        }

        // Only http-equiv is dangerous (refresh redirects). charset and viewport are needed:
        // EmailPreviewPlugin's mobile preview is meaningless without <meta name="viewport">.
        foreach (var element in document.QuerySelectorAll("meta[http-equiv]").ToList())
        {
            element.Remove();
        }

        // <style> is kept for e-mail templates, but only in the HTML namespace. In the SVG
        // namespace it is not a raw-text element, so entity-encoded text parses as a text node
        // that AngleSharp then serializes back unescaped — turning inert input into live markup.
        foreach (var element in document.QuerySelectorAll("style").ToList())
        {
            if (!string.Equals(element.NamespaceUri, HtmlNamespace, StringComparison.Ordinal))
            {
                element.Remove();
            }
        }

        foreach (var element in document.QuerySelectorAll("*"))
        {
            foreach (var attribute in element.Attributes.ToList())
            {
                var name = attribute.Name;
                var isEventHandler = name.StartsWith("on", StringComparison.OrdinalIgnoreCase);
                var isSrcDoc = name.Equals("srcdoc", StringComparison.OrdinalIgnoreCase);
                var isScriptUrl = UrlAttributes.Contains(name) && HasDangerousUrl(name, attribute.Value);

                if (isEventHandler || isSrcDoc || isScriptUrl)
                {
                    element.RemoveAttribute(name);
                }
            }
        }

        return document.Body!.InnerHtml;
    }

    private const string HtmlNamespace = "http://www.w3.org/1999/xhtml";

    /// <summary>Attributes whose value a browser resolves as a URL.</summary>
    private static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "src", "xlink:href", "action", "formaction", "data", "poster", "background", "srcset", "ping",
    };

    private static readonly string[] DangerousSchemes =
        ["javascript:", "vbscript:", "data:text/html", "data:application/xhtml"];

    private static readonly char[] Whitespace = [' ', '\t', '\n', '\r', '\f'];

    /// <summary>
    /// Checks an attribute value against its own grammar. Most URL attributes hold a single URL,
    /// but <c>srcset</c> is a comma-separated candidate list and <c>ping</c> a space-separated
    /// one — checking those as a single string only ever inspects the first entry.
    /// </summary>
    private static bool HasDangerousUrl(string name, string value) => name.ToLowerInvariant() switch
    {
        // IsDangerousUrl only looks at the prefix, so the trailing descriptor needs no trimming.
        "srcset" => value.Split(',').Any(IsDangerousUrl),
        "ping" => value.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries).Any(IsDangerousUrl),
        _ => IsDangerousUrl(value),
    };

    private static bool IsDangerousUrl(string value)
    {
        // A browser strips whitespace, control characters and zero-width characters out of a URL
        // before it resolves the scheme, so the value has to be normalized the same way first —
        // otherwise "java&#9;script:" walks straight past a StartsWith check and still executes.
        var normalized = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            var isNoise = ch <= 0x20 || ch == 0x7f || ch is >= (char)0x200b and <= (char)0x200d || ch == 0xfeff;

            if (!isNoise)
            {
                normalized.Append(ch);
            }
        }

        var url = normalized.ToString();
        return DangerousSchemes.Any(scheme => url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));
    }
}
