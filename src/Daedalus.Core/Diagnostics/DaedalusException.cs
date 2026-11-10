namespace Daedalus.Core.Diagnostics;

/// <summary>
/// Base class for all exceptions raised by Daedalus.
/// Allows the UI boundary to catch a single, meaningful type instead of
/// catching <see cref="Exception"/> broadly.
/// </summary>
public class DaedalusException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DaedalusException"/> class.
    /// </summary>
    public DaedalusException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaedalusException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DaedalusException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaedalusException"/> class
    /// with a specified error message and a reference to the inner exception
    /// that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public DaedalusException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}