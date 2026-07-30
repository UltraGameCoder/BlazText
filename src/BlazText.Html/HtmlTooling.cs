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

    /// <summary>Strips active content (scripts, event handlers, javascript: URLs) for safe previewing.</summary>
    public static string Sanitize(string html)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument($"<!DOCTYPE html><html><body>{html}</body></html>");

        foreach (var element in document.QuerySelectorAll("script, iframe, object, embed, form, base").ToList())
        {
            element.Remove();
        }

        foreach (var element in document.QuerySelectorAll("*"))
        {
            foreach (var attribute in element.Attributes.ToList())
            {
                var name = attribute.Name;
                var isEventHandler = name.StartsWith("on", StringComparison.OrdinalIgnoreCase);
                var isScriptUrl = name is "href" or "src"
                    && attribute.Value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);

                if (isEventHandler || isScriptUrl)
                {
                    element.RemoveAttribute(name);
                }
            }
        }

        return document.Body!.InnerHtml;
    }
}
