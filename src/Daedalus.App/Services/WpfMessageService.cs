using System.Windows;

namespace Daedalus.App.Services;

/// <summary>
/// WPF implementation of <see cref="IMessageService"/> using
/// <see cref="MessageBox"/>.
/// </summary>
/// <remarks>
/// The service is stateless and safe for concurrent calls, although dialogs
/// are always invoked one at a time from the UI thread in practice.
/// </remarks>
public sealed class WpfMessageService : IMessageService
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The method is called from a non-UI thread.
    /// </exception>
    public void ShowError(string title, string message)
    {
        Show(title, message, MessageBoxImage.Error);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The method is called from a non-UI thread.
    /// </exception>
    public void ShowInformation(string title, string message)
    {
        Show(title, message, MessageBoxImage.Information);
    }

    private static void Show(string title, string message, MessageBoxImage icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);

        if (Application.Current?.Dispatcher.CheckAccess() != true)
        {
            throw new InvalidOperationException(
                "Message dialogs must be shown from the UI thread.");
        }

        MessageBox.Show(
            Application.Current.MainWindow,
            message,
            title,
            MessageBoxButton.OK,
            icon);
    }
}