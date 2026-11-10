using System.IO;
using Daedalus.Core.Analysis;

namespace Daedalus.App.ViewModels;

/// <summary>
/// Wraps a <see cref="FileAnalysisResult"/> for presentation in the UI.
/// </summary>
public sealed class ReviewItemViewModel
{
    public ReviewItemViewModel(FileAnalysisResult result, string solutionRoot)
    {
        Result = result;
        FilePath = result.FilePath;

        // Calcola il percorso relativo per imitare la Solution Explorer di Visual Studio
        if (!string.IsNullOrWhiteSpace(solutionRoot))
        {
            RelativePath = Path.GetRelativePath(solutionRoot, result.FilePath);
        }
        else
        {
            RelativePath = Path.GetFileName(result.FilePath);
        }
    }

    public FileAnalysisResult Result { get; }
    public string FilePath { get; }
    public string RelativePath { get; }
    public string? ReviewNotes => Result.ReviewNotes;
    public string OriginalContent => Result.OriginalContent ?? string.Empty;
    public string ProposedContent => Result.ProposedContent ?? string.Empty;
}