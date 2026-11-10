using System.Windows;
using Wpf.Ui.Controls;

namespace Daedalus.App.Views;

/// <summary>
/// Main application window, styled with WPF UI (Fluent Design).
/// </summary>
/// <remarks>
/// Contains only <see cref="InitializeComponent"/>. The DataContext is set
/// by the composition root, not here, to keep the file free of logic and
/// preserve the strict no-code-behind constraint.
/// </remarks>
public partial class MainWindow : FluentWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}