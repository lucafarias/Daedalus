using Daedalus.Core.Configuration;
using Daedalus.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daedalus.Core.Persistence;

/// <summary>
/// Manages session-scoped backups of files before they are overwritten.
/// </summary>
/// <remarks>
/// <para>
/// Backups are stored under
/// <c>{BackupDirectory}/{sessionId}/{relativePath}</c>, where
/// <c>relativePath</c> mirrors the file's location under the solution root.
/// This preserves the original directory structure and prevents name
/// collisions between files with the same name in different folders.
/// </para>
/// <para>
/// The class is stateless apart from injected dependencies; the session
/// identifier is passed to each call by the caller, which keeps the service
/// safe for concurrent use across sessions.
/// </para>
/// <para>
/// Files whose path is outside the solution root are backed up under a
/// hashed name, so the relative-path computation never throws.
/// </para>
/// </remarks>
public sealed class BackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupRoot;
    private readonly int _retentionDays;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupService"/> class.
    /// </summary>
    /// <param name="options">Validated application options.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">Any parameter is <c>null</c>.</exception>
    public BackupService(
        IOptions<DaedalusOptions> options,
        ILogger<BackupService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        DaedalusOptions daedalusOptions = options.Value;

        _backupRoot = daedalusOptions.BackupDirectory;
        _retentionDays = daedalusOptions.BackupRetentionDays;
    }

    /// <summary>
    /// Copies <paramref name="originalPath"/> into the session backup directory,
    /// preserving its relative location under <paramref name="solutionRoot"/>.
    /// </summary>
    /// <param name="originalPath">Absolute path of the file to back up.</param>
    /// <param name="solutionRoot">Absolute path of the solution root.</param>
    /// <param name="sessionId">Identifier of the current session.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The absolute path of the backup file.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="originalPath"/> or <paramref name="solutionRoot"/> is
    /// <c>null</c>, empty, or whitespace; <paramref name="sessionId"/> is
    /// <c>null</c>, empty, or whitespace.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// <paramref name="originalPath"/> does not exist.
    /// </exception>
    /// <exception cref="FileWriteException">
    /// The backup copy fails due to permissions, locks, or I/O errors.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    public async Task<string> CreateBackupAsync(
        string originalPath,
        string solutionRoot,
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!File.Exists(originalPath))
        {
            throw new FileNotFoundException(
                $"The file to back up does not exist: '{originalPath}'.",
                originalPath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        string relativePath = ComputeRelativePath(originalPath, solutionRoot);
        string backupPath = Path.Combine(_backupRoot, sessionId, relativePath);
        string? backupDirectory = Path.GetDirectoryName(backupPath);

        try
        {
            if (!string.IsNullOrEmpty(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            await using FileStream source = new(
                originalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            await using FileStream destination = new(
                backupPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(
                ex,
                "Failed to back up {OriginalPath} to {BackupPath}.",
                originalPath,
                backupPath);

            throw new FileWriteException(
                originalPath,
                "the backup copy could not be created",
                ex);
        }

        _logger.LogDebug(
            "Backed up {OriginalPath} to {BackupPath}.",
            originalPath,
            backupPath);

        return backupPath;
    }

    /// <summary>
    /// Deletes backups older than the configured retention period.
    /// When <see cref="DaedalusOptions.BackupRetentionDays"/> is zero, all
    /// session directories are removed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the cleanup finishes.</returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    public Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_backupRoot))
        {
            return Task.CompletedTask;
        }

        DateTime threshold = DateTime.UtcNow.AddDays(-_retentionDays);

        return Task.Run(
            () =>
            {
                foreach (string sessionDirectory in Directory.EnumerateDirectories(_backupRoot))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DirectoryInfo info = new(sessionDirectory);

                    if (_retentionDays == 0 || info.CreationTimeUtc < threshold)
                    {
                        TryDeleteDirectory(sessionDirectory);
                    }
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Deletes the backup directory of a specific session.
    /// Used when the user chooses to discard backups at the end of a session.
    /// </summary>
    /// <param name="sessionId">Identifier of the session whose backups should be removed.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="sessionId"/> is <c>null</c>, empty, or whitespace.
    /// </exception>
    public void DeleteSessionBackups(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        string sessionDirectory = Path.Combine(_backupRoot, sessionId);

        if (Directory.Exists(sessionDirectory))
        {
            TryDeleteDirectory(sessionDirectory);
        }
    }

    private static string ComputeRelativePath(string filePath, string solutionRoot)
    {
        string relative = Path.GetRelativePath(solutionRoot, filePath);

        // Files outside the solution root would produce paths containing "..".
        // Reject them and fall back to a hashed name to avoid escaping the
        // backup directory.
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            string fileName = Path.GetFileName(filePath);
            string hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(filePath)));

            return Path.Combine("_external", $"{hash[..16]}_{fileName}");
        }

        return relative;
    }

    private void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            Directory.Delete(directoryPath, recursive: true);

            _logger.LogDebug(
                "Deleted backup directory {DirectoryPath}.", directoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Could not delete backup directory {DirectoryPath}.",
                directoryPath);
        }
    }
}