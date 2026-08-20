using System.Diagnostics;

namespace VanillaSlice.Tests;

/// <summary>
/// Shared child-process launcher for tests that shell out to `dotnet restore` / `dotnet build`
/// against generated projects (GeneratedProjectSmokeTests, AccountPageCompileTests).
///
/// Node reuse leaves MSBuild worker processes alive between builds; stale ones make subsequent
/// builds fail with MSB4216 ("Could not run the '...' task because MSBuild could not create or
/// connect to a task host"), which looks exactly like a template regression rather than what it
/// actually is. These tests must not depend on the health of a shared build server, so node
/// reuse is disabled unconditionally in the child process's own environment — not the ambient
/// one, since the point is that the test works regardless of how it was invoked.
/// </summary>
public static class DotNetProcessRunner
{
    public static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // See class remarks re: MSB4216. Set directly on the child process's environment so
        // this holds regardless of what the ambient environment does or doesn't set.
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = new Process { StartInfo = startInfo };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout + Environment.NewLine + stderr);
    }
}
