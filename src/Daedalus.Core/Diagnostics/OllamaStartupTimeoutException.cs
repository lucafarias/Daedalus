namespace Daedalus.Core.Diagnostics;

/// <summary>
/// Raised when the Ollama service does not become healthy within the
/// configured startup timeout after being launched by Daedalus.
/// </summary>
public sealed class OllamaStartupTimeoutException : DaedalusException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaStartupTimeoutException"/> class.
    /// </summary>
    public OllamaStartupTimeoutException()
        : this(timeoutSeconds: 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaStartupTimeoutException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OllamaStartupTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaStartupTimeoutException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OllamaStartupTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaStartupTimeoutException"/> class
    /// with the timeout value that was exceeded.
    /// </summary>
    /// <param name="timeoutSeconds">The timeout, in seconds, that elapsed before the check failed.</param>
    public OllamaStartupTimeoutException(int timeoutSeconds)
        : base(
            $"Ollama did not become healthy within {timeoutSeconds} second(s). " +
            "Verify the executable and try starting it manually.")
    {
        TimeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// Gets the timeout, in seconds, that was exceeded.
    /// </summary>
    public int TimeoutSeconds { get; }
}