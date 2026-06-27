using System.Text.Json.Serialization;

namespace {{RootNamespace}}.SliceFactory.Cli;

public class SliceManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("slices")]
    public List<SliceDefinition> Slices { get; set; } = new();
}

public record SliceDescriptor
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("prefix")]
    public string Prefix { get; init; } = "";

    public SliceDescriptor() { }
    public SliceDescriptor(string name, string prefix) { Name = name; Prefix = prefix; }
}

public record SelectListDescriptor : SliceDescriptor
{
    [JsonPropertyName("modelType")]
    public string ModelType { get; init; } = "SelectOption";

    [JsonPropertyName("dataType")]
    public string DataType { get; init; } = "string";

    public SelectListDescriptor() { }

    public SelectListDescriptor(string name, string prefix,
        string modelType = "SelectOption", string dataType = "string")
        : base(name, prefix)
    {
        ModelType = modelType;
        DataType = dataType;
    }
}

public class SliceDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("directory")]
    public string Directory { get; set; } = string.Empty;

    [JsonPropertyName("primaryKeyType")]
    public string PrimaryKeyType { get; set; } = "Guid";

    [JsonPropertyName("listing")]
    public SliceDescriptor? Listing { get; set; }

    [JsonPropertyName("form")]
    public SliceDescriptor? Form { get; set; }

    [JsonPropertyName("action")]
    public SliceDescriptor? Action { get; set; }

    [JsonPropertyName("selectList")]
    public SelectListDescriptor? SelectList { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastGeneratedAt")]
    public DateTime LastGeneratedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("generatedFiles")]
    public List<string> GeneratedFiles { get; set; } = new();

    /// <summary>
    /// Unique ID derived from namespace + directory path.
    /// Stable across renames of individual slice prefixes within the same feature directory.
    /// </summary>
    public static string GenerateId(string ns, string directory)
    {
        var normalizedDir = directory.Replace('/', '-').Replace('\\', '-')
                                     .Trim('-').ToLowerInvariant();
        return $"{ns.ToLowerInvariant().Replace(".", "-")}-{normalizedDir}";
    }
}
