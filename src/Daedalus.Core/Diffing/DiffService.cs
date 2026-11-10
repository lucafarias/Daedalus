using System;
using System.Threading;
using System.Threading.Tasks;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Extensions.Logging;

namespace Daedalus.Core.Diffing;

/// <summary>
/// Computes side-by-side diffs between the original file content and the
/// AI-proposed content.
/// </summary>
/// <remarks>
/// <para>
/// The service wraps DiffPlex's <see cref="SideBySideDiffBuilder"/>. The
/// resulting <see cref="SideBySideDiffModel"/> exposes two
/// <see cref="DiffPaneModel"/> instances (<see cref="SideBySideDiffModel.OldText"/>
/// and <see cref="SideBySideDiffModel.NewText"/>), each containing the lines
/// of the corresponding side annotated with <see cref="DiffPlex.DiffBuilder.Model.ChangeType"/>.
/// </para>
/// <para>
/// DiffPlex is CPU-bound and synchronous. Each call is therefore offloaded to
/// the thread pool via <see cref="Task.Run(Func{Task}, CancellationToken)"/>,
/// so the calling thread (typically the UI) is never blocked.
/// </para>
/// <para>
/// <b>Cancellation is cooperative at the boundaries.</b> DiffPlex does not
/// accept a <see cref="CancellationToken"/> and cannot be interrupted mid-way.
/// The token is honored before and after the computation, which is sufficient
/// for the expected file sizes (bounded by <c>DaedalusOptions.MaxFileSizeBytes</c>).
/// </para>
/// <para>
/// A new <see cref="Differ"/> and <see cref="SideBySideDiffBuilder"/> are
/// created per call. They are cheap to allocate and this removes any question
/// about thread-safety of shared instances. The class itself is stateless
/// apart from the injected logger.
/// </para>
/// </remarks>
public sealed class DiffService
{
    private readonly ILogger<DiffService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiffService"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    public DiffService(ILogger<DiffService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Computes a side-by-side diff between the original content and the
    /// modified content.
    /// </summary>
    /// <param name="originalContent">The original file content.</param>
    /// <param name="modifiedContent">The content proposed by the model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="SideBySideDiffModel"/> whose <see cref="SideBySideDiffModel.OldText"/>
    /// and <see cref="SideBySideDiffModel.NewText"/> expose the aligned lines.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="originalContent"/> or <paramref name="modifiedContent"/>
    /// is <c>null</c>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before the computation
    /// started, or before its result was returned to the caller.
    /// </exception>
    public async Task<SideBySideDiffModel> ComputeSideBySideDiffAsync(
        string originalContent,
        string modifiedContent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(originalContent);
        ArgumentNullException.ThrowIfNull(modifiedContent);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug(
            "Computing side-by-side diff (original: {OriginalLength} chars, modified: {ModifiedLength} chars).",
            originalContent.Length,
            modifiedContent.Length);

        SideBySideDiffModel model = await Task
            .Run(
                () =>
                {
                    var differ = new Differ();
                    var builder = new SideBySideDiffBuilder(differ);
                    return builder.BuildDiffModel(originalContent, modifiedContent);
                },
                cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (!model.OldText.HasDifferences && !model.NewText.HasDifferences)
        {
            _logger.LogDebug("Side-by-side diff produced no differences.");
        }
        else
        {
            _logger.LogDebug(
                "Side-by-side diff produced differences (old lines: {OldLineCount}, new lines: {NewLineCount}).",
                model.OldText.Lines.Count,
                model.NewText.Lines.Count);
        }

        return model;
    }
}