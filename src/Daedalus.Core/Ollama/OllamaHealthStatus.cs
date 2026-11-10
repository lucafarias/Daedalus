namespace Daedalus.Core.Ollama;

/// <summary>
/// Represents the result of a health check against the local Ollama service.
/// </summary>
public enum OllamaHealthStatus
{
    /// <summary>
    /// The Ollama endpoint could not be reached (network error, timeout,
    /// non-success HTTP status, or unparseable response).
    /// </summary>
    Unreachable,

    /// <summary>
    /// The endpoint responded, but the configured model is not present in
    /// the list returned by <c>GET /api/tags</c>.
    /// </summary>
    ModelMissing,

    /// <summary>
    /// The endpoint responded and the configured model is available.
    /// </summary>
    Healthy
}