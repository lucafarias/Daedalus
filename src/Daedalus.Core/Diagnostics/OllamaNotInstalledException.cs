namespace Daedalus.Core.Diagnostics;

/// <summary>
/// Raised when the Ollama executable cannot be located in any of the
/// discovery paths (configured path, PATH, %LOCALAPPDATA%, %ProgramFiles%).
/// </summary>
public sealed class OllamaNotInstalledException : DaedalusException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaNotInstalledException"/> class.
    /// </summary>
    public OllamaNotInstalledException()
        : this(Array.Empty<string>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaNotInstalledException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OllamaNotInstalledException(string message)
        : base(message)
    {
        SearchedPaths = Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaNotInstalledException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OllamaNotInstalledException(string message, Exception innerException)
        : base(message, innerException)
    {
        SearchedPaths = Array.Empty<string>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaNotInstalledException"/> class
    /// with the list of paths that were probed during discovery.
    /// </summary>
    /// <param name="searchedPaths">Locations that were checked for <c>ollama.exe</c>.</param>
    public OllamaNotInstalledException(IReadOnlyList<string> searchedPaths)
        : base(BuildMessage(searchedPaths))
    {
        SearchedPaths = searchedPaths ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets the locations that were checked for <c>ollama.exe</c>.
    /// </summary>
    public IReadOnlyList<string> SearchedPaths { get; }

    private static string BuildMessage(IReadOnlyList<string> searchedPaths)
    {
        const string summary =
            "Ollama is not installed. Download it from https://ollama.com/download.";

        return searchedPaths is { Count: > 0 }
            ? $"{summary} Searched paths: {string.Join(", ", searchedPaths)}"
            : summary;
    }
}