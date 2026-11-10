namespace Daedalus.App.Services;

/// <summary>
/// Abstraction over native file and folder dialogs, so that ViewModels do
/// not reference <c>Microsoft.Win32</c> directly.
/// </summary>
/// <remarks>
/// <para>
/// All methods are synchronous: they display a modal dialog and block the
/// calling thread until the user dismisses it. They <b>must</b> be called
/// from the UI thread; implementations enforce this contract.
/// </para>
/// <para>
/// Returning <c>null</c> means the user cancelled the dialog. A non-null
/// return value is an absolute path.
/// </para>
/// </remarks>
public interface IFileDialogService
{
    /// <summary>
    /// Displays a folder picker dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="initialDirectory">
    /// Initial directory shown by the dialog, or <c>null</c> to use the
    /// system default.
    /// </param>
    /// <returns>
    /// The selected folder's absolute path, or <c>null</c> if the user
    /// cancelled.
    /// </returns>
    string? PickFolder(string title, string? initialDirectory);
}