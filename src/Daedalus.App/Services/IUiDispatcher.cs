namespace Daedalus.App.Services;

/// <summary>
/// Abstraction over the WPF dispatcher, so that ViewModels can marshal
/// callbacks to the UI thread without referencing <c>System.Windows</c>.
/// </summary>
/// <remarks>
/// <para>
/// ViewModels never reference <see cref="System.Windows.Threading.Dispatcher"/>
/// directly. All UI-thread marshaling goes through this interface, which is
/// trivial to fake in unit tests (execute synchronously on the calling thread).
/// </para>
/// <para>
/// Implementations must be safe to call from any thread.
/// </para>
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>
    /// Determines whether the calling thread is the UI thread.
    /// </summary>
    /// <returns><c>true</c> if the calling thread has access to the UI.</returns>
    bool CheckAccess();

    /// <summary>
    /// Executes <paramref name="action"/> on the UI thread, asynchronously.
    /// If the calling thread is already the UI thread, the action is executed
    /// synchronously and a completed task is returned.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task that completes when the action has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <c>null</c>.</exception>
    Task InvokeAsync(Action action);

    /// <summary>
    /// Executes <paramref name="func"/> on the UI thread, asynchronously,
    /// and returns its result.
    /// </summary>
    /// <typeparam name="T">Return type.</typeparam>
    /// <param name="func">The function to execute.</param>
    /// <returns>A task with the result of <paramref name="func"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <c>null</c>.</exception>
    Task<T> InvokeAsync<T>(Func<T> func);
}