using System;
using System.Net.Http;
using System.Threading;
using System.Windows;
using Daedalus.App.Services;
using Daedalus.App.ViewModels;
using Daedalus.App.Views;
using Daedalus.Core.Analysis;
using Daedalus.Core.Configuration;
using Daedalus.Core.Diffing;
using Daedalus.Core.Logging;
using Daedalus.Core.Ollama;
using Daedalus.Core.Persistence;
using Daedalus.Core.Prompting;
using Daedalus.Core.Scanning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace Daedalus.App;

/// <summary>
/// Application entry point and composition root.
/// </summary>
/// <remarks>
/// <para>
/// Startup sequence:
/// </para>
/// <list type="number">
///   <item>Create a bootstrap logger so early failures are captured even if
///   the final log directory is invalid.</item>
///   <item>Load and validate <see cref="DaedalusOptions"/>. On failure, show
///   a modal dialog with all validation errors and shut down.</item>
///   <item>Replace the bootstrap logger with the full logger configured from
///   validated options.</item>
///   <item>Build and start the generic host, wiring every service.</item>
///   <item>Resolve <see cref="MainWindow"/>, assign its DataContext, and show it.</item>
/// </list>
/// <para>
/// <b>Code-behind exception</b>: this file is the WPF application entry point
/// and contains the composition root. It is not code-behind of a View in the
/// MVVM sense; it is the only place where DI configuration is allowed.
/// </para>
/// </remarks>
public partial class App : Application
{
    private IHost? _host;

    /// <inheritdoc />
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = LoggingSetup.CreateBootstrapLogger();

        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            DaedalusOptions options = configuration
                .GetSection(DaedalusOptions.SectionName)
                .Get<DaedalusOptions>() ?? new DaedalusOptions();

            var validationResult = new DaedalusOptionsValidator().Validate(null, options);
            if (validationResult.Failed)
            {
                string errors = string.Join(
                    Environment.NewLine,
                    validationResult.Failures ?? Array.Empty<string>());

                Log.Fatal("Invalid configuration: {ValidationErrors}", errors);

                MessageBox.Show(
                    $"Daedalus cannot start due to invalid configuration:{Environment.NewLine}{Environment.NewLine}{errors}",
                    "Daedalus - Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(exitCode: 1);
                return;
            }

            Log.Logger = LoggingSetup.CreateFullLogger(options);
            Log.Information(
                "Daedalus starting (Ollama: {OllamaUrl}, model: {Model}, log: {LogDirectory}).",
                options.OllamaBaseUrl,
                options.OllamaModel,
                options.LogDirectory);

            _host = Host
                .CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((_, services) =>
                    ConfigureServices(services, configuration))
                .Build();

            await _host.StartAsync();

            MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = _host.Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();

            Log.Information("Daedalus started.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Daedalus failed to start.");

            MessageBox.Show(
                $"Daedalus failed to start:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Daedalus - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(exitCode: 2);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _host?.Services.GetService<MainViewModel>()?.Dispose();
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while stopping the host.");
        }
        finally
        {
            _host?.Dispose();
            Log.Information("Daedalus shutting down.");
            Log.CloseAndFlush();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DaedalusOptions>()
            .Bind(configuration.GetSection(DaedalusOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<DaedalusOptions>, DaedalusOptionsValidator>();

        services.AddSingleton(_ => new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        });

        services.AddSingleton<OllamaConnectionManager>();
        services.AddSingleton<OllamaProcessLauncher>();
        services.AddSingleton<OllamaHttpClient>();
        services.AddSingleton<PromptProvider>();
        services.AddSingleton<LlmResponseParser>();
        services.AddSingleton<SolutionScanner>();
        services.AddSingleton<DiffService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<AtomicFileWriter>();
        services.AddSingleton<RefactoringOrchestrator>();

        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<IMessageService, WpfMessageService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}