namespace Daedalus.Core.Diagnostics;

/// <summary>
/// Raised when the configured LLM model is not present in the list returned
/// by Ollama's <c>GET /api/tags</c> endpoint.
/// </summary>
/// <remarks>
/// The message is always derived from the model name; there is no
/// constructor that accepts a free-form message, because a missing model
/// is a well-defined failure with a well-defined remediation
/// (<c>ollama pull &lt;model&gt;</c>).
/// </remarks>
public sealed class OllamaModelMissingException : DaedalusException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaModelMissingException"/> class
    /// with an empty model name.
    /// </summary>
    public OllamaModelMissingException()
        : this(modelName: string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaModelMissingException"/> class
    /// for the specified model.
    /// </summary>
    /// <param name="modelName">Identifier of the model that was not found.</param>
    public OllamaModelMissingException(string modelName)
        : base(BuildMessage(modelName))
    {
        ModelName = modelName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaModelMissingException"/> class
    /// for the specified model, with a reference to the inner exception.
    /// </summary>
    /// <param name="modelName">Identifier of the model that was not found.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public OllamaModelMissingException(string modelName, Exception innerException)
        : base(BuildMessage(modelName), innerException)
    {
        ModelName = modelName;
    }

    /// <summary>
    /// Gets the identifier of the model that is missing.
    /// </summary>
    public string ModelName { get; }

    private static string BuildMessage(string modelName) =>
        $"The model '{modelName}' is not installed. " +
        $"Run 'ollama pull {modelName}' and try again.";
}