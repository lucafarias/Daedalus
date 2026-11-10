using Daedalus.Core.Configuration;
using Serilog;
using Serilog.Events;

namespace Daedalus.Core.Logging;

/// <summary>
/// Centralizes Serilog configuration for the application.
/// Provides a bootstrap logger for early startup failures and a full
/// logger configured from <see cref="DaedalusOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The bootstrap logger and the full logger use the same output template
/// to keep log files consistent, but write to different directories.
/// The bootstrap logger targets <c>%TEMP%\Daedalus\</c> so it remains
/// functional even when the configured log directory is invalid or
/// inaccessible.
/// </para>
/// <para>
/// <see cref="Log.Logger"/> is replaced at most once per process, from
/// the composition root, before any concurrent logging occurs.
/// Serilog's file sink is thread-safe, but the static logger swap is not:
/// the replacement must happen on a single thread at startup.
/// </para>
/// </remarks>
public static class LoggingSetup
{
    /// <summary>
    /// Output template shared by the bootstrap and full loggers.
    /// </summary>
    public const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private const int BootstrapRetainedFileCount = 3;
    private const int FullRetainedFileCount = 7;

    /// <summary>
    /// Creates a bootstrap logger used before configuration has been
    /// loaded and validated. Writes to <c>%TEMP%\Daedalus\bootstrap-.log</c>
    /// so it works even when the configured log directory is unavailable.
    /// </summary>
    /// <returns>A new <see cref="Serilog.ILogger"/> instance.</returns>
    public static Serilog.ILogger CreateBootstrapLogger()
    {
        string bootstrapDirectory = Path.Combine(Path.GetTempPath(), "Daedalus");
        Directory.CreateDirectory(bootstrapDirectory);

        string bootstrapPath = Path.Combine(bootstrapDirectory, "bootstrap-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: bootstrapPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: BootstrapRetainedFileCount,
                shared: false,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }

    /// <summary>
    /// Creates the full application logger from validated options.
    /// The caller is responsible for assigning the result to
    /// <see cref="Log.Logger"/> or to the host's logging pipeline.
    /// </summary>
    /// <param name="options">Validated application options.</param>
    /// <returns>A new <see cref="Serilog.ILogger"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="DaedalusOptions.LogMinimumLevel"/> is not a recognized level.
    /// </exception>
    public static Serilog.ILogger CreateFullLogger(DaedalusOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string logPath = Path.Combine(options.LogDirectory, "daedalus-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Is(ParseLevel(options.LogMinimumLevel))
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: FullRetainedFileCount,
                shared: false,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }

    /// <summary>
    /// Translates a configuration string into a Serilog <see cref="LogEventLevel"/>.
    /// </summary>
    /// <remarks>
    /// Values are validated upstream by <c>DaedalusOptionsValidator</c>;
    /// this method therefore treats unknown input as a programming error,
    /// not a user misconfiguration.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="level"/> is <c>null</c>, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="level"/> is not a recognized Serilog level.
    /// </exception>
    internal static LogEventLevel ParseLevel(string level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level);

        return level.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "Unsupported log level. Validation should have rejected this value.")
        };
    }

    /// <summary>
    /// Flushes and disposes the current global <see cref="Log.Logger"/>.
    /// Must be invoked exactly once during application shutdown, after the
    /// last log entry has been written.
    /// </summary>
    /// <returns>A task that completes when the logger is flushed and closed.</returns>
    /// <summary>
    /// Flushes and disposes the current global <see cref="Log.Logger"/>.
    /// Must be invoked exactly once during application shutdown, after the
    /// last log entry has been written.
    /// </summary>
    /// <returns>A task that completes when the logger is flushed and closed.</returns>
    public static ValueTask CloseAndFlushAsync()
    {
        return Log.CloseAndFlushAsync();
    }
}