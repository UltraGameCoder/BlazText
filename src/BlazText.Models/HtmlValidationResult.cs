namespace BlazText.Models;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>A single problem found while validating document HTML.</summary>
public class ValidationIssue
{
    public ValidationSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>1-based line in the HTML source, 0 when unknown.</summary>
    public int Line { get; set; }

    /// <summary>1-based column in the HTML source, 0 when unknown.</summary>
    public int Column { get; set; }
}

/// <summary>Outcome of validating a document's HTML content.</summary>
public class HtmlValidationResult
{
    public List<ValidationIssue> Issues { get; set; } = [];

    public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error);
}
