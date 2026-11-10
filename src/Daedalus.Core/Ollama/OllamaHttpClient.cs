using Daedalus.Core.Configuration;
using Daedalus.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daedalus.Core.Ollama;

public sealed class OllamaHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaHttpClient> _logger;
    private readonly Uri _generateUri;
    private readonly string _modelName;
    private readonly TimeSpan _requestTimeout;

    public OllamaHttpClient(
        HttpClient httpClient,
        IOptions<DaedalusOptions> options,
        ILogger<OllamaHttpClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;

        DaedalusOptions daedalusOptions = options.Value;
        _modelName = daedalusOptions.OllamaModel;
        _requestTimeout = TimeSpan.FromSeconds(daedalusOptions.RequestTimeoutSeconds);
        string baseUri = daedalusOptions.OllamaBaseUrl.TrimEnd('/');
        _generateUri = new Uri($"{baseUri}/api/generate", UriKind.Absolute);
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string fileContent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentNullException.ThrowIfNull(fileContent);

        var payload = new OllamaGenerateRequest
        {
            Model = _modelName,
            System = systemPrompt,
            Prompt = fileContent,
            Stream = false
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_requestTimeout);

        // Log a Information per vedere il caricamento nel file
        _logger.LogInformation("Sending generation request to Ollama (model: {ModelName}, content: {Length} chars). Waiting for response...", _modelName, fileContent.Length);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _generateUri)
            {
                Content = JsonContent.Create(payload)
            };

            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await TryReadBodyAsync(response, timeoutCts.Token).ConfigureAwait(false);
                _logger.LogWarning("Ollama generation request failed with HTTP {StatusCode}. Body: {ResponseBody}", (int)response.StatusCode, errorBody);

                throw new OllamaRequestException(
                    $"Ollama returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                    response.StatusCode,
                    errorBody);
            }

            string json = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

            OllamaGenerateResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<OllamaGenerateResponse>(json);
            }
            catch (JsonException ex)
            {
                throw new OllamaRequestException("Failed to parse the Ollama generation response.", response.StatusCode, json, ex);
            }

            if (parsed?.Response is not { Length: > 0 } content)
            {
                throw new OllamaRequestException("The Ollama generation response did not contain any content.", response.StatusCode, json);
            }

            _logger.LogInformation("Received response from Ollama ({ResponseLength} chars).", content.Length);
            return content;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("Ollama generation request timed out after {TimeoutSeconds} second(s).", _requestTimeout.TotalSeconds);
            throw new OllamaRequestException($"The Ollama generation request timed out after {_requestTimeout.TotalSeconds:F0} second(s).", null, null, ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama generation request failed at the network layer.");
            throw new OllamaRequestException("Failed to reach the Ollama service.", ex.StatusCode, null, ex);
        }
    }

    private static async Task<string> TryReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("system")] public string System { get; set; } = string.Empty;
        [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
        [JsonPropertyName("stream")] public bool Stream { get; set; }
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")] public string? Response { get; set; }
    }
}