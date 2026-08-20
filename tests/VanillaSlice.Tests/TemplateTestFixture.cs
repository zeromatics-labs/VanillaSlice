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
