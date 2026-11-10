using System.Windows;
using System.Windows.Threading;

namespace Daedalus.App.Services;

/// <summary>
/// WPF implementation of <see cref="IUiDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// Resolves the dispatcher from <see cref="Application.Current"/>. The
/// dispatcher is captured once at construction to avoid repeated null-checks
/// and to make the dependency explicit: this class cannot be constructed
/// before <see cref="Application.Current"/> exists.
/// </para>
/// <para>
/// When the calling thread is already the UI thread, the delegate is executed
/// synchronously and a completed task is returned. This avoids unnecessary
/// queueing in the common case of UI-originated calls, and preserves the
/// exception semantics: exceptions propagate to the caller even in the
/// synchronous path.
/// </para>
/// </remarks>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="WpfUiDispatcher"/> class.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="Application.Current"/> is <c>null</c>, meaning the WPF
    /// application has not been initialized yet.
    /// </exception>
    public WpfUiDispatcher()
    {
        _dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                "WpfUiDispatcher cannot be created before Application.Current exists.");
    }

    /// <inheritdoc />
    public bool CheckAccess() => _dispatcher.CheckAccess();

    /// <inheritdoc />
    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcher.CheckAccess())
        {
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        return _dispatcher.InvokeAsync(action).Task;
    }

    /// <inheritdoc />
    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (_dispatcher.CheckAccess())
        {
            try
            {
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        return _dispatcher.InvokeAsync(func).Task;
    }
}