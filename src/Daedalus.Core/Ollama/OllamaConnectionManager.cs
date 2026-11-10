using Daedalus.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daedalus.Core.Ollama;

/// <summary>
/// Checks whether the local Ollama service is reachable and whether the
/// configured model is installed.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="HttpClient"/> is injected and must be a singleton managed
/// by the DI container; this class never creates or disposes it.
/// </para>
/// <para>
/// All three public methods delegate to a single private helper
/// (<see cref="TryFetchTagsAsync"/>), so the HTTP semantics are defined in
/// exactly one place. A health check performs a single request to
/// <c>GET /api/tags</c>; no retries are attempted, because a failed check
/// is actionable information for the caller, not a transient condition.
/// </para>
/// <para>
/// Instance state is immutable after construction (readonly fields), so
/// concurrent calls from multiple threads are safe.
/// </para>
/// </remarks>
public sealed class OllamaConnectionManager
{
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaConnectionManager> _logger;
    private readonly Uri _tagsUri;
    private readonly string _modelName;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaConnectionManager"/> class.
    /// </summary>
    /// <param name="httpClient">Injected HTTP client (singleton, not owned).</param>
    /// <param name="options">Validated application options.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">
    /// Any parameter is <c>null</c>.
    /// </exception>
    /// <exception cref="UriFormatException">
    /// <see cref="DaedalusOptions.OllamaBaseUrl"/> is not a valid URI.
    /// Validation should have rejected this earlier; this is a safety net.
    /// </exception>
    public OllamaConnectionManager(
        HttpClient httpClient,
        IOptions<DaedalusOptions> options,
        ILogger<OllamaConnectionManager> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;

        DaedalusOptions daedalusOptions = options.Value;

        _modelName = daedalusOptions.OllamaModel;

        string baseUri = daedalusOptions.OllamaBaseUrl.TrimEnd('/');
        _tagsUri = new Uri($"{baseUri}/api/tags", UriKind.Absolute);
    }

    /// <summary>
    /// Performs a full health check: reachability plus model presence.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="OllamaHealthStatus.Unreachable"/> if the endpoint does not
    /// respond; <see cref="OllamaHealthStatus.ModelMissing"/> if it responds
    /// but the model is absent; <see cref="OllamaHealthStatus.Healthy"/>
    /// otherwise.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    public async Task<OllamaHealthStatus> CheckHealthAsync(CancellationToken cancellationToken)
    {
        OllamaTagsResponse? tags = await TryFetchTagsAsync(cancellationToken).ConfigureAwait(false);

        if (tags is null)
        {
            return OllamaHealthStatus.Unreachable;
        }

        bool modelInstalled = tags.Models is { Count: > 0 }
            && tags.Models.Any(model => MatchesModel(model, _modelName));

        if (!modelInstalled)
        {
            _logger.LogWarning(
                "Model {ModelName} is not installed in Ollama.",
                _modelName);
            return OllamaHealthStatus.ModelMissing;
        }

        return OllamaHealthStatus.Healthy;
    }

    /// <summary>
    /// Checks whether the Ollama endpoint responds with a success status.
    /// Does not verify model presence.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the endpoint responded; <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken)
    {
        OllamaTagsResponse? tags = await TryFetchTagsAsync(cancellationToken).ConfigureAwait(false);
        return tags is not null;
    }

    /// <summary>
    /// Checks whether the configured model is installed in Ollama.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the model is installed; <c>false</c> otherwise
    /// (including when the endpoint is unreachable).
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled by the caller.
    /// </exception>
    public async Task<bool> IsModelInstalledAsync(CancellationToken cancellationToken)
    {
        OllamaTagsResponse? tags = await TryFetchTagsAsync(cancellationToken).ConfigureAwait(false);

        return tags?.Models is { Count: > 0 }
            && tags.Models.Any(model => MatchesModel(model, _modelName));
    }

    private async Task<OllamaTagsResponse?> TryFetchTagsAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(HealthCheckTimeout);

        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(_tagsUri, timeoutCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Ollama tags endpoint returned HTTP {StatusCode}.",
                    (int)response.StatusCode);
                return null;
            }

            string json = await response.Content
                .ReadAsStringAsync(timeoutCts.Token)
                .ConfigureAwait(false);

            return JsonSerializer.Deserialize<OllamaTagsResponse>(json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // External cancellation: propagate so the caller can distinguish
            // a user-initiated cancellation from a health-check timeout.
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(
                "Ollama health check timed out after {TimeoutSeconds} second(s).",
                HealthCheckTimeout.TotalSeconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Ollama health check failed.");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse the Ollama tags response.");
            return null;
        }
    }

    private static bool MatchesModel(OllamaModelInfo model, string expected)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        return string.Equals(model.Name, expected, StringComparison.OrdinalIgnoreCase)
            || string.Equals(model.Model, expected, StringComparison.OrdinalIgnoreCase);
    }

    // ----- JSON DTOs --------------------------------------------------------
    // Classes (not records) with get/set are used intentionally: this is the
    // most robust form for System.Text.Json reflection-based deserialization
    // and avoids surprises with init-only setters or positional records.

    private sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo>? Models { get; set; }
    }

    private sealed class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }
}