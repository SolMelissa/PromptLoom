// FIX: Remove static Random.Shared usage from core logic to improve determinism in tests.
// CAUSE: Static RNG made prompt generation harder to control in unit tests.
// CHANGE: Add IRandomSource and a default implementation. 2025-12-25

using System;

namespace PromptLoom.Services;

/// <summary>
/// Factory for random number generators used by core logic.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Creates a random number generator. When <paramref name="seed"/> is provided,
    /// the generator should be deterministic.
    /// </summary>
    Random Create(int? seed);
}

/// <summary>
/// Default random source backed by System.Random.
/// </summary>
public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _shared = new Random();

    /// <summary>
    /// Creates a random number generator for the given seed or returns a shared instance.
    /// </summary>
    public Random Create(int? seed) => seed.HasValue ? new Random(seed.Value) : _shared;
}
