using System.Diagnostics;
using Xunit;
using ZKnow.VanillaStudio.Models;

namespace VanillaSlice.Tests;

/// <summary>
/// Generates a real project and builds it. Guards the claim that the wizard's
/// "Authentication ✅" actually produces a compiling, runnable application.
///
/// SQLite and .NET 10 are deliberate: no external database server is required,
/// and no .NET 9 runtime is assumed to be installed. RenderingMode.Auto is
/// deliberate too: it is the only mode that generates WebPortal.Client, which
/// owns the Routes component App.razor renders.
///
/// Runs against both Bootstrap and Tailwind CSS (spec §7 minimum). FluentUI,
/// MudBlazor and Radzen use third-party component libraries whose APIs could
/// not be verified offline, so they are not covered here.
/// </summary>
[Trait("Category", "Smoke")]
public class GeneratedProjectSmokeTests
{
    [Theory]
    [InlineData(UIFramework.Bootstrap)]
    [InlineData(UIFramework.TailwindCSS)]
    public async Task Generated_project_with_identity_builds(UIFramework uiFramework)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"vs-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var keepForInspection = false;

        try
        {
            var config = new ProjectConfiguration
            {
                ProjectName = "SmokeTest",
                RootNamespace = "SmokeTest",
                OutputDirectory = outputDirectory,
                DotNetVersion = DotNetVersion.Net10,
                DatabaseProvider = DatabaseProvider.SQLite,
                UIFramework = uiFramework,
                // Auto is the only mode that generates WebPortal.Client, which owns the
                // Routes component App.razor renders. See the note in Task 13.
                RenderingMode = RenderingMode.Auto,
                IncludeAuthentication = true,
                EmailProvider = EmailProvider.Dev,
                IncludeWebProject = true,
                IncludeHybridMaui = false,   // requires the MAUI workload
                IncludeMauiNative = false,   // requires the MAUI workload
                UseAspireOrchestration = false,
            };

            await GeneratedProjectHarness.GenerateToDiskAsync(config);

            var solutionFile = Directory.GetFiles(outputDirectory, "*.sln", SearchOption.TopDirectoryOnly)
                .SingleOrDefault();
            Assert.True(solutionFile is not null,
                $"No .sln file was generated in {outputDirectory}. Contents:{Environment.NewLine}" +
                string.Join(Environment.NewLine, Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories)));

            var (exitCode, output) = await RunAsync(
                "dotnet",
                $"build \"{solutionFile}\" -warnaserror:CS0246",
                outputDirectory);

            if (exitCode != 0)
            {
                keepForInspection = true;
            }

            Assert.True(exitCode == 0,
                $"Generated project ({uiFramework}) failed to build. Output left at: {outputDirectory}{Environment.NewLine}{output}");
        }
        finally
        {
            if (!keepForInspection)
            {
                try { Directory.Delete(outputDirectory, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout + Environment.NewLine + stderr);
    }
}
