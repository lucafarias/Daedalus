using Daedalus.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;

namespace Daedalus.Core.Scanning;

/// <summary>
/// Enumerates the files of a solution that are eligible for analysis,
/// applying directory, pattern, extension, and size filters.
/// </summary>
/// <remarks>
/// <para>
/// The scan is a manual depth-first traversal. Each directory is enumerated
/// separately so that an inaccessible subdirectory affects only its own
/// subtree, not the whole scan. Access errors are logged at warning level
/// and do not abort the enumeration.
/// </para>
/// <para>
/// Directory exclusion is checked <b>before</b> entering a directory, so
/// excluded subtrees are never enumerated at all.
/// </para>
/// <para>
/// The result is materialized on the thread pool (via <see cref="Task.Run"/>)
/// and then yielded incrementally, so the calling thread (typically the UI)
/// is never blocked by filesystem I/O.
/// </para>
/// <para>
/// Instance state is immutable after construction; concurrent calls are safe.
/// </para>
/// </remarks>
public sealed class SolutionScanner
{
    private readonly ILogger<SolutionScanner> _logger;
    private readonly HashSet<string> _excludedDirectories;
    private readonly string[] _excludedFilePatterns;
    private readonly HashSet<string> _targetExtensions;
    private readonly HashSet<string> _targetFileNames;
    private readonly long _maxFileSizeBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SolutionScanner"/> class.
    /// </summary>
    /// <param name="options">Validated application options.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">Any parameter is <c>null</c>.</exception>
    public SolutionScanner(
        IOptions<DaedalusOptions> options,
        ILogger<SolutionScanner> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        DaedalusOptions daedalusOptions = options.Value;

        _excludedDirectories = new HashSet<string>(
            daedalusOptions.ExcludedDirectories,
            StringComparer.OrdinalIgnoreCase);

        _excludedFilePatterns = daedalusOptions.ExcludedFilePatterns;

        _targetExtensions = new HashSet<string>(
            daedalusOptions.TargetExtensions,
            StringComparer.OrdinalIgnoreCase);

        _targetFileNames = new HashSet<string>(
            daedalusOptions.TargetFileNames,
            StringComparer.OrdinalIgnoreCase);

        _maxFileSizeBytes = daedalusOptions.MaxFileSizeBytes;
    }

    /// <summary>
    /// Scans <paramref name="rootPath"/> and yields every file eligible for
    /// analysis, in deterministic path order.
    /// </summary>
    /// <param name="rootPath">Absolute path of the solution root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An asynchronous sequence of eligible files.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="rootPath"/> is <c>null</c>, empty, or whitespace.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// <paramref name="rootPath"/> does not exist.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    public async IAsyncEnumerable<ScannedFile> ScanAsync(
        string rootPath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        cancellationToken.ThrowIfCancellationRequested();

        string normalizedRoot = Path.GetFullPath(rootPath);

        if (!Directory.Exists(normalizedRoot))
        {
            throw new DirectoryNotFoundException(
                $"The solution root does not exist: '{normalizedRoot}'.");
        }

        List<ScannedFile> files = await Task
            .Run(() => EnumerateEligibleFiles(normalizedRoot, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Solution scan completed for {RootPath}: {FileCount} eligible file(s).",
            normalizedRoot,
            files.Count);

        foreach (ScannedFile file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return file;
        }
    }

    private List<ScannedFile> EnumerateEligibleFiles(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var result = new List<ScannedFile>();
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string current = pending.Pop();

            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(current).ToList();
            }
            catch (Exception ex) when (IsAccessFailure(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Skipping directory '{DirectoryPath}': it cannot be enumerated.",
                    current);
                continue;
            }

            foreach (string subdirectory in subdirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string name = Path.GetFileName(subdirectory);
                if (!_excludedDirectories.Contains(name))
                {
                    pending.Push(subdirectory);
                }
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current).ToList();
            }
            catch (Exception ex) when (IsAccessFailure(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Skipping files in '{DirectoryPath}': they cannot be enumerated.",
                    current);
                continue;
            }

            foreach (string filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsEligible(filePath))
                {
                    continue;
                }

                FileInfo info;
                try
                {
                    info = new FileInfo(filePath);
                    if (!info.Exists)
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (IsAccessFailure(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Skipping file '{FilePath}': metadata cannot be read.",
                        filePath);
                    continue;
                }

                bool oversized = info.Length > _maxFileSizeBytes;

                result.Add(new ScannedFile(filePath, info.Length, oversized));
            }
        }

        result.Sort(static (left, right) =>
            string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));

        return result;
    }

    private bool IsEligible(string filePath)
    {
        string fileName = Path.GetFileName(filePath);

        foreach (string pattern in _excludedFilePatterns)
        {
            if (FileSystemName.MatchesSimpleExpression(
                    pattern.AsSpan(),
                    fileName.AsSpan(),
                    ignoreCase: true))
            {
                return false;
            }
        }

        string extension = Path.GetExtension(filePath);
        if (_targetExtensions.Contains(extension))
        {
            return true;
        }

        return _targetFileNames.Contains(fileName);
    }

    private static bool IsAccessFailure(Exception exception) =>
        exception is UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or PathTooLongException
            or IOException;
}