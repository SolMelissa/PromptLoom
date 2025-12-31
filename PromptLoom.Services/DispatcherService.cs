// FIX: Introduce dispatcher service wrapper to allow view models to be tested without WPF Dispatcher.
// CAUSE: Direct Application.Current.Dispatcher usage made tests require a WPF Application.
// CHANGE: Add IDispatcherService and a default WPF implementation. 2025-12-25

using System;
using System.Windows;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for dispatcher operations.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Returns true if the current thread has dispatcher access.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Invokes an action on the dispatcher thread.
    /// </summary>
    void Invoke(Action action);

    /// <summary>
    /// Begins an async invoke on the dispatcher thread.
    /// </summary>
    void BeginInvoke(Action action);
}

/// <summary>
/// WPF-backed dispatcher service.
/// </summary>
public sealed class DispatcherService : IDispatcherService
{
    /// <summary>
    /// Returns true if the current thread has dispatcher access.
    /// </summary>
    public bool CheckAccess()
    {
        var dispatcher = Application.Current?.Dispatcher;
        return dispatcher?.CheckAccess() ?? true;
    }

    /// <summary>
    /// Invokes an action on the dispatcher thread.
    /// </summary>
    public void Invoke(Action action)
    {
        if (action is null) return;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    /// <summary>
    /// Begins an async invoke on the dispatcher thread.
    /// </summary>
    public void BeginInvoke(Action action)
    {
        if (action is null) return;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }
}
