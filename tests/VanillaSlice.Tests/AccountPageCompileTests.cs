using Xunit;
using ZKnow.VanillaStudio.Models;

namespace VanillaSlice.Tests;

/// <summary>
/// Compiles the HybridApp account screens (Login/Register/Logout/ForgotPassword) against each
/// UI framework's real package assembly.
///
/// These four screens live only under
/// Templates/HybridApp/Components/Pages/Account, each with five UIFramework-conditional
/// markup branches. GeneratedProjectSmokeTests does NOT exercise this markup at all — it sets
/// IncludeHybridMaui = false, so the HybridApp project (and therefore these files) is never
/// even generated, regardless of which UIFramework values its [Theory] covers. Extending that
/// existing Theory to five frameworks would report safety that test does not have.
///
/// Building the real HybridApp.csproj is not a viable fix either: it multi-targets
/// net10.0-android;net10.0-ios;net10.0-maccatalyst (plus net10.0-windows... on Windows), which
/// needs the full MAUI toolchain — Android SDK, and a Mac for iOS/MacCatalyst — that a CI-style
/// test run cannot assume.
///
/// So this test takes the cheaper route the task called out as acceptable: generate the real
/// solution (IncludeHybridMaui = true, so the Account pages actually render with real
/// ProjectName substitution), then drop a throwaway, plain net10.0 Razor Class Library project
/// next to it that:
///   - references the generated {ProjectName}.Client.Shared.csproj — the plain C# project that
///     supplies MauiAuthenticationStateProvider / IdentityClient the pages @inject. It has no
///     MAUI dependency and is already built today by GeneratedProjectSmokeTests, so it is not
///     part of what is under test here.
///   - copies in the four generated Account .razor files verbatim (byte-for-byte what HybridApp
///     would compile)
///   - references the UI framework's real NuGet package at the version pinned in the csproj
///     templates (FluentUI 4.12.1, MudBlazor 7.8.0, Radzen.Blazor 5.2.12)
///
/// This compiles the exact markup at risk without needing the MAUI workload.
/// </summary>
[Trait("Category", "Smoke")]
public class AccountPageCompileTests
{
    private static readonly string[] AccountFiles = ["Login.razor", "Register.razor", "Logout.razor", "ForgotPassword.razor"];

    [Theory]
    [InlineData(UIFramework.Bootstrap)]
    [InlineData(UIFramework.FluentUI)]
    [InlineData(UIFramework.MudBlazor)]
    [InlineData(UIFramework.Radzen)]
    [InlineData(UIFramework.TailwindCSS)]
    public async Task Hybrid_account_pages_compile(UIFramework uiFramework)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"vs-account-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var keepForInspection = false;

        try
        {
            var config = new ProjectConfiguration
            {
                ProjectName = "AccountProbe",
                RootNamespace = "AccountProbe",
                OutputDirectory = outputDirectory,
                DotNetVersion = DotNetVersion.Net10,
                DatabaseProvider = DatabaseProvider.SQLite,
                UIFramework = uiFramework,
                // Auto is the only mode that generates WebPortal.Client, kept here only so
                // generation matches the shape GeneratedProjectSmokeTests exercises.
                RenderingMode = RenderingMode.Auto,
                IncludeAuthentication = true,
                EmailProvider = EmailProvider.Dev,
                IncludeWebProject = true,
                IncludeHybridMaui = true,   // the only source of the markup under test
                IncludeMauiNative = false,
                UseAspireOrchestration = false,
            };

            await GeneratedProjectHarness.GenerateToDiskAsync(config);

            var probeDir = WriteProbeProject(outputDirectory, config);

            var (restoreExit, restoreOutput) = await DotNetProcessRunner.RunAsync(
                "dotnet", $"restore \"{probeDir}\"", probeDir);
            if (restoreExit != 0)
            {
                keepForInspection = true;
            }
            Assert.True(restoreExit == 0,
                $"Account pages ({uiFramework}) probe project failed to restore. Output left at: {outputDirectory}{Environment.NewLine}{restoreOutput}");

            // --nodereuse:false belt-and-braces MSBUILDDISABLENODEREUSE (see DotNetProcessRunner):
            // it stops this specific build from leaving a fresh node behind for the next one to
            // trip over, on top of refusing to reuse a stale one.
            var (buildExit, buildOutput) = await DotNetProcessRunner.RunAsync(
                "dotnet", $"build \"{probeDir}\" --no-restore -warnaserror:CS0246 --nodereuse:false", probeDir);

            if (buildExit != 0)
            {
                keepForInspection = true;
            }

            Assert.True(buildExit == 0,
                $"Account pages ({uiFramework}) failed to compile. Output left at: {outputDirectory}{Environment.NewLine}{buildOutput}");
        }
        finally
        {
            if (!keepForInspection)
            {
                try { Directory.Delete(outputDirectory, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    private static string WriteProbeProject(string outputDirectory, ProjectConfiguration config)
    {
        var hybridAccountDir = Path.Combine(
            outputDirectory, $"{config.ProjectName}.HybridApp", "Components", "Pages", "Account");
        Assert.True(Directory.Exists(hybridAccountDir),
            $"Generated HybridApp Account folder not found at {hybridAccountDir}. " +
            "Generation must have changed shape — this probe copies real generated markup, it does not author its own.");

        var probeDir = Path.Combine(outputDirectory, "AccountMarkupProbe");
        var pagesDir = Path.Combine(probeDir, "Pages", "Account");
        Directory.CreateDirectory(pagesDir);

        foreach (var file in AccountFiles)
        {
            File.Copy(Path.Combine(hybridAccountDir, file), Path.Combine(pagesDir, file));
        }

        var uiUsing = config.UIFramework switch
        {
            UIFramework.FluentUI => "@using Microsoft.FluentUI.AspNetCore.Components" + Environment.NewLine,
            UIFramework.MudBlazor => "@using MudBlazor" + Environment.NewLine,
            UIFramework.Radzen => "@using Radzen" + Environment.NewLine + "@using Radzen.Blazor" + Environment.NewLine,
            _ => string.Empty,
        };

        File.WriteAllText(Path.Combine(probeDir, "_Imports.razor"),
            "@using System.Net.Http" + Environment.NewLine +
            "@using Microsoft.AspNetCore.Components.Authorization" + Environment.NewLine +
            "@using Microsoft.AspNetCore.Components.Forms" + Environment.NewLine +
            "@using Microsoft.AspNetCore.Components.Routing" + Environment.NewLine +
            "@using Microsoft.AspNetCore.Components.Web" + Environment.NewLine +
            uiUsing);

        var uiPackageRef = config.UIFramework switch
        {
            UIFramework.FluentUI =>
                "<PackageReference Include=\"Microsoft.FluentUI.AspNetCore.Components\" Version=\"4.12.1\" />",
            UIFramework.MudBlazor =>
                "<PackageReference Include=\"MudBlazor\" Version=\"7.8.0\" />",
            UIFramework.Radzen =>
                "<PackageReference Include=\"Radzen.Blazor\" Version=\"5.2.12\" />",
            _ => string.Empty,
        };

        File.WriteAllText(Path.Combine(probeDir, "AccountMarkupProbe.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="{config.AspNetCoreVersion}" />
                <PackageReference Include="Microsoft.AspNetCore.Components.Authorization" Version="{config.AspNetCoreVersion}" />
                {uiPackageRef}
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="..\{config.ProjectName}.Platform\{config.ProjectName}.Client.Shared\{config.ProjectName}.Client.Shared.csproj" />
              </ItemGroup>
            </Project>
            """);

        return probeDir;
    }
}
