// FIX: Make batch timestamps testable by abstracting DateTime.Now behind a clock interface.
// CAUSE: Direct calls to DateTime.Now in view-models made time-dependent behavior hard to test.
// CHANGE: Add IClock and a default SystemClock. 2025-12-25

using System;

namespace PromptLoom.Services;

/// <summary>
/// Provides the current time.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current local time.
    /// </summary>
    DateTime Now { get; }
}

/// <summary>
/// Default clock implementation using system time.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>
    /// Gets the current local time.
    /// </summary>
    public DateTime Now => DateTime.Now;
}
