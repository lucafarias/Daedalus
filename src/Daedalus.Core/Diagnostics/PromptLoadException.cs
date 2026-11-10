namespace Daedalus.Core.Diagnostics;

/// <summary>
/// Raised when the prompt file cannot be loaded: it does not exist,
/// cannot be read, or contains no usable content.
/// </summary>
/// <remarks>
/// A single exception type covers all three failure modes because the
/// remediation is identical from the user's perspective: fix the prompt
/// configuration and retry. The specific reason is carried in the message.
/// </remarks>
public sealed class PromptLoadException : DaedalusException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromptLoadException"/> class.
    /// </summary>
    public PromptLoadException()
        : base("Failed to load the prompt file.")
    {
        PromptPath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptLoadException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PromptLoadException(string message)
        : base(message)
    {
        PromptPath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptLoadException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PromptLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
        PromptPath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptLoadException"/> class
    /// for a specific prompt file, without an inner exception.
    /// </summary>
    /// <param name="promptPath">Path of the prompt file that could not be loaded.</param>
    /// <param name="reason">Human-readable reason for the failure.</param>
    public PromptLoadException(string promptPath, string reason)
        : base($"Failed to load the prompt file '{promptPath}': {reason}.")
    {
        PromptPath = promptPath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptLoadException"/> class
    /// for a specific prompt file, with an inner exception.
    /// </summary>
    /// <param name="promptPath">Path of the prompt file that could not be loaded.</param>
    /// <param name="reason">Human-readable reason for the failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PromptLoadException(string promptPath, string reason, Exception innerException)
        : base($"Failed to load the prompt file '{promptPath}': {reason}.", innerException)
    {
        PromptPath = promptPath;
    }

    /// <summary>
    /// Gets the path of the prompt file that could not be loaded,
    /// or an empty string when the exception was constructed without a path.
    /// </summary>
    public string PromptPath { get; }
}