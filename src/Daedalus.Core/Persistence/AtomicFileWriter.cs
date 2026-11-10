using Daedalus.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Daedalus.Core.Persistence;

/// <summary>
/// Writes file content atomically, creating a backup of the previous content
/// when the file already exists.
/// </summary>
/// <remarks>
/// <para>
/// The write sequence is:
/// </para>
/// <list type="number">
///   <item>If the target exists, create a backup via <see cref="BackupService"/>.</item>
///   <item>Write the new content to a temporary file in the same directory.</item>
///   <item>If the target exists, call <c>File.Replace</c> (atomic on NTFS);
///   otherwise call <c>File.Move(overwrite: true)</c>.</item>
/// </list>
/// <para>
/// On any failure, the temporary file is deleted and the original file is
/// left untouched. The backup, if created, is retained.
/// </para>
/// <para>
/// The class is stateless apart from injected dependencies and safe for
/// concurrent calls on <b>different</b> paths. Concurrent writes to the
/// <b>same</b> path are the caller's responsibility to serialize.
/// </para>
/// </remarks>
public sealed class AtomicFileWriter
{
    private const int StreamBufferSize = 81920;

    private readonly BackupService _backupService;
    private readonly ILogger<AtomicFileWriter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AtomicFileWriter"/> class.
    /// </summary>
    /// <param name="backupService">Backup service used before overwriting.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">Any parameter is <c>null</c>.</exception>
    public AtomicFileWriter(
        BackupService backupService,
        ILogger<AtomicFileWriter> logger)
    {
        ArgumentNullException.ThrowIfNull(backupService);
        ArgumentNullException.ThrowIfNull(logger);

        _backupService = backupService;
        _logger = logger;
    }

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/> atomically.
    /// </summary>
    /// <param name="path">Absolute path of the target file.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="solutionRoot">
    /// Absolute path of the solution root, used to compute the backup path.
    /// Ignored when the target file does not exist.
    /// </param>
    /// <param name="sessionId">Identifier of the current session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/>, <paramref name="solutionRoot"/>, or
    /// <paramref name="sessionId"/> is <c>null</c>, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="content"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// The parent directory of <paramref name="path"/> does not exist.
    /// </exception>
    /// <exception cref="FileWriteException">
    /// The backup or the atomic replace fails.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    public async Task WriteAsync(
        string path,
        string content,
        string solutionRoot,
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        cancellationToken.ThrowIfCancellationRequested();

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"The parent directory of '{path}' does not exist.");
        }

        bool targetExists = File.Exists(path);

        if (targetExists)
        {
            await _backupService
                .CreateBackupAsync(path, solutionRoot, sessionId, cancellationToken)
                .ConfigureAwait(false);
        }

        string temporaryPath = Path.Combine(directory, Path.GetRandomFileName());

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                StreamBufferSize,
                useAsync: true))
            {
                await using StreamWriter writer = new(stream);

                await writer
                    .WriteAsync(content.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);

                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (targetExists)
            {
                File.Replace(
                    sourceFileName: temporaryPath,
                    destinationFileName: path,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);

                _logger.LogInformation(
                    "Replaced {FilePath} atomically (backup created).", path);
            }
            else
            {
                File.Move(temporaryPath, path, overwrite: false);

                _logger.LogInformation(
                    "Created {FilePath} (no previous content to back up).", path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemporaryFile(temporaryPath);

            _logger.LogError(
                ex,
                "Failed to write {FilePath} atomically.", path);

            throw new FileWriteException(
                path,
                "the atomic replace failed",
                ex);
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    private void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(
                ex,
                "Could not delete the temporary file {TemporaryPath}.",
                temporaryPath);
        }
    }
}