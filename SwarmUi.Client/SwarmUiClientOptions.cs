/*
Build notes (2025-12-22):
- No build/runtime errors observed in this library template yet.
- If you see HTTP 401/403 from SwarmUI, the fix is usually to provide SwarmToken (cookie `swarm_token`)
  and ensure BaseUrl matches the SwarmUI host/port.

// FIX: Build error CS0236 in SwarmUiClientOptions | CAUSE: property name 'Timeout' shadowed System.Threading.Timeout in initializer
// CHANGE: Fully-qualify System.Threading.Timeout.InfiniteTimeSpan | DATE: 2025-12-22
*/
using System.Threading;

namespace SwarmUi.Client;

/// <summary>
/// Options for configuring <see cref="SwarmUiClient"/>.
/// </summary>
public sealed class SwarmUiClientOptions
{
    /// <summary>
    /// Base URL of SwarmUI, for example: http://127.0.0.1:7801/
    /// </summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>
    /// Optional Swarm authentication cookie value. If SwarmUI auth is enabled,
    /// you must supply the swarm_token cookie value for a valid account.
    /// </summary>
    public string? SwarmToken { get; init; }

    /// <summary>
    /// HTTP request timeout.
    /// </summary>
    public TimeSpan Timeout { get; init; } = System.Threading.Timeout.InfiniteTimeSpan;

    /// <summary>
    /// If true, the client will automatically acquire a new session_id when needed.
    /// </summary>
    public bool AutoSession { get; init; } = true;

    /// <summary>
    /// Optional user-agent string.
    /// </summary>
    public string UserAgent { get; init; } = "SwarmUiBridge/1.0 (C#)";
}
