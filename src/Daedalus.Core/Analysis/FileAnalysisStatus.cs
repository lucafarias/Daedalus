namespace Daedalus.Core.Analysis;

/// <summary>
/// Final state of a single file after analysis.
/// </summary>
public enum FileAnalysisStatus
{
    /// <summary>
    /// The model proposed content that differs from the original. The file
    /// enters the review queue.
    /// </summary>
    ProposedChanges,

    /// <summary>
    /// The model returned content equivalent to the original. The file is
    /// filtered out of the review queue.
    /// </summary>
    NoChanges,

    /// <summary>
    /// The analysis failed (I/O error, HTTP error, parse error, timeout).
    /// The file is reported in the session summary with the error message.
    /// </summary>
    Failed,

    /// <summary>
    /// The file was skipped before analysis (oversized).
    /// </summary>
    Skipped
}