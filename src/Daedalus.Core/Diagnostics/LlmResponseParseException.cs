using System;

namespace Daedalus.Core.Diagnostics;

/// <summary>
/// Raised when the LLM response does not match the format expected by
/// <c>LlmResponseParser</c> (missing code block, malformed markers, etc.).
/// </summary>
public sealed class LlmResponseParseException : DaedalusException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LlmResponseParseException"/> class.
    /// </summary>
    public LlmResponseParseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmResponseParseException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public LlmResponseParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmResponseParseException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LlmResponseParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmResponseParseException"/> class
    /// with the file path and the raw response that failed to parse.
    /// </summary>
    /// <param name="filePath">Path of the file whose analysis produced the response.</param>
    /// <param name="rawResponse">The raw LLM response that could not be parsed.</param>
    public LlmResponseParseException(string filePath, string rawResponse)
        : base($"Failed to parse the LLM response for '{filePath}'.")
    {
        FilePath = filePath;
        RawResponse = rawResponse;
    }

    /// <summary>
    /// Gets the path of the file whose analysis produced the response.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Gets the raw LLM response that could not be parsed.
    /// </summary>
    public string? RawResponse { get; }
}