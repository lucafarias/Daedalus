using Daedalus.Core.Configuration;
using Daedalus.Core.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Diagnostics;

namespace Daedalus.Core.Ollama;

/// <summary>
/// Ensures that the Ollama service is reachable, starting it if necessary.
/// </summary>
public sealed class OllamaProcessLauncher : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly OllamaConnectionManager _connectionManager;
    private readonly ILogger<OllamaProcessLauncher> _logger;
    private readonly SemaphoreSlim _startLock = new(initialCount: 1, maxCount: 1);
    private readonly string? _configuredExecutablePath;
    private readonly string _modelName;
    private readonly TimeSpan _startupTimeout;

    private bool _disposed;

    public OllamaProcessLauncher(
        OllamaConnectionManager connectionManager,
        IOptions<DaedalusOptions> options,
        ILogger<OllamaProcessLauncher> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionManager = connectionManager;
        _logger = logger;

        DaedalusOptions daedalusOptions = options.Value;

        _configuredExecutablePath = daedalusOptions.OllamaExecutablePath;
        _modelName = daedalusOptions.OllamaModel;
        _startupTimeout = TimeSpan.FromSeconds(daedalusOptions.StartupTimeoutSeconds);
    }

    public async Task EnsureRunningAsync(IProgress<string>? statusProgress, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _startLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            statusProgress?.Report("Checking Ollama connection...");
            OllamaHealthStatus status =
                await _connectionManager.CheckHealthAsync(cancellationToken).ConfigureAwait(false);

            switch (status)
            {
                case OllamaHealthStatus.Healthy:
                    _logger.LogInformation("Ollama is already running and healthy.");
                    statusProgress?.Report("Ollama is already running and healthy.");
                    return;

                case OllamaHealthStatus.ModelMissing:
                    throw new OllamaModelMissingException(_modelName);
            }

            _logger.LogInformation("Ollama is unreachable. Attempting to start it.");
            statusProgress?.Report("Ollama is unreachable. Locating executable...");

            string executablePath = DiscoverExecutablePath();

            statusProgress?.Report($"Starting Ollama process: {executablePath}");
            using Process process = StartProcess(executablePath);

            try
            {
                statusProgress?.Report($"Waiting for Ollama to start (timeout: {_startupTimeout.TotalSeconds}s)...");
                await WaitForHealthAsync(process, statusProgress, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                TryKill(process);
                throw;
            }

            _logger.LogInformation("Ollama became healthy (pid: {ProcessId}).", process.Id);
            statusProgress?.Report("Ollama is now running.");
        }
        finally
        {
            _startLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _startLock.Dispose();
    }

    private string DiscoverExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_configuredExecutablePath))
        {
            if (!File.Exists(_configuredExecutablePath))
            {
                throw new OllamaNotInstalledException(
                    $"The configured Ollama executable does not exist: '{_configuredExecutablePath}'.");
            }
            return _configuredExecutablePath;
        }

        var searchedPaths = new List<string>();

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathVariable))
        {
            foreach (string segment in pathVariable.Split(
                Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string candidate = Path.Combine(segment, "ollama.exe");
                searchedPaths.Add(candidate);
                if (File.Exists(candidate)) return candidate;
            }
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string localCandidate = Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe");
        searchedPaths.Add(localCandidate);
        if (File.Exists(localCandidate)) return localCandidate;

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesCandidate = Path.Combine(programFiles, "Ollama", "ollama.exe");
        searchedPaths.Add(programFilesCandidate);
        if (File.Exists(programFilesCandidate)) return programFilesCandidate;

        throw new OllamaNotInstalledException(searchedPaths);
    }

    private Process StartProcess(string executablePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty
        };

        var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) _logger.LogDebug("Ollama stdout: {Line}", e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) _logger.LogWarning("Ollama stderr: {Line}", e.Data);
        };

        if (!process.Start())
        {
            throw new OllamaNotInstalledException($"Failed to start the Ollama process from '{executablePath}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private async Task WaitForHealthAsync(Process process, IProgress<string>? statusProgress, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_startupTimeout);

        try
        {
            while (true)
            {
                if (process.HasExited)
                {
                    _logger.LogDebug("Ollama process exited during startup (exit code: {ExitCode}).", process.ExitCode);
                }

                OllamaHealthStatus status = await _connectionManager
                    .CheckHealthAsync(timeoutCts.Token)
                    .ConfigureAwait(false);

                switch (status)
                {
                    case OllamaHealthStatus.Healthy:
                        return;
                    case OllamaHealthStatus.ModelMissing:
                        throw new OllamaModelMissingException(_modelName);
                }

                await Task.Delay(PollInterval, timeoutCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new OllamaStartupTimeoutException((int)_startupTimeout.TotalSeconds);
        }
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogWarning("Ollama process {ProcessId} was terminated after a failed or cancelled startup.", process.Id);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Could not terminate the Ollama process (it may have already exited).");
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "Could not terminate the Ollama process due to a Win32 error.");
        }
    }
}