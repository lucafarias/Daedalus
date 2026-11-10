using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Daedalus.App.Services;
using Daedalus.Core.Analysis;
using Daedalus.Core.Diagnostics;
using Daedalus.Core.Persistence;
using Daedalus.Core.Prompting;
using Microsoft.Extensions.Logging;

namespace Daedalus.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IMessageService _messageService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly RefactoringOrchestrator _orchestrator;
    private readonly PromptProvider _promptProvider;
    private readonly AtomicFileWriter _writer;
    private readonly ILogger<MainViewModel> _logger;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");

    private CancellationTokenSource? _analysisCts;
    private bool _disposed;

    [ObservableProperty]
    private string _solutionRootPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Select a solution folder and start the analysis.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartAnalysisCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAndNextCommand))]
    [NotifyCanExecuteChangedFor(nameof(SkipCurrentCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousResultCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextResultCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMaximum;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAndNextCommand))]
    [NotifyCanExecuteChangedFor(nameof(SkipCurrentCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousResultCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextResultCommand))]
    private ReviewItemViewModel? _currentItem;

    [ObservableProperty]
    private string _finalText = string.Empty;

    [ObservableProperty]
    private string _currentSyntax = "C#";

    public ObservableCollection<ReviewItemViewModel> ReviewQueue { get; } = new();
    public ObservableCollection<string> LogMessages { get; } = new();

    public MainViewModel(
        IFileDialogService fileDialogService,
        IMessageService messageService,
        IUiDispatcher uiDispatcher,
        RefactoringOrchestrator orchestrator,
        PromptProvider promptProvider,
        AtomicFileWriter writer,
        ILogger<MainViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(fileDialogService);
        ArgumentNullException.ThrowIfNull(messageService);
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(promptProvider);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(logger);

        _fileDialogService = fileDialogService;
        _messageService = messageService;
        _uiDispatcher = uiDispatcher;
        _orchestrator = orchestrator;
        _promptProvider = promptProvider;
        _writer = writer;
        _logger = logger;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = null;
    }

    [RelayCommand]
    private void SelectSolutionFolder()
    {
        string? selected = _fileDialogService.PickFolder("Select solution root folder", string.IsNullOrWhiteSpace(SolutionRootPath) ? null : SolutionRootPath);
        if (selected is null) return;

        SolutionRootPath = selected;
        StatusMessage = $"Solution root: {selected}";
    }

    [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanStartAnalysis))]
    private async Task StartAnalysisAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return;

        _analysisCts?.Dispose();
        _analysisCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        IsBusy = true;
        ReviewQueue.Clear();
        LogMessages.Clear();
        CurrentItem = null;
        FinalText = string.Empty;
        ProgressValue = 0;
        ProgressMaximum = 0;
        StatusMessage = "Starting...";

        try
        {
            var statusProgress = new Progress<string>(OnStatusProgress);
            var fileProgress = new Progress<AnalysisProgress>(OnFileProgress);

            IReadOnlyList<FileAnalysisResult> results = await _orchestrator
                .AnalyzeSolutionAsync(SolutionRootPath, statusProgress, fileProgress, _analysisCts.Token)
                .ConfigureAwait(true);

            int proposed = 0, noChanges = 0, failed = 0, skipped = 0;
            foreach (FileAnalysisResult result in results)
            {
                switch (result.Status)
                {
                    case FileAnalysisStatus.ProposedChanges:
                        ReviewQueue.Add(new ReviewItemViewModel(result, SolutionRootPath));
                        proposed++;
                        break;
                    case FileAnalysisStatus.NoChanges: noChanges++; break;
                    case FileAnalysisStatus.Failed: failed++; break;
                    case FileAnalysisStatus.Skipped: skipped++; break;
                }
            }

            if (ReviewQueue.Count > 0) CurrentItem = ReviewQueue[0];

            string summary = proposed > 0
                ? $"Analysis complete. {proposed} file(s) need review, {noChanges} unchanged, {failed} failed, {skipped} skipped."
                : $"Analysis complete. No changes proposed ({noChanges} unchanged, {failed} failed, {skipped} skipped).";

            StatusMessage = summary;
            LogMessages.Add($"[SYSTEM] {summary}");
        }
        catch (OperationCanceledException) when (_analysisCts.IsCancellationRequested)
        {
            StatusMessage = "Analysis cancelled.";
            LogMessages.Add("[SYSTEM] Analysis cancelled by user.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed.");
            _messageService.ShowError("Analysis Failed", ex.Message);
            StatusMessage = "Analysis failed.";
            LogMessages.Add($"[ERROR] {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveCurrent))]
    private async Task SaveAndNextAsync(CancellationToken cancellationToken)
    {
        ReviewItemViewModel? current = CurrentItem;
        if (current is null) return;

        IsBusy = true;
        try
        {
            await _writer.WriteAsync(current.FilePath, FinalText, SolutionRootPath, _sessionId, cancellationToken).ConfigureAwait(true);
            LogMessages.Add($"[SAVE] {current.RelativePath}");

            ReviewQueue.Remove(current);
            if (ReviewQueue.Count > 0)
            {
                CurrentItem = ReviewQueue[0];
                StatusMessage = $"Saved {current.RelativePath}. {ReviewQueue.Count} file(s) remaining.";
            }
            else
            {
                CurrentItem = null;
                StatusMessage = $"Saved {current.RelativePath}. Review complete.";
            }
        }
        catch (Exception ex)
        {
            _messageService.ShowError("Save Failed", ex.Message);
            LogMessages.Add($"[ERROR] Failed to save: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSkipCurrent))]
    private void SkipCurrent()
    {
        ReviewItemViewModel? current = CurrentItem;
        if (current is null) return;

        ReviewQueue.Remove(current);
        LogMessages.Add($"[SKIP] {current.RelativePath}");

        if (ReviewQueue.Count > 0)
        {
            CurrentItem = ReviewQueue[0];
            StatusMessage = $"Skipped {current.RelativePath}. {ReviewQueue.Count} file(s) remaining.";
        }
        else
        {
            CurrentItem = null;
            StatusMessage = $"Skipped {current.RelativePath}. Review complete.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigatePrevious))]
    private void PreviousResult()
    {
        if (CurrentItem is null) return;
        int index = ReviewQueue.IndexOf(CurrentItem);
        if (index > 0) CurrentItem = ReviewQueue[index - 1];
    }

    [RelayCommand(CanExecute = nameof(CanNavigateNext))]
    private void NextResult()
    {
        if (CurrentItem is null) return;
        int index = ReviewQueue.IndexOf(CurrentItem);
        if (index >= 0 && index < ReviewQueue.Count - 1) CurrentItem = ReviewQueue[index + 1];
    }

    partial void OnCurrentItemChanged(ReviewItemViewModel? value)
    {
        FinalText = value?.ProposedContent ?? value?.OriginalContent ?? string.Empty;

        if (value != null)
        {
            string ext = Path.GetExtension(value.FilePath).ToLowerInvariant();
            CurrentSyntax = ext switch
            {
                ".cs" => "C#",
                ".xaml" => "XML",
                ".csproj" => "XML",
                ".json" => "Json",
                _ => "C#"
            };
        }
    }

    partial void OnSolutionRootPathChanged(string value) => StartAnalysisCommand.NotifyCanExecuteChanged();

    private bool CanStartAnalysis() => !IsBusy && !string.IsNullOrWhiteSpace(SolutionRootPath) && Directory.Exists(SolutionRootPath);
    private bool CanSaveCurrent() => CurrentItem is not null && !IsBusy;
    private bool CanSkipCurrent() => CurrentItem is not null && !IsBusy;
    private bool CanNavigatePrevious() => CurrentItem is not null && ReviewQueue.IndexOf(CurrentItem) > 0;
    private bool CanNavigateNext() => CurrentItem is not null && ReviewQueue.IndexOf(CurrentItem) < ReviewQueue.Count - 1;

    private void OnStatusProgress(string message)
    {
        if (!_uiDispatcher.CheckAccess())
        {
            _ = _uiDispatcher.InvokeAsync(() => OnStatusProgress(message));
            return;
        }

        StatusMessage = message;
        LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        if (LogMessages.Count > 500) LogMessages.RemoveAt(0);
    }

    private void OnFileProgress(AnalysisProgress progress)
    {
        if (!_uiDispatcher.CheckAccess())
        {
            _ = _uiDispatcher.InvokeAsync(() => OnFileProgress(progress));
            return;
        }

        ProgressValue = progress.ProcessedCount;
        ProgressMaximum = progress.TotalCount;

        string fileName = Path.GetFileName(progress.FilePath);
        string msg = $"Analyzed ({progress.ProcessedCount}/{progress.TotalCount}): {fileName}";

        StatusMessage = msg;
        LogMessages.Add($"[{DateTime.Now:HH:mm:ss}] {msg} -> {progress.Status}");

        if (LogMessages.Count > 500) LogMessages.RemoveAt(0);
    }
}