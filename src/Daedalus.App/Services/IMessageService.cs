namespace Daedalus.App.Services;

/// <summary>
/// Abstraction over modal message dialogs, so that ViewModels do not
/// reference <c>System.Windows.MessageBox</c> directly.
/// </summary>
/// <remarks>
/// All methods are synchronous and must be called from the UI thread.
/// Implementations enforce this contract.
/// </remarks>
public interface IMessageService
{
    /// <summary>
    /// Shows a modal error dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Message body.</param>
    void ShowError(string title, string message);

    /// <summary>
    /// Shows a modal information dialog.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Message body.</param>
    void ShowInformation(string title, string message);
}