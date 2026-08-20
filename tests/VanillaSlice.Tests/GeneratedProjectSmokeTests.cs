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
/// Runs against both Bootstrap and Tailwind CSS (spec §7 minimum) with
/// IncludeHybridMaui = false, so this test never generates — and never compiles —
/// HybridApp (net10.0-android;net10.0-ios;net10.0-maccatalyst needs a full MAUI
/// toolchain this test cannot assume). That means it does NOT exercise the
/// UIFramework-conditional Account screens under Templates/HybridApp/Components/Pages/Account
/// (Login/Register/Logout/ForgotPassword) for any framework, Bootstrap and Tailwind included —
/// extending this Theory would not add coverage for that markup. FluentUI, MudBlazor and
/// Radzen are covered for that markup instead by AccountPageCompileTests, which compiles a
/// throwaway net10.0 Razor Class Library containing the real generated Account pages against
/// each framework's real pinned package version, without needing the MAUI workload.
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

            // --nodereuse:false belt-and-braces MSBUILDDISABLENODEREUSE (see DotNetProcessRunner):
            // it stops this specific build from leaving a fresh node behind for the next one to
            // trip over, on top of refusing to reuse a stale one.
            var (exitCode, output) = await DotNetProcessRunner.RunAsync(
                "dotnet",
                $"build \"{solutionFile}\" -warnaserror:CS0246 --nodereuse:false",
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
}
