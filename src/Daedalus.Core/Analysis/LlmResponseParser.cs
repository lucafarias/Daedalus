using System;
using Daedalus.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Daedalus.Core.Analysis;

/// <summary>
/// Parses the raw response returned by the LLM into a structured result.
/// </summary>
/// <remarks>
/// <para>
/// Expected response format (enforced by the prompt, not by this parser):
/// </para>
/// <code>
/// ```&lt;language&gt;
/// &lt;full file content&gt;
/// ```
///
/// ### 📝 Architectural Review Notes
/// - point 1
/// - point 2
/// </code>
/// <para>
/// The parser is intentionally lenient on optional parts: the review notes
/// section is best-effort, and the language tag is informational. It is
/// strict on the mandatory part: a fenced code block must be present and
/// closed, and the extracted content must be consistent with the original.
/// </para>
/// <para>
/// The "no changes" decision is based on a normalized comparison between the
/// extracted code and the original file content, <b>not</b> on a localized
/// marker string. This removes the dependency on the language of the prompt.
/// </para>
/// <para>
/// The class is stateless and thread-safe.
/// </para>
/// </remarks>
public sealed class LlmResponseParser
{
    private const string ReviewNotesHeader = "Architectural Review Notes";
    private const string CodeFence = "```";

    private readonly ILogger<LlmResponseParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmResponseParser"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public LlmResponseParser(ILogger<LlmResponseParser> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Parses a raw LLM response.
    /// </summary>
    /// <param name="rawResponse">The text returned by the LLM.</param>
    /// <param name="originalContent">
    /// The original content of the file, used to decide between
    /// <see cref="LlmParseOutcome.NoChanges"/> and
    /// <see cref="LlmParseOutcome.ProposedChanges"/>.
    /// </param>
    /// <param name="filePath">
    /// Path of the file being analyzed, used only in error messages and logs.
    /// </param>
    /// <param name="expectedExtension">
    /// Extension of the file being analyzed (e.g. <c>.cs</c>), used only to
    /// emit a debug log when the detected language tag does not match.
    /// </param>
    /// <returns>A structured <see cref="LlmParseResult"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="rawResponse"/>, <paramref name="filePath"/>, or
    /// <paramref name="expectedExtension"/> is <c>null</c>, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="originalContent"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="LlmResponseParseException">
    /// The response contains no closed fenced code block, or the extracted
    /// content is empty while the original is not.
    /// </exception>
    public LlmParseResult Parse(
        string rawResponse,
        string originalContent,
        string filePath,
        string expectedExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawResponse);
        ArgumentNullException.ThrowIfNull(originalContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedExtension);

        if (!TryExtractCodeBlock(
                rawResponse,
                out string language,
                out string extracted,
                out int codeEndIndex))
        {
            _logger.LogWarning(
                "No closed fenced code block found in the LLM response for {FilePath}.",
                filePath);

            throw new LlmResponseParseException(filePath, rawResponse);
        }

        if (extracted.Length == 0 && originalContent.Length > 0)
        {
            _logger.LogWarning(
                "The LLM returned an empty code block for {FilePath}, "
                + "but the original file is not empty.",
                filePath);

            throw new LlmResponseParseException(filePath, rawResponse);
        }

        string? reviewNotes = ExtractReviewNotes(rawResponse, codeEndIndex);

        LlmParseOutcome outcome = AreEquivalent(extracted, originalContent)
            ? LlmParseOutcome.NoChanges
            : LlmParseOutcome.ProposedChanges;

        LogLanguageMismatchIfAny(language, expectedExtension, filePath);

        _logger.LogDebug(
            "Parsed LLM response for {FilePath}: outcome={Outcome}, "
            + "language={Language}, contentLength={ContentLength}, hasNotes={HasNotes}.",
            filePath,
            outcome,
            language,
            extracted.Length,
            reviewNotes is not null);

        return new LlmParseResult(extracted, outcome, language, reviewNotes);
    }

    private static bool TryExtractCodeBlock(
        string text,
        out string language,
        out string code,
        out int endIndex)
    {
        language = string.Empty;
        code = string.Empty;
        endIndex = 0;

        int openIndex = text.IndexOf(CodeFence, StringComparison.Ordinal);
        if (openIndex < 0)
        {
            return false;
        }

        int languageLineEnd = text.IndexOf('\n', openIndex);
        if (languageLineEnd < 0)
        {
            return false;
        }

        string declaredLanguage = text
            .Substring(openIndex + CodeFence.Length, languageLineEnd - openIndex - CodeFence.Length)
            .Trim();

        int codeStart = languageLineEnd + 1;

        int closeIndex = text.IndexOf(CodeFence, codeStart, StringComparison.Ordinal);
        if (closeIndex < 0)
        {
            return false;
        }

        int codeLength = closeIndex - codeStart;

        // Strip a single trailing newline (CRLF or LF) so that the extracted
        // content matches the original which never carries a trailing newline
        // introduced by the fence rendering.
        if (codeLength >= 2 && text[codeStart + codeLength - 1] == '\n'
            && text[codeStart + codeLength - 2] == '\r')
        {
            codeLength -= 2;
        }
        else if (codeLength >= 1 && text[codeStart + codeLength - 1] == '\n')
        {
            codeLength -= 1;
        }

        language = declaredLanguage;
        code = text.Substring(codeStart, codeLength);
        endIndex = closeIndex + CodeFence.Length;
        return true;
    }

    private static string? ExtractReviewNotes(string text, int searchStart)
    {
        int headerIndex = text.IndexOf(
            ReviewNotesHeader,
            searchStart,
            StringComparison.OrdinalIgnoreCase);

        if (headerIndex < 0)
        {
            return null;
        }

        int lineEnd = text.IndexOf('\n', headerIndex);
        if (lineEnd < 0)
        {
            return null;
        }

        string notes = text.Substring(lineEnd + 1).Trim();
        return notes.Length == 0 ? null : notes;
    }

    private static bool AreEquivalent(string left, string right)
    {
        return string.Equals(
            NormalizeLineEndings(left),
            NormalizeLineEndings(right),
            StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    private void LogLanguageMismatchIfAny(
        string detectedLanguage,
        string expectedExtension,
        string filePath)
    {
        if (detectedLanguage.Length == 0)
        {
            return;
        }

        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        string expectedLanguage = expectedExtension.ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".xaml" => "xml",
            ".csproj" => "xml",
            ".json" => "json",
            _ => string.Empty
        };

        if (expectedLanguage.Length == 0)
        {
            return;
        }

        if (!string.Equals(detectedLanguage, expectedLanguage, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(detectedLanguage, "cs", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(detectedLanguage, "c#", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "The LLM declared language '{DetectedLanguage}' for {FilePath}, "
                + "expected '{ExpectedLanguage}'. Continuing anyway.",
                detectedLanguage,
                filePath,
                expectedLanguage);
        }
    }
}