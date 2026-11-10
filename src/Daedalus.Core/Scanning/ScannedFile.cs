namespace Daedalus.Core.Scanning;

/// <summary>
/// A file discovered during a solution scan, with the metadata needed by the
/// orchestrator to decide whether to analyze it.
/// </summary>
/// <remarks>
/// Oversized files are returned rather than silently discarded, so the caller
/// can report them to the user and mark them as skipped with a reason.
/// </remarks>
public sealed class ScannedFile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScannedFile"/> class.
    /// </summary>
    /// <param name="path">Absolute path of the file.</param>
    /// <param name="sizeBytes">Size of the file, in bytes.</param>
    /// <param name="isOversized">
    /// <c>true</c> when the file exceeds the configured
    /// <see cref="Configuration.DaedalusOptions.MaxFileSizeBytes"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sizeBytes"/> is negative.
    /// </exception>
    public ScannedFile(string path, long sizeBytes, bool isOversized)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes), sizeBytes, "Size must be non-negative.");
        }

        Path = path;
        SizeBytes = sizeBytes;
        IsOversized = isOversized;
    }

    /// <summary>
    /// Gets the absolute path of the file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the size of the file, in bytes.
    /// </summary>
    public long SizeBytes { get; }

    /// <summary>
    /// Gets a value indicating whether the file exceeds the configured size limit.
    /// </summary>
    public bool IsOversized { get; }
}