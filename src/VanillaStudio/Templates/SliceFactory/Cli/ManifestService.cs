using System.Text.Json;

namespace {{RootNamespace}}.SliceFactory.Cli;

/// <summary>
/// Service for managing the slices manifest file
/// </summary>
public class ManifestService
{
    private readonly string _manifestPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ManifestService(string? basePath = null)
    {
        // Default to solution root or current directory
        var solutionRoot = basePath ?? FindSolutionRoot() ?? Directory.GetCurrentDirectory();
        _manifestPath = Path.Combine(solutionRoot, "slices-manifest.json");
    }

    /// <summary>
    /// Gets the manifest file path
    /// </summary>
    public string ManifestPath => _manifestPath;

    /// <summary>
    /// Load the manifest from disk, or create a new one if it doesn't exist
    /// </summary>
    public async Task<SliceManifest> LoadAsync()
    {
        if (!File.Exists(_manifestPath))
            return new SliceManifest();

        try
        {
            var json = await File.ReadAllTextAsync(_manifestPath);

            // Detect v1 by parsing the "version" field value
            using var probe = JsonDocument.Parse(json);
            var versionToken = probe.RootElement.TryGetProperty("version", out var vEl) ? vEl : default;
            bool isV1 = versionToken.ValueKind == JsonValueKind.String
                        || (versionToken.ValueKind == JsonValueKind.Number && versionToken.GetInt32() < 2);

            if (isV1)
            {
                var migrated = MigrateV1(probe.RootElement);
                // Back up v1 before overwriting
                var backupPath = _manifestPath.Replace(".json", ".v1.json");
                if (!File.Exists(backupPath))
                    await File.WriteAllTextAsync(backupPath, json);
                await SaveAsync(migrated);
                return migrated;
            }

            return JsonSerializer.Deserialize<SliceManifest>(json, JsonOptions)
                   ?? new SliceManifest();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not parse manifest file: {ex.Message}");
            return new SliceManifest();
        }
    }

    /// <summary>
    /// Save the manifest to disk
    /// </summary>
    public async Task SaveAsync(SliceManifest manifest)
    {
        manifest.LastUpdated = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(_manifestPath, json);
    }

    /// <summary>
    /// Migrate a v1 manifest to v2 format
    /// </summary>
    private static SliceManifest MigrateV1(JsonElement root)
    {
        var manifest = new SliceManifest { Version = 2 };

        if (!root.TryGetProperty("slices", out var slicesEl))
            return manifest;

        foreach (var el in slicesEl.EnumerateArray())
        {
            var prefix    = el.TryGetProperty("componentPrefix", out var p) ? p.GetString() ?? "" : "";
            var plural    = el.TryGetProperty("featurePluralName", out var pl) ? pl.GetString() ?? prefix + "s" : prefix + "s";
            var ns        = el.TryGetProperty("namespace", out var ns_) ? ns_.GetString() ?? "" : "";
            var dir       = el.TryGetProperty("directoryName", out var d) ? d.GetString() ?? "" : "";
            var pk        = el.TryGetProperty("primaryKeyType", out var pk_) ? pk_.GetString() ?? "Guid" : "Guid";
            var hasList   = el.TryGetProperty("generateListing", out var gl) && gl.GetBoolean();
            var hasForm   = el.TryGetProperty("generateForm", out var gf) && gf.GetBoolean();
            var hasSel    = el.TryGetProperty("generateSelectList", out var gs) && gs.GetBoolean();
            var modelType = el.TryGetProperty("selectListModelType", out var mt) ? mt.GetString() ?? "SelectOption" : "SelectOption";
            var dataType  = el.TryGetProperty("selectListDataType", out var dt) ? dt.GetString() ?? "string" : "string";
            var createdAt = el.TryGetProperty("createdAt", out var ca) ? ca.GetDateTime() : DateTime.UtcNow;
            var lastGen   = el.TryGetProperty("lastGeneratedAt", out var lg) ? lg.GetDateTime() : DateTime.UtcNow;
            var files     = el.TryGetProperty("generatedFiles", out var gf2)
                            ? gf2.EnumerateArray().Select(f => f.GetString() ?? "").ToList()
                            : new List<string>();

            var slice = new SliceDefinition
            {
                Id            = SliceDefinition.GenerateId(ns, dir),
                Namespace     = ns,
                Directory     = dir,
                PrimaryKeyType = pk,
                Listing       = hasList  ? new SliceDescriptor(plural, prefix) : null,
                Form          = hasForm  ? new SliceDescriptor(prefix, prefix) : null,
                SelectList    = hasSel   ? new SelectListDescriptor(prefix + " Types", prefix, modelType, dataType) : null,
                CreatedAt     = createdAt,
                LastGeneratedAt = lastGen,
                GeneratedFiles = files
            };

            manifest.Slices.Add(slice);
        }

        return manifest;
    }

    /// <summary>
    /// Add or update a slice in the manifest
    /// </summary>
    public async Task<SliceDefinition> AddOrUpdateSliceAsync(SliceDefinition slice, List<string>? generatedFiles = null)
    {
        var manifest = await LoadAsync();

        // Generate ID if not set
        if (string.IsNullOrEmpty(slice.Id))
        {
            slice.Id = SliceDefinition.GenerateId(slice.Namespace, slice.Directory);
        }

        // Find existing slice or add new
        var existingIndex = manifest.Slices.FindIndex(s => s.Id == slice.Id);
        if (existingIndex >= 0)
        {
            // Update existing
            slice.CreatedAt = manifest.Slices[existingIndex].CreatedAt;
            slice.LastGeneratedAt = DateTime.UtcNow;
            if (generatedFiles != null)
            {
                slice.GeneratedFiles = generatedFiles;
            }
            manifest.Slices[existingIndex] = slice;
        }
        else
        {
            // Add new
            slice.CreatedAt = DateTime.UtcNow;
            slice.LastGeneratedAt = DateTime.UtcNow;
            if (generatedFiles != null)
            {
                slice.GeneratedFiles = generatedFiles;
            }
            manifest.Slices.Add(slice);
        }

        await SaveAsync(manifest);
        return slice;
    }

    /// <summary>
    /// Get a slice by ID
    /// </summary>
    public async Task<SliceDefinition?> GetSliceAsync(string id)
    {
        var manifest = await LoadAsync();
        return manifest.Slices.FirstOrDefault(s =>
            s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all slices
    /// </summary>
    public async Task<List<SliceDefinition>> GetAllSlicesAsync()
    {
        var manifest = await LoadAsync();
        return manifest.Slices;
    }

    /// <summary>
    /// Remove a slice from the manifest
    /// </summary>
    public async Task<bool> RemoveSliceAsync(string id)
    {
        var manifest = await LoadAsync();
        var removed = manifest.Slices.RemoveAll(s =>
            s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) > 0;

        if (removed)
        {
            await SaveAsync(manifest);
        }

        return removed;
    }

    /// <summary>
    /// Create a SliceDefinition from CLI options
    /// </summary>
    public static SliceDefinition FromCliOptions(CliOptions options)
    {
        return new SliceDefinition
        {
            Id = SliceDefinition.GenerateId(options.Namespace!, options.ComponentPrefix!),
            ComponentPrefix = options.ComponentPrefix!,
            FeaturePluralName = options.FeaturePluralName!,
            Namespace = options.Namespace!,
            DirectoryName = options.DirectoryName!,
            PrimaryKeyType = options.PrimaryKeyType,
            GenerateForm = options.GenerateForm,
            GenerateListing = options.GenerateListing,
            GenerateSelectList = options.GenerateSelectList,
            SelectListModelType = options.SelectListModelType,
            SelectListDataType = options.SelectListDataType
        };
    }

    /// <summary>
    /// Find the solution root by looking for .sln files
    /// </summary>
    private static string? FindSolutionRoot()
    {
        var current = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(current);

        while (directory != null)
        {
            if (directory.GetFiles("*.sln").Length > 0)
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        return null;
    }
}
