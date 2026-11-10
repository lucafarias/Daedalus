using Daedalus.Core.Configuration;
using Daedalus.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daedalus.Core.Prompting;

/// <summary>
/// Loads the prompt that will be sent to the LLM, either from a user-supplied
/// file or from the default file shipped alongside the application.
/// </summary>
/// <remarks>
/// <para>
/// Resolution order:
/// </para>
/// <list type="number">
///   <item><see cref="DaedalusOptions.PromptFilePath"/> when set (absolute path,
///   validated at startup);</item>
///   <item><see cref="DaedalusOptions.DefaultPromptFileName"/> resolved against
///   <see cref="AppContext.BaseDirectory"/>.</item>
/// </list>
/// <para>
/// The provider does not cache. The orchestrator is expected to invoke
/// <see cref="LoadAsync"/> once at session start and reuse the result for
/// the entire session. Concurrent invocations are safe: the class is
/// stateless after construction and only reads from disk.
/// </para>
/// <para>
/// Every load emits an <c>Information</c> log entry with the resolved path
/// and content length, and (when Debug logging is enabled) a 500-character
/// preview. This makes the exact prompt used for a session reconstructible
/// from the log file, without requiring the UI.
/// </para>
/// </remarks>
public sealed class PromptProvider
{
    private const int PreviewLength = 500;

    private readonly ILogger<PromptProvider> _logger;
    private readonly string? _customPromptPath;
    private readonly string _defaultPromptPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptProvider"/> class.
    /// </summary>
    /// <param name="options">Validated application options.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">Any parameter is <c>null</c>.</exception>
    public PromptProvider(
        IOptions<DaedalusOptions> options,
        ILogger<PromptProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        DaedalusOptions daedalusOptions = options.Value;

        _customPromptPath = string.IsNullOrWhiteSpace(daedalusOptions.PromptFilePath)
            ? null
            : daedalusOptions.PromptFilePath;

        _defaultPromptPath = Path.Combine(
            AppContext.BaseDirectory,
            daedalusOptions.DefaultPromptFileName);
    }

    /// <summary>
    /// Loads the prompt from the resolved path.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PromptLoadResult"/> with the content, source path, and
    /// origin (default vs. custom).
    /// </returns>
    /// <exception cref="PromptLoadException">
    /// The file does not exist, cannot be read, or is empty.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    public async Task<PromptLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        string path = _customPromptPath ?? _defaultPromptPath;
        bool isDefault = _customPromptPath is null;

        string content;
        try
        {
            content = await File
                .ReadAllTextAsync(path, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            throw new PromptLoadException(path, "the file does not exist", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new PromptLoadException(path, "the parent directory does not exist", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PromptLoadException(path, "access to the file was denied", ex);
        }
        catch (IOException ex)
        {
            throw new PromptLoadException(
                path, "an I/O error occurred while reading the file", ex);
        }
        catch (ArgumentException ex)
        {
            throw new PromptLoadException(path, "the file path is not valid", ex);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new PromptLoadException(
                path, "the file is empty or contains only whitespace");
        }

        _logger.LogInformation(
            "Loaded {PromptSource} prompt from {PromptPath} ({PromptLength} characters).",
            isDefault ? "default" : "custom",
            path,
            content.Length);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            string preview = content.Length > PreviewLength
                ? content[..PreviewLength] + "..."
                : content;

            _logger.LogDebug(
                "Prompt preview:{NewLine}{Preview}",
                Environment.NewLine,
                preview);
        }

        return new PromptLoadResult(content, path, isDefault);
    }
}