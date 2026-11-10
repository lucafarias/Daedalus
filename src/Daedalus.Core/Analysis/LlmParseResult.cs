namespace Daedalus.Core.Analysis;

/// <summary>
/// Immutable result of parsing an LLM response.
/// </summary>
/// <remarks>
/// Carries the extracted code, the classification, the detected language
/// tag of the code fence (informational), and the optional review notes.
/// The review notes are best-effort: their absence does not invalidate
/// the result.
/// </remarks>
public sealed class LlmParseResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LlmParseResult"/> class.
    /// </summary>
    /// <param name="extractedContent">The code extracted from the fenced block.</param>
    /// <param name="outcome">Whether the content differs from the original.</param>
    /// <param name="detectedLanguage">
    /// The language tag declared on the opening fence, or an empty string
    /// when no tag was present.
    /// </param>
    /// <param name="reviewNotes">
    /// The text under the "Architectural Review Notes" heading, or <c>null</c>
    /// when the section is absent or empty.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="extractedContent"/> or <paramref name="detectedLanguage"/> is <c>null</c>.
    /// </exception>
    public LlmParseResult(
        string extractedContent,
        LlmParseOutcome outcome,
        string detectedLanguage,
        string? reviewNotes)
    {
        ArgumentNullException.ThrowIfNull(extractedContent);
        ArgumentNullException.ThrowIfNull(detectedLanguage);

        ExtractedContent = extractedContent;
        Outcome = outcome;
        DetectedLanguage = detectedLanguage;
        ReviewNotes = reviewNotes;
    }

    /// <summary>
    /// Gets the code extracted from the fenced block.
    /// </summary>
    public string ExtractedContent { get; }

    /// <summary>
    /// Gets the classification of the response.
    /// </summary>
    public LlmParseOutcome Outcome { get; }

    /// <summary>
    /// Gets the language tag declared on the opening fence, or an empty string.
    /// </summary>
    public string DetectedLanguage { get; }

    /// <summary>
    /// Gets the review notes, or <c>null</c> when none were provided.
    /// </summary>
    public string? ReviewNotes { get; }
}