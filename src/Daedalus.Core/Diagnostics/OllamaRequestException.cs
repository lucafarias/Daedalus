using System;
using System.Net;

namespace Daedalus.Core.Diagnostics;

/// <summary>
/// Raised when a request to Ollama fails with a non-success status code
/// or a network-level error.
/// </summary>
public sealed class OllamaRequestException : DaedalusException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaRequestException"/> class.
    /// </summary>
    public OllamaRequestException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaRequestException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public OllamaRequestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaRequestException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OllamaRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaRequestException"/> class
    /// with diagnostic context and no inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="statusCode">HTTP status code, if applicable.</param>
    /// <param name="responseBody">Response body, if any.</param>
    public OllamaRequestException(
        string message,
        HttpStatusCode? statusCode,
        string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaRequestException"/> class
    /// with diagnostic context and an inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="statusCode">HTTP status code, if applicable.</param>
    /// <param name="responseBody">Response body, if any.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OllamaRequestException(
        string message,
        HttpStatusCode? statusCode,
        string? responseBody,
        Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Gets the HTTP status code returned by Ollama, or <c>null</c> when the
    /// failure occurred before an HTTP response was received.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the raw response body returned by Ollama, or <c>null</c> when none was available.
    /// </summary>
    public string? ResponseBody { get; }
}