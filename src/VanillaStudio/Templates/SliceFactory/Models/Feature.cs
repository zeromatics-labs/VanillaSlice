using System.Text.Json.Serialization;
using {{RootNamespace}}.SliceFactory.Cli;
using {{RootNamespace}}.SliceFactory.Components.Pages;

namespace {{RootNamespace}}.SliceFactory.Models;

public class Feature
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ModuleNamespace { get; set; } = string.Empty;
    public string ProjectNamespace { get; set; } = string.Empty;
    public string PrimaryKeyType { get; set; } = string.Empty;

    /// <summary>
    /// Solution root at generation time. Not persisted — reconstructed at runtime
    /// via PathDetectionService so paths stay portable across machines.
    /// </summary>
    [JsonIgnore]
    public string BasePath { get; set; } = string.Empty;

    public string DirectoryName { get; set; } = string.Empty;

    // Per-slice descriptors — null means not generated
    public SliceDescriptor? Listing { get; set; }
    public SliceDescriptor? Form { get; set; }
    public SliceDescriptor? Action { get; set; }
    public SelectListDescriptor? SelectList { get; set; }

    public string UIFramework { get; set; } = "Bootstrap";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// JSON serialized configuration from webportal-profile.json
    /// </summary>
    public string? ProfileConfiguration { get; set; }

    public List<FeatureFile> Files { get; set; } = new();
    public List<FeatureProject> Projects { get; set; } = new();
}

/// <summary>
/// Represents a file generated for a feature
/// </summary>
public class FeatureFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FeatureId { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ProjectType { get; set; } = string.Empty;
    public string SliceType { get; set; } = string.Empty; // Form or Listing

    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Whether the file still exists on disk
    /// </summary>
    public bool Exists { get; set; } = true;
}

/// <summary>
/// Represents the relationship between a feature and project types
/// </summary>
public class FeatureProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FeatureId { get; set; } = string.Empty;

    public ProjectType ProjectType { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectNamespace { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tree node for hierarchical display
/// </summary>
public class FeatureTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Module, Feature, ProjectType, File
    public bool IsExpanded { get; set; }
    public bool HasChildren => Children.Any();
    public List<FeatureTreeNode> Children { get; set; } = new();

    // Optional data for different node types
    public Feature? Feature { get; set; }
    public FeatureFile? File { get; set; }
    public ProjectType? ProjectType { get; set; }
    public string? ModuleNamespace { get; set; }
}
