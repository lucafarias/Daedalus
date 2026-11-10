namespace Daedalus.Core.Prompting;

/// <summary>
/// Immutable result of a successful prompt load.
/// </summary>
/// <remarks>
/// Carries the resolved content, the path it was read from, and whether
/// the default prompt shipped with the application was used. The path and
/// flag are surfaced so the caller can log and display exactly which prompt
/// is about to be sent to the model.
/// </remarks>
public sealed class PromptLoadResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromptLoadResult"/> class.
    /// </summary>
    /// <param name="content">The full prompt content.</param>
    /// <param name="sourcePath">Absolute path the content was read from.</param>
    /// <param name="isDefault">
    /// <c>true</c> when the shipped default prompt was used;
    /// <c>false</c> when a user-supplied prompt was loaded.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="content"/> or <paramref name="sourcePath"/> is <c>null</c>.
    /// </exception>
    public PromptLoadResult(string content, string sourcePath, bool isDefault)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourcePath);

        Content = content;
        SourcePath = sourcePath;
        IsDefault = isDefault;
    }

    /// <summary>
    /// Gets the full prompt content.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Gets the absolute path the prompt was read from.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets a value indicating whether the shipped default prompt was used.
    /// </summary>
    public bool IsDefault { get; }
}