using Daedalus.Core.Configuration;
using Daedalus.Core.Diagnostics;
using Daedalus.Core.Diffing;
using Daedalus.Core.Ollama;
using Daedalus.Core.Prompting;
using Daedalus.Core.Scanning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Daedalus.Core.Analysis;

public sealed class RefactoringOrchestrator
{
    private readonly SolutionScanner _scanner;
    private readonly PromptProvider _promptProvider;
    private readonly OllamaHttpClient _httpClient;
    private readonly LlmResponseParser _parser;
    private readonly DiffService _diffService;
    private readonly OllamaProcessLauncher _launcher;
    private readonly ILogger<RefactoringOrchestrator> _logger;
    private readonly int _maxConcurrentRequests;

    public RefactoringOrchestrator(
        SolutionScanner scanner,
        PromptProvider promptProvider,
        OllamaHttpClient httpClient,
        LlmResponseParser parser,
        DiffService diffService,
        OllamaProcessLauncher launcher,
        IOptions<DaedalusOptions> options,
        ILogger<RefactoringOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(promptProvider);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(diffService);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _scanner = scanner;
        _promptProvider = promptProvider;
        _httpClient = httpClient;
        _parser = parser;
        _diffService = diffService;
        _launcher = launcher;
        _logger = logger;
        _maxConcurrentRequests = options.Value.MaxConcurrentRequests;
    }

    public async Task<IReadOnlyList<FileAnalysisResult>> AnalyzeSolutionAsync(
        string solutionRoot,
        IProgress<string>? statusProgress,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);

        await _launcher.EnsureRunningAsync(statusProgress, cancellationToken).ConfigureAwait(false);

        statusProgress?.Report("Loading prompt file...");
        PromptLoadResult prompt = await _promptProvider.LoadAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Starting analysis of {SolutionRoot}.", solutionRoot);
        statusProgress?.Report($"Scanning solution: {solutionRoot}...");

        var files = new List<ScannedFile>();
        await foreach (ScannedFile file in _scanner.ScanAsync(solutionRoot, cancellationToken).ConfigureAwait(false))
        {
            files.Add(file);
        }

        if (files.Count == 0)
        {
            _logger.LogInformation("No eligible files found.");
            statusProgress?.Report("No eligible files found.");
            return Array.Empty<FileAnalysisResult>();
        }

        _logger.LogInformation("Analysis will process {FileCount} file(s).", files.Count);
        statusProgress?.Report($"Found {files.Count} file(s). Starting analysis...");

        var results = new ConcurrentBag<FileAnalysisResult>();
        int processedCount = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxConcurrentRequests,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(
            files,
            parallelOptions,
            async (file, token) =>
            {
                statusProgress?.Report($"Analyzing: {Path.GetFileName(file.Path)}...");

                FileAnalysisResult result = await AnalyzeFileAsync(file, prompt, token).ConfigureAwait(false);
                results.Add(result);

                int completed = Interlocked.Increment(ref processedCount);
                progress?.Report(new AnalysisProgress(result.FilePath, result.Status, completed, files.Count));

                _logger.LogInformation("Processed {FilePath} ({Completed}/{Total}): {Status}.",
                    file.Path, completed, files.Count, result.Status);
            }).ConfigureAwait(false);

        _logger.LogInformation("Analysis completed for {SolutionRoot}.", solutionRoot);
        return results.ToArray();
    }

    private async Task<FileAnalysisResult> AnalyzeFileAsync(
        ScannedFile file,
        PromptLoadResult prompt,
        CancellationToken cancellationToken)
    {
        if (file.IsOversized)
        {
            _logger.LogWarning("Skipping {FilePath}: exceeds size limit.", file.Path);
            return FileAnalysisResult.Skipped(file.Path, $"File size ({file.SizeBytes} bytes) exceeds the configured limit.");
        }

        string originalContent;
        try
        {
            originalContent = await File.ReadAllTextAsync(file.Path, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to read {FilePath}.", file.Path);
            return FileAnalysisResult.Failed(file.Path, $"Failed to read the file: {ex.Message}");
        }

        string rawResponse;
        try
        {
            rawResponse = await _httpClient.GenerateAsync(prompt.Content, originalContent, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OllamaRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama generation failed for {FilePath}.", file.Path);
            return FileAnalysisResult.Failed(file.Path, ex.Message);
        }

        LlmParseResult parseResult;
        try
        {
            parseResult = _parser.Parse(rawResponse, originalContent, file.Path, Path.GetExtension(file.Path));
        }
        catch (LlmResponseParseException ex)
        {
            _logger.LogWarning(ex, "Failed to parse the LLM response for {FilePath}.", file.Path);
            return FileAnalysisResult.Failed(file.Path, ex.Message);
        }

        if (parseResult.Outcome == LlmParseOutcome.NoChanges)
        {
            return FileAnalysisResult.NoChanges(file.Path, originalContent, parseResult.ReviewNotes);
        }

        DiffPlex.DiffBuilder.Model.SideBySideDiffModel diffModel;
        try
        {
            diffModel = await _diffService.ComputeSideBySideDiffAsync(originalContent, parseResult.ExtractedContent, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute the diff for {FilePath}.", file.Path);
            return FileAnalysisResult.Failed(file.Path, $"Failed to compute the diff: {ex.Message}");
        }

        return FileAnalysisResult.Proposed(file.Path, originalContent, parseResult.ExtractedContent, diffModel, parseResult.ReviewNotes);
    }
}