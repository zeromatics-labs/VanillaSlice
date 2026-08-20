using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using ZKnow.VanillaStudio.Models;
using ZKnow.VanillaStudio.Services;

namespace VanillaSlice.Tests;

/// <summary>
/// Locates the repository's Templates directory and runs the real TemplateEngineService
/// against it, so tests can assert on what a generated project actually contains.
/// </summary>
public static class TemplateTestFixture
{
    public static string TemplatesPath { get; } = Locate();

    private static string Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "VanillaStudio", "Templates");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate src/VanillaStudio/Templates walking up from {AppContext.BaseDirectory}");
    }

    public static List<GeneratedFile> Generate(
        string templateName,
        Dictionary<string, object> parameters,
        Func<string, bool>? includeFile = null)
    {
        var engine = new TemplateEngineService(
            NullLogger<TemplateEngineService>.Instance, TemplatesPath);
        return engine.GenerateFromTemplateAsync(templateName, parameters, "", includeFile)
                     .GetAwaiter().GetResult();
    }

    /// <summary>Content of the single generated file whose path ends with <paramref name="endsWith"/>.</summary>
    public static string FileContent(List<GeneratedFile> files, string endsWith)
    {
        var match = files.SingleOrDefault(f =>
            f.RelativePath.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new InvalidOperationException(
                $"No generated file ends with '{endsWith}'. Got:{Environment.NewLine}" +
                string.Join(Environment.NewLine, files.Select(f => f.RelativePath)));
        return match.Content;
    }

    public static bool HasFile(List<GeneratedFile> files, string endsWith) =>
        files.Any(f => f.RelativePath.EndsWith(endsWith, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Runs the full generation pipeline and writes the result to config.OutputDirectory.</summary>
public static class GeneratedProjectHarness
{
    public static async Task GenerateToDiskAsync(ProjectConfiguration config)
    {
        var service = BuildGenerationService();
        var result = await service.GenerateProjectAsync(config);

        if (!result.Success)
            throw new InvalidOperationException(
                $"Generation failed: {result.Message}{Environment.NewLine}" +
                string.Join(Environment.NewLine, result.Errors));

        foreach (var file in result.GeneratedFiles)
        {
            var fullPath = Path.Combine(config.OutputDirectory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, file.Content);
        }
    }

    private static EnhancedProjectGenerationService BuildGenerationService()
    {
        var templateEngine = new TemplateEngineService(
            NullLogger<TemplateEngineService>.Instance, TemplateTestFixture.TemplatesPath);

        return new EnhancedProjectGenerationService(
            new StubWebHostEnvironment(),
            NullLogger<EnhancedProjectGenerationService>.Instance,
            templateEngine,
            new TemplateBasedFrameworkCoreGenerator(
                templateEngine, NullLogger<TemplateBasedFrameworkCoreGenerator>.Instance),
            new TemplateBasedServerDataGenerator(
                templateEngine, NullLogger<TemplateBasedServerDataGenerator>.Instance),
            new TemplateBasedCommonGenerator(
                templateEngine, NullLogger<TemplateBasedCommonGenerator>.Instance),
            new PlatformProjectsGenerator(
                NullLogger<PlatformProjectsGenerator>.Instance, templateEngine),
            new InfrastructureProjectsGenerator(
                NullLogger<InfrastructureProjectsGenerator>.Instance, templateEngine),
            new WebPortalProjectsGenerator(
                NullLogger<WebPortalProjectsGenerator>.Instance, templateEngine),
            new HybridAppProjectsGenerator(
                templateEngine, NullLogger<HybridAppProjectsGenerator>.Instance),
            new MauiNativeAppProjectsGenerator(
                templateEngine, NullLogger<MauiNativeAppProjectsGenerator>.Instance),
            new ProjectValidationService(NullLogger<ProjectValidationService>.Instance));
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "VanillaSlice.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    }
}
