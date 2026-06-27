using {{RootNamespace}}.SliceFactory.Models;
using {{RootNamespace}}.SliceFactory.Data;

namespace {{RootNamespace}}.SliceFactory.Services;

public class PlacementGuidanceService
{
    private readonly JsonFeatureStore _store;
    private readonly FeatureManagementService _featureService;

    public PlacementGuidanceService(JsonFeatureStore store, FeatureManagementService featureService)
    {
        _store = store;
        _featureService = featureService;
    }

    public async Task<PlacementGuidance> AnalyzePlacementAsync(
        string componentPrefix,
        string moduleNamespace,
        string projectNamespace,
        string basePath,
        List<FeatureFilePreview> newFiles)
    {
        var guidance = new PlacementGuidance
        {
            NewFiles = newFiles
        };

        // Get existing features for conflict analysis
        var existingFeatures = _store.Features
            .Where(f => f.ModuleNamespace == moduleNamespace
                     || f.Listing?.Prefix == componentPrefix || f.Form?.Prefix == componentPrefix
                     || f.Action?.Prefix == componentPrefix || f.SelectList?.Prefix == componentPrefix)
            .ToList();

        // Analyze conflicts
        await AnalyzeConflicts(guidance, componentPrefix, moduleNamespace, existingFeatures, newFiles);

        // Build namespace hierarchy
        await BuildNamespaceHierarchy(guidance, moduleNamespace, projectNamespace, existingFeatures);

        // Generate suggestions
        await GenerateSuggestions(guidance, componentPrefix, moduleNamespace, existingFeatures);

        // Analyze existing files in target directories
        await AnalyzeExistingFiles(guidance, basePath, newFiles);

        return guidance;
    }

    private async Task AnalyzeConflicts(
        PlacementGuidance guidance,
        string componentPrefix,
        string moduleNamespace,
        List<Feature> existingFeatures,
        List<FeatureFilePreview> newFiles)
    {
        // Check for duplicate feature names
        var duplicateFeature = existingFeatures.FirstOrDefault(f =>
            f.DirectoryName.Equals(componentPrefix, StringComparison.OrdinalIgnoreCase) &&
            f.ModuleNamespace.Equals(moduleNamespace, StringComparison.OrdinalIgnoreCase));

        if (duplicateFeature != null)
        {
            guidance.Conflicts.Add(new ConflictWarning
            {
                Type = ConflictType.DuplicateFeatureName,
                Severity = ConflictSeverity.Error,
                Message = $"Feature '{componentPrefix}' already exists in namespace '{moduleNamespace}'",
                Details = $"Created on {duplicateFeature.CreatedAt:MMM dd, yyyy}",
                Suggestions = new List<string>
                {
                    $"{componentPrefix}2",
                    $"{componentPrefix}New",
                    $"{componentPrefix}Extended"
                }
            });
        }

        // Check for file overwrites
        foreach (var newFile in newFiles)
        {
            var existingFile = existingFeatures
                .SelectMany(f => f.Files)
                .FirstOrDefault(ef => ef.FilePath.Equals(newFile.FilePath, StringComparison.OrdinalIgnoreCase));

            if (existingFile != null)
            {
                guidance.Conflicts.Add(new ConflictWarning
                {
                    Type = ConflictType.FileOverwrite,
                    Severity = ConflictSeverity.Warning,
                    Message = $"File '{newFile.FileName}' will overwrite existing file",
                    Details = $"Existing file: {existingFile.FilePath}",
                    FilePath = newFile.FilePath,
                    Suggestions = new List<string>
                    {
                        "Choose a different component prefix",
                        "Use a different namespace",
                        "Backup existing file before generation"
                    }
                });
            }
        }

        // Check naming conventions
        if (!IsValidNamingConvention(componentPrefix))
        {
            guidance.Conflicts.Add(new ConflictWarning
            {
                Type = ConflictType.NamingConvention,
                Severity = ConflictSeverity.Warning,
                Message = "Component prefix doesn't follow recommended naming conventions",
                Details = "Use PascalCase without spaces or special characters",
                Suggestions = new List<string>
                {
                    ToPascalCase(componentPrefix),
                    RemoveSpecialCharacters(componentPrefix)
                }
            });
        }
    }

    private async Task BuildNamespaceHierarchy(
        PlacementGuidance guidance,
        string moduleNamespace,
        string projectNamespace,
        List<Feature> existingFeatures)
    {
        guidance.NamespaceStructure.RootNamespace = moduleNamespace;

        // Build hierarchy from existing features
        var namespaceGroups = existingFeatures
            .GroupBy(f => f.ModuleNamespace)
            .OrderBy(g => g.Key);

        foreach (var group in namespaceGroups)
        {
            var moduleNode = new NamespaceNode
            {
                Name = group.Key,
                FullPath = group.Key,
                Type = NodeType.Module,
                ExistingFeatureCount = group.Count(),
                IsNew = group.Key == moduleNamespace && !existingFeatures.Any(f => f.ModuleNamespace == moduleNamespace)
            };

            // Add features under this module
            foreach (var feature in group)
            {
                var featureNode = new NamespaceNode
                {
                    Name = feature.Listing?.Prefix ?? feature.Form?.Prefix
                        ?? feature.Action?.Prefix ?? feature.SelectList?.Prefix ?? feature.DirectoryName,
                    FullPath = $"{feature.ModuleNamespace}.{feature.DirectoryName}",
                    Type = NodeType.Feature,
                    ExistingFeatureCount = 1
                };

                moduleNode.Children.Add(featureNode);
            }

            guidance.NamespaceStructure.Nodes.Add(moduleNode);
        }
    }

    private async Task GenerateSuggestions(
        PlacementGuidance guidance,
        string componentPrefix,
        string moduleNamespace,
        List<Feature> existingFeatures)
    {
        // Suggest alternative names if conflicts exist
        if (guidance.HasConflicts)
        {
            guidance.Suggestions.Add(new PlacementSuggestion
            {
                Type = SuggestionType.AlternativeName,
                Title = "Alternative Component Names",
                Description = "Consider these alternative names to avoid conflicts",
                RecommendedAction = "Change the component prefix to one of the suggested alternatives",
                Parameters = new Dictionary<string, string>
                {
                    ["alternatives"] = string.Join(", ", GenerateAlternativeNames(componentPrefix, existingFeatures))
                }
            });
        }

        // Suggest namespace organization
        var featuresInNamespace = existingFeatures.Count(f => f.ModuleNamespace == moduleNamespace);
        if (featuresInNamespace > 5)
        {
            guidance.Suggestions.Add(new PlacementSuggestion
            {
                Type = SuggestionType.AlternativeNamespace,
                Title = "Consider Sub-namespace",
                Description = $"The '{moduleNamespace}' namespace already contains {featuresInNamespace} features",
                RecommendedAction = "Consider creating a sub-namespace for better organization",
                Parameters = new Dictionary<string, string>
                {
                    ["suggestion"] = $"{moduleNamespace}.{GetCategoryFromPrefix(componentPrefix)}"
                }
            });
        }

        // Best practice suggestions
        guidance.Suggestions.Add(new PlacementSuggestion
        {
            Type = SuggestionType.BestPractice,
            Title = "Naming Best Practices",
            Description = "Follow these conventions for better maintainability",
            RecommendedAction = "Use descriptive, PascalCase names that clearly indicate the feature's purpose",
            Parameters = new Dictionary<string, string>
            {
                ["example"] = $"{ToPascalCase(componentPrefix)}Management"
            }
        });
    }

    private async Task AnalyzeExistingFiles(
        PlacementGuidance guidance,
        string basePath,
        List<FeatureFilePreview> newFiles)
    {
        foreach (var newFile in newFiles)
        {
            var fullPath = Path.Combine(basePath, newFile.DirectoryPath, newFile.FileName);

            if (File.Exists(fullPath))
            {
                var fileInfo = new FileInfo(fullPath);
                guidance.ExistingFiles.Add(new ExistingFileInfo
                {
                    FilePath = fullPath,
                    FileName = newFile.FileName,
                    ProjectType = newFile.ProjectType,
                    SliceType = newFile.SliceType,
                    LastModified = fileInfo.LastWriteTime,
                    FileSize = fileInfo.Length,
                    WillBeOverwritten = true
                });
            }
        }
    }

    private bool IsValidNamingConvention(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               char.IsUpper(name[0]) &&
               name.All(c => char.IsLetterOrDigit(c)) &&
               !name.Contains(' ');
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var words = input.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", words.Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower()));
    }

    private string RemoveSpecialCharacters(string input)
    {
        return new string(input.Where(c => char.IsLetterOrDigit(c)).ToArray());
    }

    private List<string> GenerateAlternativeNames(string originalName, List<Feature> existingFeatures)
    {
        var alternatives = new List<string>();
        var existingNames = existingFeatures.Select(f => f.DirectoryName.ToLower()).ToHashSet();

        // Generate numbered alternatives
        for (int i = 2; i <= 5; i++)
        {
            var candidate = $"{originalName}{i}";
            if (!existingNames.Contains(candidate.ToLower()))
                alternatives.Add(candidate);
        }

        // Generate semantic alternatives
        var semanticSuffixes = new[] { "New", "Extended", "Advanced", "Pro", "Plus" };
        foreach (var suffix in semanticSuffixes)
        {
            var candidate = $"{originalName}{suffix}";
            if (!existingNames.Contains(candidate.ToLower()))
                alternatives.Add(candidate);
        }

        return alternatives.Take(3).ToList();
    }

    private string GetCategoryFromPrefix(string prefix)
    {
        // Simple categorization based on common patterns
        if (prefix.ToLower().Contains("user")) return "Users";
        if (prefix.ToLower().Contains("product")) return "Products";
        if (prefix.ToLower().Contains("order")) return "Orders";
        if (prefix.ToLower().Contains("report")) return "Reports";

        return "Features";
    }
}
