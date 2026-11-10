namespace Daedalus.Core.Analysis;

/// <summary>
/// Progress notification emitted after each file has been analyzed.
/// </summary>
/// <remarks>
/// Emitted once per file, after the result is finalized. The UI uses
/// <see cref="ProcessedCount"/> and <see cref="TotalCount"/> to drive a
/// determinate progress bar, and <see cref="Status"/> to color the entry
/// in the session log.
/// </remarks>
public sealed class AnalysisProgress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalysisProgress"/> class.
    /// </summary>
    /// <param name="filePath">Absolute path of the analyzed file.</param>
    /// <param name="status">Final state of the analysis for this file.</param>
    /// <param name="processedCount">Number of files completed so far.</param>
    /// <param name="totalCount">Total number of files to analyze.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="filePath"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="processedCount"/> or <paramref name="totalCount"/> is negative.
    /// </exception>
    public AnalysisProgress(
        string filePath,
        FileAnalysisStatus status,
        int processedCount,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (processedCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processedCount), processedCount, "Must be non-negative.");
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalCount), totalCount, "Must be non-negative.");
        }

        FilePath = filePath;
        Status = status;
        ProcessedCount = processedCount;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Gets the absolute path of the analyzed file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the final state of the analysis for this file.
    /// </summary>
    public FileAnalysisStatus Status { get; }

    /// <summary>
    /// Gets the number of files completed so far.
    /// </summary>
    public int ProcessedCount { get; }

    /// <summary>
    /// Gets the total number of files to analyze.
    /// </summary>
    public int TotalCount { get; }
}