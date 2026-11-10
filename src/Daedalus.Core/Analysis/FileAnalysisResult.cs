using DiffPlex.DiffBuilder.Model;

namespace Daedalus.Core.Analysis;

/// <summary>
/// Immutable result of analyzing a single file.
/// </summary>
/// <remarks>
/// Use the static factory methods (<see cref="Proposed"/>, <see cref="NoChanges"/>,
/// <see cref="Failed"/>, <see cref="Skipped"/>) to construct instances.
/// The private constructor prevents building inconsistent states, such as a
/// <see cref="FileAnalysisStatus.ProposedChanges"/> result without content
/// or without a diff model.
/// </remarks>
public sealed class FileAnalysisResult
{
    private FileAnalysisResult(
        string filePath,
        FileAnalysisStatus status,
        string? originalContent,
        string? proposedContent,
        SideBySideDiffModel? diffModel,
        string? reviewNotes,
        string? errorMessage,
        string? skipReason)
    {
        FilePath = filePath;
        Status = status;
        OriginalContent = originalContent;
        ProposedContent = proposedContent;
        DiffModel = diffModel;
        ReviewNotes = reviewNotes;
        ErrorMessage = errorMessage;
        SkipReason = skipReason;
    }

    /// <summary>
    /// Gets the absolute path of the analyzed file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the final state of the analysis.
    /// </summary>
    public FileAnalysisStatus Status { get; }

    /// <summary>
    /// Gets the original content of the file, when it was read successfully.
    /// </summary>
    public string? OriginalContent { get; }

    /// <summary>
    /// Gets the content proposed by the model, when
    /// <see cref="Status"/> is <see cref="FileAnalysisStatus.ProposedChanges"/>.
    /// </summary>
    public string? ProposedContent { get; }

    /// <summary>
    /// Gets the side-by-side diff model, when
    /// <see cref="Status"/> is <see cref="FileAnalysisStatus.ProposedChanges"/>.
    /// </summary>
    public SideBySideDiffModel? DiffModel { get; }

    /// <summary>
    /// Gets the review notes returned by the model, when available.
    /// </summary>
    public string? ReviewNotes { get; }

    /// <summary>
    /// Gets the error message, when <see cref="Status"/> is
    /// <see cref="FileAnalysisStatus.Failed"/>.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the reason for skipping, when <see cref="Status"/> is
    /// <see cref="FileAnalysisStatus.Skipped"/>.
    /// </summary>
    public string? SkipReason { get; }

    /// <summary>
    /// Creates a result for a file with proposed changes.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any required argument is <c>null</c>.</exception>
    public static FileAnalysisResult Proposed(
        string filePath,
        string originalContent,
        string proposedContent,
        SideBySideDiffModel diffModel,
        string? reviewNotes)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(originalContent);
        ArgumentNullException.ThrowIfNull(proposedContent);
        ArgumentNullException.ThrowIfNull(diffModel);

        return new FileAnalysisResult(
            filePath,
            FileAnalysisStatus.ProposedChanges,
            originalContent,
            proposedContent,
            diffModel,
            reviewNotes,
            errorMessage: null,
            skipReason: null);
    }

    /// <summary>
    /// Creates a result for a file that the model left unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any required argument is <c>null</c>.</exception>
    public static FileAnalysisResult NoChanges(
        string filePath,
        string originalContent,
        string? reviewNotes)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(originalContent);

        return new FileAnalysisResult(
            filePath,
            FileAnalysisStatus.NoChanges,
            originalContent,
            proposedContent: null,
            diffModel: null,
            reviewNotes,
            errorMessage: null,
            skipReason: null);
    }

    /// <summary>
    /// Creates a result for a file whose analysis failed.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any required argument is <c>null</c>.</exception>
    public static FileAnalysisResult Failed(string filePath, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(errorMessage);

        return new FileAnalysisResult(
            filePath,
            FileAnalysisStatus.Failed,
            originalContent: null,
            proposedContent: null,
            diffModel: null,
            reviewNotes: null,
            errorMessage,
            skipReason: null);
    }

    /// <summary>
    /// Creates a result for a file that was skipped before analysis.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any required argument is <c>null</c>.</exception>
    public static FileAnalysisResult Skipped(string filePath, string skipReason)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(skipReason);

        return new FileAnalysisResult(
            filePath,
            FileAnalysisStatus.Skipped,
            originalContent: null,
            proposedContent: null,
            diffModel: null,
            reviewNotes: null,
            errorMessage: null,
            skipReason);
    }
}