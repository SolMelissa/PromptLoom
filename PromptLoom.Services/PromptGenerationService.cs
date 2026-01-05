// CHANGE LOG
// - 2026-03-05 | Request: Decouple generation from views | Add prompt generation service using a selection provider.
using System;
using System.Collections.Generic;
using PromptLoom.Models;

namespace PromptLoom.Services;

/// <summary>
/// Provides the input selection for prompt generation.
/// </summary>
public interface IPromptSelectionProvider
{
    /// <summary>
    /// Returns the categories used for prompt generation.
    /// </summary>
    IReadOnlyList<CategoryModel> GetSelection();
}

/// <summary>
/// Default prompt generation entry point that uses a selection provider.
/// </summary>
public sealed class PromptGenerationService
{
    private readonly PromptEngine _engine;
    private readonly IPromptSelectionProvider _selectionProvider;

    public PromptGenerationService(PromptEngine engine, IPromptSelectionProvider selectionProvider)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _selectionProvider = selectionProvider ?? throw new ArgumentNullException(nameof(selectionProvider));
    }

    public PromptEngine.GenerateResult Generate(int? seed)
    {
        var categories = _selectionProvider.GetSelection();
        return _engine.Generate(categories, seed);
    }
}
