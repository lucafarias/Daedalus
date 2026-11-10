using Microsoft.Win32;
using System.Windows;

namespace Daedalus.App.Services;

/// <summary>
/// WPF implementation of <see cref="IFileDialogService"/> using the native
/// <see cref="OpenFolderDialog"/> introduced in .NET 8.
/// </summary>
/// <remarks>
/// <para>
/// The dialog is modal and blocks the calling thread. It therefore must be
/// invoked from the UI thread; the implementation enforces this by checking
/// <see cref="Application.Current"/>'s dispatcher access.
/// </para>
/// <para>
/// The service is stateless and safe for concurrent calls, although in
/// practice dialogs are always invoked one at a time from the UI thread.
/// </para>
/// </remarks>
public sealed class WpfFileDialogService : IFileDialogService
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The method is called from a non-UI thread.
    /// </exception>
    public string? PickFolder(string title, string? initialDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (Application.Current?.Dispatcher.CheckAccess() != true)
        {
            throw new InvalidOperationException(
                "File dialogs must be shown from the UI thread.");
        }

        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        bool? result = dialog.ShowDialog();

        return result == true ? dialog.FolderName : null;
    }
}