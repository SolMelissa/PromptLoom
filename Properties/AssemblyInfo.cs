// CHANGE LOG
// - 2025-12-29 | Request: Centralize versioning | Removed hardcoded assembly version attributes.
// - 2025-12-25 | Request: Allow test project access | Added InternalsVisibleTo for PromptLoom.Tests.
// - 2025-12-22 | Request: Align assembly versions | Updated assembly version attributes for WPF pack URI consistency.
//
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
[assembly: InternalsVisibleTo("PromptLoom.Tests")]

// WPF apps don't strictly need a TargetFrameworkAttribute at runtime, but some tooling expects it.
// Keep it here (once) since we disabled auto-generation.
[assembly: TargetFramework(".NETCoreApp,Version=v8.0", FrameworkDisplayName = ".NET 8.0")]
