// TEST: Dispatcher service that executes actions immediately on the calling thread.

using System;
using PromptLoom.Services;

namespace PromptLoom.Tests.Fakes;

public sealed class ImmediateDispatcherService : IDispatcherService
{
    public bool CheckAccess() => true;

    public void Invoke(Action action)
    {
        action();
    }

    public void BeginInvoke(Action action)
    {
        action();
    }
}
