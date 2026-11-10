namespace Daedalus.Core.Analysis;

/// <summary>
/// Classification of the LLM response after parsing.
/// </summary>
public enum LlmParseOutcome
{
    /// <summary>
    /// The extracted code block differs from the original file content.
    /// The file should enter the review queue.
    /// </summary>
    ProposedChanges,

    /// <summary>
    /// The extracted code block is identical to the original file content.
    /// The file should be filtered out of the review queue.
    /// </summary>
    NoChanges
}