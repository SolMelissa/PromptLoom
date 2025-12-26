// TEST: Fake process service for view model smoke tests.

using System.Diagnostics;
using PromptLoom.Services;

namespace PromptLoom.Tests.Fakes;

public sealed class FakeProcessService : IProcessService
{
    public ProcessStartInfo? LastStartInfo { get; private set; }

    public void Start(ProcessStartInfo startInfo)
    {
        LastStartInfo = startInfo;
    }
}
