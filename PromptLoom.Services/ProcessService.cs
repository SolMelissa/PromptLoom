// FIX: Introduce process service wrapper to allow view models to be tested without Process.Start.
// CAUSE: Direct Process.Start calls in view models were not mockable.
// CHANGE: Add IProcessService and a default implementation. 2025-12-25

using System.Diagnostics;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for starting processes.
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Starts a process using the provided start info.
    /// </summary>
    void Start(ProcessStartInfo startInfo);
}

/// <summary>
/// Default process service backed by Process.Start.
/// </summary>
public sealed class ProcessService : IProcessService
{
    /// <summary>
    /// Starts a process using Process.Start.
    /// </summary>
    public void Start(ProcessStartInfo startInfo) => Process.Start(startInfo);
}
