namespace Daedalus.Core.Diagnostics;

/// <summary>
/// Raised when an atomic write or a backup operation fails.
/// </summary>
/// <remarks>
/// A single exception type covers backup copy failures and atomic replace
/// failures because the remediation is the same: the file was not modified,
/// and the user must investigate filesystem permissions or locks.
/// </remarks>
public sealed class FileWriteException : DaedalusException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileWriteException"/> class.
    /// </summary>
    public FileWriteException()
        : base("Failed to write the file.")
    {
        FilePath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileWriteException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public FileWriteException(string message)
        : base(message)
    {
        FilePath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileWriteException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FileWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
        FilePath = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileWriteException"/> class
    /// for a specific file, with a reason and an inner exception.
    /// </summary>
    /// <param name="filePath">Path of the file that could not be written.</param>
    /// <param name="reason">Human-readable reason for the failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FileWriteException(string filePath, string reason, Exception innerException)
        : base($"Failed to write the file '{filePath}': {reason}.", innerException)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Gets the path of the file that could not be written,
    /// or an empty string when the exception was constructed without a path.
    /// </summary>
    public string FilePath { get; }
}