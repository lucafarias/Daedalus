using Microsoft.Extensions.Options;

namespace Daedalus.Core.Configuration;

/// <summary>
/// Validates <see cref="DaedalusOptions"/> at application startup.
/// </summary>
public sealed class DaedalusOptionsValidator : IValidateOptions<DaedalusOptions>
{
    private static readonly HashSet<string> ValidLogLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"
    };

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DaedalusOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        ValidateOllama(options, errors);
        ValidatePrompt(options, errors);
        ValidateScanning(options, errors);
        ValidateLogging(options, errors);
        ValidateBackup(options, errors);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateOllama(DaedalusOptions options, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.OllamaBaseUrl))
        {
            errors.Add($"{nameof(DaedalusOptions.OllamaBaseUrl)} must not be empty.");
        }
        else if (!Uri.TryCreate(options.OllamaBaseUrl, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add(
                $"{nameof(DaedalusOptions.OllamaBaseUrl)} must be a valid absolute HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(options.OllamaModel))
        {
            errors.Add($"{nameof(DaedalusOptions.OllamaModel)} must not be empty.");
        }

        if (options.OllamaExecutablePath is not null)
        {
            if (!Path.IsPathFullyQualified(options.OllamaExecutablePath))
            {
                errors.Add(
                    $"{nameof(DaedalusOptions.OllamaExecutablePath)} must be an absolute path.");
            }
            else if (!File.Exists(options.OllamaExecutablePath))
            {
                errors.Add(
                    $"{nameof(DaedalusOptions.OllamaExecutablePath)} does not exist: " +
                    $"'{options.OllamaExecutablePath}'.");
            }
        }

        if (options.StartupTimeoutSeconds <= 0)
        {
            errors.Add(
                $"{nameof(DaedalusOptions.StartupTimeoutSeconds)} must be greater than zero.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            errors.Add(
                $"{nameof(DaedalusOptions.RequestTimeoutSeconds)} must be greater than zero.");
        }

        if (options.MaxConcurrentRequests < 1)
        {
            errors.Add(
                $"{nameof(DaedalusOptions.MaxConcurrentRequests)} must be at least 1.");
        }
    }

    private static void ValidatePrompt(DaedalusOptions options, List<string> errors)
    {
        if (options.PromptFilePath is not null)
        {
            if (!Path.IsPathFullyQualified(options.PromptFilePath))
            {
                errors.Add($"{nameof(DaedalusOptions.PromptFilePath)} must be an absolute path.");
            }
            else if (!File.Exists(options.PromptFilePath))
            {
                errors.Add(
                    $"{nameof(DaedalusOptions.PromptFilePath)} does not exist: " +
                    $"'{options.PromptFilePath}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.DefaultPromptFileName))
        {
            errors.Add($"{nameof(DaedalusOptions.DefaultPromptFileName)} must not be empty.");
        }
    }

    private static void ValidateScanning(DaedalusOptions options, List<string> errors)
    {
        if (options.ExcludedDirectories is null || options.ExcludedDirectories.Length == 0)
        {
            errors.Add(
                $"{nameof(DaedalusOptions.ExcludedDirectories)} must contain at least one entry.");
        }

        if (options.ExcludedFilePatterns is null || options.ExcludedFilePatterns.Length == 0)
        {
            errors.Add(
                $"{nameof(DaedalusOptions.ExcludedFilePatterns)} must contain at least one entry.");
        }

        if (options.TargetExtensions is null || options.TargetExtensions.Length == 0)
        {
            errors.Add(
                $"{nameof(DaedalusOptions.TargetExtensions)} must contain at least one entry.");
        }
        else
        {
            foreach (var extension in options.TargetExtensions)
            {
                if (string.IsNullOrWhiteSpace(extension) || !extension.StartsWith('.'))
                {
                    errors.Add(
                        $"{nameof(DaedalusOptions.TargetExtensions)} entries must start with '.': " +
                        $"'{extension}'.");
                }
            }
        }

        if (options.TargetFileNames is null || options.TargetFileNames.Length == 0)
        {
            errors.Add(
                $"{nameof(DaedalusOptions.TargetFileNames)} must contain at least one entry.");
        }

        if (options.MaxFileSizeBytes <= 0)
        {
            errors.Add($"{nameof(DaedalusOptions.MaxFileSizeBytes)} must be greater than zero.");
        }
    }

    private static void ValidateLogging(DaedalusOptions options, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.LogDirectory))
        {
            errors.Add($"{nameof(DaedalusOptions.LogDirectory)} must not be empty.");
        }
        else if (!Path.IsPathFullyQualified(options.LogDirectory))
        {
            errors.Add($"{nameof(DaedalusOptions.LogDirectory)} must be an absolute path.");
        }
        else
        {
            try
            {
                Directory.CreateDirectory(options.LogDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(
                    $"{nameof(DaedalusOptions.LogDirectory)} cannot be created " +
                    $"('{options.LogDirectory}'): {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(options.LogMinimumLevel) ||
            !ValidLogLevels.Contains(options.LogMinimumLevel))
        {
            errors.Add(
                $"{nameof(DaedalusOptions.LogMinimumLevel)} must be one of: " +
                $"{string.Join(", ", ValidLogLevels)}.");
        }
    }

    private static void ValidateBackup(DaedalusOptions options, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(options.BackupDirectory))
        {
            errors.Add($"{nameof(DaedalusOptions.BackupDirectory)} must not be empty.");
        }
        else if (!Path.IsPathFullyQualified(options.BackupDirectory))
        {
            errors.Add($"{nameof(DaedalusOptions.BackupDirectory)} must be an absolute path.");
        }

        if (options.BackupRetentionDays < 0)
        {
            errors.Add(
                $"{nameof(DaedalusOptions.BackupRetentionDays)} must be zero or greater.");
        }
    }
}