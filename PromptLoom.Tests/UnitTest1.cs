// TEST: Add baseline PromptEngine coverage and view model smoke tests using fakes.

using System;
using System.IO;
using PromptLoom.Models;
using PromptLoom.Services;
using PromptLoom.Tests.Fakes;
using PromptLoom.ViewModels;

namespace PromptLoom.Tests;

/// <summary>
/// Unit tests for core prompt generation logic.
/// </summary>
public class UnitTest1
{
    /// <summary>
    /// Generates a prompt from a single category/subcategory/entry and validates prefix/suffix ordering.
    /// </summary>
    [Fact]
    public void Generate_PromptFromSingleEntry_PreservesAffixOrder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PromptLoomTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "entry.txt");
        File.WriteAllText(filePath, "sparkle");

        try
        {
            var entry = new PromptEntryModel("entry.txt", filePath)
            {
                Enabled = true,
                Order = 0
            };
            var sub = new SubCategoryModel("Sub", tempDir)
            {
                Enabled = true,
                Prefix = "sub-pre",
                Suffix = "sub-suf",
                UseAllTxtFiles = false
            };
            sub.Entries.Add(entry);
            sub.SelectedEntry = entry;

            var cat = new CategoryModel("Cat", tempDir)
            {
                Enabled = true,
                Prefix = "cat-pre",
                Suffix = "cat-suf",
                Order = 0
            };
            cat.SubCategories.Add(sub);

            var engine = new PromptEngine();
            var result = engine.Generate(new[] { cat }, seed: 123);

            Assert.Equal("cat-pre sub-pre sparkle sub-suf cat-suf", result.Prompt);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; test output is deterministic regardless.
            }
        }
    }
}

public class MainViewModelSmokeTests
{
    [Fact]
    public void CopyCommand_SetsClipboardText()
    {
        var clipboard = new FakeClipboardService();
        var dispatcher = new ImmediateDispatcherService();
        var vm = new MainViewModel(
            clipboard: clipboard,
            dispatcher: dispatcher);

        vm.PromptText = "hello";
        vm.CopyCommand.Execute(null);

        Assert.Equal("hello", clipboard.LastText);
        Assert.Equal("Copied!", vm.CopyButtonText);
    }

    [Fact]
    public void OpenSwarmUiCommand_StartsProcess()
    {
        var process = new FakeProcessService();
        var dispatcher = new ImmediateDispatcherService();
        var vm = new MainViewModel(
            process: process,
            dispatcher: dispatcher);

        vm.SwarmUrl = "http://127.0.0.1:7801";
        vm.OpenSwarmUiCommand.Execute(null);

        Assert.NotNull(process.LastStartInfo);
        Assert.Equal(vm.SwarmUrl, process.LastStartInfo?.FileName);
    }

    [Fact]
    public void QueueRecomputePrompt_UsesDispatcher()
    {
        var dispatcher = new ImmediateDispatcherService();
        var vm = new MainViewModel(dispatcher: dispatcher);

        vm.QueueRecomputePrompt(immediate: true);

        Assert.NotNull(vm.PromptText);
    }
}
