// CHANGE LOG
// - 2026-01-02 | Request: Patch version bump | Update assembly version to 1.8.2.9.
// - 2025-12-25 | Fix: Allow test project to access internal members needed for smoke tests.
// - 2025-12-22 | Fix: Align AssemblyVersion/FileVersion/InformationalVersion with the csproj version.
// FIX: Allow test project to access internal members needed for smoke tests.
// CAUSE: QueueRecomputePrompt is internal and not visible to PromptLoom.Tests, blocking compilation.
// CHANGE: Add InternalsVisibleTo for PromptLoom.Tests. 2025-12-25
// FIX: WPF startup crash FileNotFoundException for 'PromptLoom, Version=1.8.2.0'
// CAUSE: AssemblyInfo.cs hardcoded version 1.8.0.5 while the project version was 1.8.2.0;
//        WPF BAML pack URIs bind to assembly identity (including version) and failed to resolve.
// CHANGE: Align AssemblyVersion/FileVersion/InformationalVersion with the csproj version.
// DATE: 2025-12-22

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("PromptLoom")]
[assembly: AssemblyCompany("PromptLoom")]
[assembly: AssemblyProduct("PromptLoom")]
[assembly: AssemblyVersion("1.8.2.9")]
[assembly: AssemblyFileVersion("1.8.2.9")]
[assembly: AssemblyInformationalVersion("1.8.2.9")]
[assembly: InternalsVisibleTo("PromptLoom.Tests")]

// WPF apps don't strictly need a TargetFrameworkAttribute at runtime, but some tooling expects it.
// Keep it here (once) since we disabled auto-generation.
[assembly: TargetFramework(".NETCoreApp,Version=v8.0", FrameworkDisplayName = ".NET 8.0")]
