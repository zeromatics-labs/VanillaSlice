# Action Slice + CLI Semantic Naming + Directory Tree Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the shared `--prefix`/`--plural` CLI flags with per-slice named flags (`--listing "Doctors"`, `--form "Doctor Profile"`, etc.), add a new Action slice type, upgrade the manifest to v2 with `SliceDescriptor` model, and replace the `DirectoryName` text field in the Web UI with an interactive directory tree picker built from the manifest.

**Architecture:** `NameDerivationService` (static, no DI) derives PascalCase prefixes from display names and is the single source of truth for name→prefix conversion. The manifest v2 schema replaces flat boolean+string fields with nullable `SliceDescriptor?` properties per slice type. `FeatureManagementService` generates per-slice parameter dictionaries so each slice type uses its own prefix independently. The `DirectoryTreePicker` Blazor component is a pure in-memory tree built from `ManifestService.GetAllSlicesAsync()` directory values.

**Tech Stack:** .NET 9, C#, Blazor Server, xUnit (test project), `System.Text.Json` (existing)

## Global Constraints

- All template source files live under `src/VanillaStudio/Templates/SliceFactory/` and use `{{RootNamespace}}` as the namespace placeholder — never use a concrete namespace in these files
- `src/VanillaStudio/ZKnow.VanillaStudio.csproj` excludes `Templates/**` from compilation — template files are text resources only, never reference them from the host project's compiled code
- Template placeholder pattern: `__ParameterName__` — double underscores, case-sensitive key
- `TemplateEngineService.ProcessTemplatesAsync(projectType, sliceType, parameters)` loads templates from `Templates/{projectType}/{sliceType}/` — Action templates go in `Templates/{projectType}/Action/`
- Preserve `CRLF` line endings in all generated files (existing `normalizedContent.Replace` logic handles this)
- `ManifestService` reads/writes `slices-manifest.json` at solution root
- No breaking changes to `regenerate-all` — v2 `ManifestService.LoadAsync()` auto-migrates v1 manifests

---

## File Map

### New files
| File | Responsibility |
|---|---|
| `src/VanillaStudio/Templates/SliceFactory/Services/NameDerivationService.cs` | Static: `DerivePrefix(name)` — PascalCase + singularize last word |
| `src/VanillaStudio/Templates/SliceFactory/Templates/ServiceContracts/Action/I__ComponentPrefix__ActionDataService.cs` | Action interface template |
| `src/VanillaStudio/Templates/SliceFactory/Templates/Controllers/Action/__ComponentPrefix__ActionController.cs` | Action controller template |
| `src/VanillaStudio/Templates/SliceFactory/Templates/ServerSideServices/Action/__ComponentPrefix__ActionServerDataService.cs` | Action server service template |
| `src/VanillaStudio/Templates/SliceFactory/Templates/ClientShared/Action/__ComponentPrefix__ActionClientDataService.cs` | Action client service template |
| `src/VanillaStudio/Templates/SliceFactory/Components/Shared/DirectoryTreePicker.razor` | Tree UI component |
| `src/VanillaStudio/Templates/SliceFactory/Components/Shared/DirectoryTreePicker.razor.cs` | Tree component codebehind |
| `tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj` | xUnit test project |
| `tests/VanillaSlice.Tests/NameDerivationTests.cs` | Unit tests for DerivePrefix |
| `tests/VanillaSlice.Tests/ManifestMigrationTests.cs` | Unit tests for v1→v2 migration logic |

### Modified files
| File | Change |
|---|---|
| `src/VanillaStudio/Templates/SliceFactory/Cli/SliceManifest.cs` | Add `SliceDescriptor`, `SelectListDescriptor`; rewrite `SliceDefinition` with nullable descriptor fields; update `GenerateId` |
| `src/VanillaStudio/Templates/SliceFactory/Cli/ManifestService.cs` | Add `MigrateIfNeeded()` in `LoadAsync()`; backup v1; update `FromCliOptions()` |
| `src/VanillaStudio/Templates/SliceFactory/Cli/CliOptions.cs` | Replace prefix/plural/bool flags with `ListingName?`, `FormName?`, `ActionName?`, `SelectListName?` |
| `src/VanillaStudio/Templates/SliceFactory/Cli/CliRunner.cs` | Use `NameDerivationService`; build `SliceDescriptor` per slice; update `GenerateAsync` / `RegenerateAsync` / `ListAsync` |
| `src/VanillaStudio/Templates/SliceFactory/Models/Feature.cs` | Replace `ComponentPrefix`/`FeaturePluralName`/`HasForm`/`HasListing`/`HasSelectList` with `SliceDescriptor?` properties |
| `src/VanillaStudio/Templates/SliceFactory/Services/TemplateEngineService.cs` | Add optional `componentPrefixPlural` param to `CreateParameterDictionary` |
| `src/VanillaStudio/Templates/SliceFactory/Services/FeatureManagementService.cs` | Per-slice parameter dicts; add Action handling; update `PreviewFeatureFilesAsync` and `GetAllExistingPreviewsAsync` |
| `src/VanillaStudio/Templates/SliceFactory/Components/Pages/Index.razor.cs` | Update `FormViewModel` — per-slice name fields; wire `DirectoryTreePicker` |
| `src/VanillaStudio/Templates/SliceFactory/Components/Pages/Index.razor` | Replace `DirectoryName` text input with `<DirectoryTreePicker>` |

---

## Task 1: NameDerivationService + Test Project

**Files:**
- Create: `src/VanillaStudio/Templates/SliceFactory/Services/NameDerivationService.cs`
- Create: `tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj`
- Create: `tests/VanillaSlice.Tests/NameDerivationTests.cs`

**Interfaces:**
- Produces: `NameDerivationService.DerivePrefix(string displayName): string` — used by CliRunner (Task 8) and ManifestService (Task 3)

- [ ] **Step 1: Create the test project**

Create `tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  </ItemGroup>
</Project>
```

Run: `dotnet new sln --name VanillaSlice.Tests --output tests/VanillaSlice.Tests` (if no solution exists in tests/). Add to root solution if one exists.

- [ ] **Step 2: Write the failing tests**

Create `tests/VanillaSlice.Tests/NameDerivationTests.cs`.

The test project cannot reference the template files (they use `{{RootNamespace}}` placeholder). Copy the service logic inline into the test file as a private nested class:

```csharp
namespace VanillaSlice.Tests;

public class NameDerivationTests
{
    // Inline copy of NameDerivationService for isolated testing.
    // Kept in sync with Templates/SliceFactory/Services/NameDerivationService.cs.
    private static string DerivePrefix(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        var words = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words[^1] = Singularize(words[^1]);
        return string.Concat(words.Select(PascalCase));
    }

    private static string PascalCase(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private static string Singularize(string word)
    {
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
            return word[..^3] + "y";
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            word.Length > 3)
            return word[..^1];
        return word;
    }

    [Theory]
    [InlineData("Doctors", "Doctor")]
    [InlineData("Doctor Profile", "DoctorProfile")]
    [InlineData("Disable Doctor", "DisableDoctor")]
    [InlineData("Doctor Types", "DoctorType")]
    [InlineData("Babies", "Baby")]
    [InlineData("Categories", "Category")]
    [InlineData("Matters", "Matter")]
    [InlineData("Cases", "Case")]
    [InlineData("Settings", "Setting")]
    [InlineData("Active Doctors", "ActiveDoctor")]
    public void DerivePrefix_ReturnsExpectedPrefix(string input, string expected)
    {
        Assert.Equal(expected, DerivePrefix(input));
    }

    [Fact]
    public void DerivePrefix_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => DerivePrefix(""));
        Assert.Throws<ArgumentException>(() => DerivePrefix("   "));
    }
}
```

- [ ] **Step 3: Run failing tests**

```
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj -v minimal
```

Expected: build error (NameDerivationTests references logic not yet implemented as a real service, but the inline version should compile — all tests should PASS immediately since the logic is inline).

- [ ] **Step 4: Create NameDerivationService.cs**

Create `src/VanillaStudio/Templates/SliceFactory/Services/NameDerivationService.cs`:

```csharp
namespace {{RootNamespace}}.SliceFactory.Services;

/// <summary>
/// Derives a PascalCase code prefix from a human-readable display name.
/// Called by CliRunner and ManifestService when building SliceDescriptors.
/// </summary>
public static class NameDerivationService
{
    /// <summary>
    /// Converts a display name to a PascalCase prefix.
    /// Singularizes the last word, then PascalCases each word.
    /// Examples: "Doctors" → "Doctor", "Doctor Profile" → "DoctorProfile",
    ///           "Disable Doctor" → "DisableDoctor", "Doctor Types" → "DoctorType"
    /// </summary>
    public static string DerivePrefix(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));

        var words = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words[^1] = Singularize(words[^1]);
        return string.Concat(words.Select(PascalCase));
    }

    private static string PascalCase(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private static string Singularize(string word)
    {
        // -ies → -y  (Babies → Baby, Categories → Category)
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
            return word[..^3] + "y";

        // -s (not -ss, min length 4) → strip -s  (Doctors → Doctor, Cases → Case)
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            word.Length > 3)
            return word[..^1];

        return word;
    }
}
```

- [ ] **Step 5: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Services/NameDerivationService.cs \
        tests/VanillaSlice.Tests/
git commit -m "feat: add NameDerivationService and test project"
```

---

## Task 2: Manifest v2 Schema — SliceManifest.cs

**Files:**
- Modify: `src/VanillaStudio/Templates/SliceFactory/Cli/SliceManifest.cs`
- Create: `tests/VanillaSlice.Tests/ManifestMigrationTests.cs`

**Interfaces:**
- Consumes: nothing new
- Produces:
  - `SliceDescriptor(string Name, string Prefix)` record
  - `SelectListDescriptor(string Name, string Prefix, string ModelType, string DataType)` record extending `SliceDescriptor`
  - `SliceDefinition.Listing?`, `.Form?`, `.Action?`, `.SelectList?` nullable descriptor props
  - `SliceManifest.Version` changed from `string "1.0"` to `int 2`
  - `SliceDefinition.GenerateId(string ns, string directory): string` (signature change)

- [ ] **Step 1: Write migration test**

Create `tests/VanillaSlice.Tests/ManifestMigrationTests.cs`:

```csharp
namespace VanillaSlice.Tests;

public class ManifestMigrationTests
{
    [Fact]
    public void V1_WithListing_MigratesListingDescriptor()
    {
        var v1Json = """
            {
              "version": "1.0",
              "slices": [{
                "id": "doctors-doctor",
                "componentPrefix": "Doctor",
                "featurePluralName": "Doctors",
                "namespace": "Doctors",
                "directoryName": "Features/Doctors",
                "primaryKeyType": "Guid",
                "generateForm": true,
                "generateListing": true,
                "generateSelectList": false,
                "selectListModelType": "SelectOption",
                "selectListDataType": "string",
                "createdAt": "2026-01-01T00:00:00Z",
                "lastGeneratedAt": "2026-01-01T00:00:00Z",
                "generatedFiles": []
              }]
            }
            """;

        var migrated = MigrateV1Json(v1Json);

        Assert.Equal(2, migrated.Version);
        var slice = migrated.Slices.Single();
        Assert.Equal("Doctors", slice.Listing!.Name);
        Assert.Equal("Doctor", slice.Listing.Prefix);
        Assert.Equal("Doctor", slice.Form!.Prefix);
        Assert.Null(slice.Action);
        Assert.Null(slice.SelectList);
    }

    [Fact]
    public void V1_WithSelectList_MigratesSelectListDescriptor()
    {
        var v1Json = """
            {
              "version": "1.0",
              "slices": [{
                "id": "doctors-doctor",
                "componentPrefix": "Doctor",
                "featurePluralName": "Doctors",
                "namespace": "Doctors",
                "directoryName": "Features/Doctors",
                "primaryKeyType": "Guid",
                "generateForm": false,
                "generateListing": false,
                "generateSelectList": true,
                "selectListModelType": "Custom",
                "selectListDataType": "int",
                "createdAt": "2026-01-01T00:00:00Z",
                "lastGeneratedAt": "2026-01-01T00:00:00Z",
                "generatedFiles": []
              }]
            }
            """;

        var migrated = MigrateV1Json(v1Json);
        var slice = migrated.Slices.Single();
        Assert.Null(slice.Listing);
        Assert.Null(slice.Form);
        Assert.NotNull(slice.SelectList);
        Assert.Equal("Custom", slice.SelectList!.ModelType);
        Assert.Equal("int", slice.SelectList.DataType);
    }

    // Inline migration logic to test independently of ManifestService I/O.
    private static SliceManifestV2 MigrateV1Json(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var v2 = new SliceManifestV2 { Version = 2 };

        foreach (var sliceEl in root.GetProperty("slices").EnumerateArray())
        {
            var prefix = sliceEl.GetProperty("componentPrefix").GetString() ?? "";
            var plural = sliceEl.GetProperty("featurePluralName").GetString() ?? prefix + "s";
            var hasList = sliceEl.GetProperty("generateListing").GetBoolean();
            var hasForm = sliceEl.GetProperty("generateForm").GetBoolean();
            var hasSel = sliceEl.GetProperty("generateSelectList").GetBoolean();
            var modelType = sliceEl.GetProperty("selectListModelType").GetString() ?? "SelectOption";
            var dataType = sliceEl.GetProperty("selectListDataType").GetString() ?? "string";

            var slice = new SliceDefinitionV2
            {
                Id = sliceEl.GetProperty("id").GetString() ?? "",
                Namespace = sliceEl.GetProperty("namespace").GetString() ?? "",
                Directory = sliceEl.GetProperty("directoryName").GetString() ?? "",
                PrimaryKeyType = sliceEl.GetProperty("primaryKeyType").GetString() ?? "Guid",
                Listing = hasList ? new SliceDescriptorV2(plural, prefix) : null,
                Form = hasForm ? new SliceDescriptorV2(prefix, prefix) : null,
                SelectList = hasSel ? new SelectListDescriptorV2(prefix + " Types", prefix, modelType, dataType) : null,
            };
            v2.Slices.Add(slice);
        }
        return v2;
    }

    // Minimal in-test versions of the v2 types for isolation
    private class SliceManifestV2 { public int Version { get; set; } public List<SliceDefinitionV2> Slices { get; } = new(); }
    private class SliceDefinitionV2 { public string Id { get; set; } = ""; public string Namespace { get; set; } = ""; public string Directory { get; set; } = ""; public string PrimaryKeyType { get; set; } = "Guid"; public SliceDescriptorV2? Listing { get; set; } public SliceDescriptorV2? Form { get; set; } public SliceDescriptorV2? Action { get; set; } public SelectListDescriptorV2? SelectList { get; set; } }
    private record SliceDescriptorV2(string Name, string Prefix);
    private record SelectListDescriptorV2(string Name, string Prefix, string ModelType, string DataType) : SliceDescriptorV2(Name, Prefix);
}
```

- [ ] **Step 2: Run test to confirm it fails**

```
dotnet test tests/VanillaSlice.Tests/ --filter "ManifestMigrationTests" -v minimal
```

Expected: PASS (inline types compile independently). Confirms migration logic is correct before wiring into the real `ManifestService`.

- [ ] **Step 3: Rewrite SliceManifest.cs**

Replace the entire content of `src/VanillaStudio/Templates/SliceFactory/Cli/SliceManifest.cs`:

```csharp
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
```

- [ ] **Step 4: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Cli/SliceManifest.cs \
        tests/VanillaSlice.Tests/ManifestMigrationTests.cs
git commit -m "feat: manifest v2 schema - SliceDescriptor model and GenerateId by directory"
```

---

## Task 3: ManifestService — v1→v2 Migration

**Files:**
- Modify: `src/VanillaStudio/Templates/SliceFactory/Cli/ManifestService.cs`

**Interfaces:**
- Consumes: `SliceDescriptor`, `SelectListDescriptor`, `SliceManifest.Version` (from Task 2); `NameDerivationService.DerivePrefix` (from Task 1)
- Produces: updated `LoadAsync()` auto-migrates v1; updated `FromCliOptions()` (full signature in Task 8)

- [ ] **Step 1: Add v1→v2 migration to ManifestService.cs**

Replace the `LoadAsync()` method and add the migration helpers. The rest of the file stays the same for now (full `FromCliOptions` update happens in Task 8).

In `LoadAsync()`, change the `Deserialize` step to call migration:

```csharp
public async Task<SliceManifest> LoadAsync()
{
    if (!File.Exists(_manifestPath))
        return new SliceManifest();

    try
    {
        var json = await File.ReadAllTextAsync(_manifestPath);
        
        // Detect v1 by parsing the "version" field value
        using var probe = System.Text.Json.JsonDocument.Parse(json);
        var versionToken = probe.RootElement.TryGetProperty("version", out var vEl) ? vEl : default;
        bool isV1 = versionToken.ValueKind == System.Text.Json.JsonValueKind.String
                    || (versionToken.ValueKind == System.Text.Json.JsonValueKind.Number && versionToken.GetInt32() < 2);

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

        return System.Text.Json.JsonSerializer.Deserialize<SliceManifest>(json, JsonOptions)
               ?? new SliceManifest();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not parse manifest file: {ex.Message}");
        return new SliceManifest();
    }
}
```

Add the private `MigrateV1` method to `ManifestService`:

```csharp
private static SliceManifest MigrateV1(System.Text.Json.JsonElement root)
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
```

- [ ] **Step 2: Update AddOrUpdateSliceAsync to use new GenerateId**

In `AddOrUpdateSliceAsync`, the line:
```csharp
slice.Id = SliceDefinition.GenerateId(slice.Namespace, slice.ComponentPrefix);
```
becomes:
```csharp
slice.Id = SliceDefinition.GenerateId(slice.Namespace, slice.Directory);
```

- [ ] **Step 3: Verify migration manually**

Create a temp `slices-manifest.json` with v1 content:
```json
{
  "version": "1.0",
  "slices": [{
    "id": "doctors-doctor",
    "componentPrefix": "Doctor",
    "featurePluralName": "Doctors",
    "namespace": "Doctors",
    "directoryName": "Features/Doctors",
    "primaryKeyType": "Guid",
    "generateForm": true,
    "generateListing": true,
    "generateSelectList": false,
    "selectListModelType": "SelectOption",
    "selectListDataType": "string",
    "createdAt": "2026-01-01T00:00:00Z",
    "lastGeneratedAt": "2026-01-01T00:00:00Z",
    "generatedFiles": []
  }]
}
```

Run `dotnet run -- list` in the SliceFactory project. Expected output:
```
ID                                    Namespace     Types         Generated
...
doctors-features-doctors              Doctors       Listing,Form  2026-01-01 00:00
```

Also verify `slices-manifest.v1.json` backup was created.

- [ ] **Step 4: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Cli/ManifestService.cs
git commit -m "feat: manifest v1->v2 auto-migration with v1 backup"
```

---

## Task 4: CliOptions v2

**Files:**
- Modify: `src/VanillaStudio/Templates/SliceFactory/Cli/CliOptions.cs`

**Interfaces:**
- Consumes: nothing new
- Produces:
  - `CliOptions.ListingName?` — string, non-null means listing enabled
  - `CliOptions.FormName?` — string, non-null means form enabled
  - `CliOptions.ActionName?` — string, non-null means action enabled
  - `CliOptions.SelectListName?` — string, non-null means select list enabled
  - `CliOptions.SelectListModelType`, `CliOptions.SelectListDataType` — unchanged
  - Removed: `ComponentPrefix`, `FeaturePluralName`, `GenerateForm`, `GenerateListing`, `GenerateSelectList`

- [ ] **Step 1: Rewrite CliOptions.cs**

Replace the entire file content:

```csharp
namespace {{RootNamespace}}.SliceFactory.Cli;

public class CliOptions
{
    public CliCommand Command { get; set; } = CliCommand.None;

    // Named slice flags — non-null means "generate this slice with this display name"
    public string? ListingName { get; set; }
    public string? FormName { get; set; }
    public string? ActionName { get; set; }
    public string? SelectListName { get; set; }

    // SelectList configuration (only meaningful when SelectListName is set)
    public string SelectListModelType { get; set; } = "SelectOption";
    public string SelectListDataType { get; set; } = "string";

    // Shared options
    public string? Namespace { get; set; }
    public string? DirectoryName { get; set; }
    public string PrimaryKeyType { get; set; } = "Guid";
    public bool Preview { get; set; } = false;

    // Regenerate command options
    public string? SliceId { get; set; }

    public bool ShowHelp { get; set; } = false;

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();

        if (args.Length == 0)
            return options;

        var i = 0;
        while (i < args.Length)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "generate": case "gen": case "g":
                    options.Command = CliCommand.Generate; break;
                case "regenerate": case "regen": case "r":
                    options.Command = CliCommand.Regenerate; break;
                case "regenerate-all": case "regen-all": case "ra":
                    options.Command = CliCommand.RegenerateAll; break;
                case "list": case "ls": case "l":
                    options.Command = CliCommand.List; break;
                case "remove": case "rm": case "delete":
                    options.Command = CliCommand.Remove; break;

                case "--listing": case "-l":
                    options.ListingName = GetNextArg(args, ref i); break;
                case "--form": case "-f":
                    options.FormName = GetNextArg(args, ref i); break;
                case "--action": case "-a":
                    options.ActionName = GetNextArg(args, ref i); break;
                case "--select-list": case "-s":
                    options.SelectListName = GetNextArg(args, ref i); break;

                case "--select-model":
                    options.SelectListModelType = GetNextArg(args, ref i) ?? "SelectOption"; break;
                case "--select-type":
                    options.SelectListDataType = GetNextArg(args, ref i) ?? "string"; break;

                case "--namespace": case "-n":
                    options.Namespace = GetNextArg(args, ref i); break;
                case "--directory": case "-d":
                    options.DirectoryName = GetNextArg(args, ref i); break;
                case "--pk": case "--primary-key":
                    options.PrimaryKeyType = GetNextArg(args, ref i) ?? "Guid"; break;
                case "--preview":
                    options.Preview = true; break;
                case "--id":
                    options.SliceId = GetNextArg(args, ref i); break;
                case "--help": case "-h": case "help": case "?":
                    options.ShowHelp = true; break;

                default:
                    if (options.Command == CliCommand.Regenerate &&
                        !arg.StartsWith("-") && string.IsNullOrEmpty(options.SliceId))
                        options.SliceId = args[i];
                    break;
            }

            i++;
        }

        return options;
    }

    private static string? GetNextArg(string[] args, ref int i)
    {
        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
        {
            i++;
            return args[i];
        }
        return null;
    }

    public (bool IsValid, string? ErrorMessage) ValidateForGenerate()
    {
        if (string.IsNullOrEmpty(Namespace))
            return (false, "Namespace is required. Use --namespace <name>");

        if (string.IsNullOrEmpty(DirectoryName))
            return (false, "Directory is required. Use --directory <path>");

        if (ListingName == null && FormName == null && ActionName == null && SelectListName == null)
            return (false, "At least one slice type must be specified: --listing, --form, --action, or --select-list");

        var validPkTypes = new[] { "string", "Guid", "int", "long" };
        if (!validPkTypes.Contains(PrimaryKeyType, StringComparer.OrdinalIgnoreCase))
            return (false, $"Invalid primary key type. Must be one of: {string.Join(", ", validPkTypes)}");

        return (true, null);
    }

    public static string GetHelpText() => """
        SliceFactory CLI - Generate vertical slice boilerplate code

        USAGE:
            dotnet run -- <command> [options]

        COMMANDS:
            generate, gen, g        Generate a new slice
            regenerate, regen, r    Regenerate a specific slice from manifest
            regenerate-all, ra      Regenerate all slices from manifest
            list, ls, l             List all slices in manifest
            remove, rm, delete      Remove a slice from manifest

        GENERATE OPTIONS:
            --listing, -l <name>    Generate listing slice (e.g. "Doctors")
            --form, -f <name>       Generate form slice (e.g. "Doctor Profile")
            --action, -a <name>     Generate action slice (e.g. "Disable Doctor")
            --select-list, -s <name> Generate select list slice (e.g. "Doctor Types")
            --namespace, -n <name>  Module namespace [required]
            --directory, -d <path>  Output directory relative to solution root [required]
            --pk <type>             Primary key type: Guid, string, int, long (default: Guid)
            --select-model <type>   SelectList model type: SelectOption, Custom
            --select-type <type>    SelectList data type (default: string)
            --preview               Preview files without generating

        REGENERATE OPTIONS:
            --id <slice-id>         Slice ID to regenerate (or pass as argument)

        EXAMPLES:
            # Generate listing + form for Doctors
            dotnet run -- generate --listing "Doctors" --form "Doctor Profile" \
                --namespace ZeroLegal.Doctors --directory Features/Doctors

            # Generate a discrete action slice
            dotnet run -- generate --action "Disable Doctor" \
                --namespace ZeroLegal.Doctors --directory Features/Doctors

            # Preview without generating
            dotnet run -- generate --listing "Doctors" \
                --namespace ZeroLegal.Doctors --directory Features/Doctors --preview

            # Regenerate a specific slice
            dotnet run -- regenerate zerolegal-doctors-features-doctors

            # List all slices
            dotnet run -- list
        """;
}

public enum CliCommand
{
    None,
    Generate,
    Regenerate,
    RegenerateAll,
    List,
    Remove
}
```

- [ ] **Step 2: Verify parse manually**

Run in the SliceFactory project:
```
dotnet run -- generate --listing "Doctors" --form "Doctor Profile" --namespace ZeroLegal.Doctors --directory Features/Doctors --preview
```

Expected: no crash on parse (generation will fail until Task 8 wires it up, but Parse should succeed).

- [ ] **Step 3: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Cli/CliOptions.cs
git commit -m "feat: CliOptions v2 - named slice flags replace --prefix/--plural booleans"
```

---

## Task 5: Feature.cs v2 + TemplateEngineService update

**Files:**
- Modify: `src/VanillaStudio/Templates/SliceFactory/Models/Feature.cs`
- Modify: `src/VanillaStudio/Templates/SliceFactory/Services/TemplateEngineService.cs`

**Interfaces:**
- Consumes: `SliceDescriptor`, `SelectListDescriptor` (from Task 2)
- Produces:
  - `Feature.Listing?`, `Feature.Form?`, `Feature.Action?`, `Feature.SelectList?` replacing old flat fields
  - `TemplateEngineService.CreateParameterDictionary(..., string? componentPrefixPlural = null)`

- [ ] **Step 1: Update Feature.cs**

In `Feature.cs`, replace the old slice-related fields. The `FeatureFile`, `FeatureProject`, and `FeatureTreeNode` classes stay unchanged. Only `Feature` class changes:

```csharp
// Replace these old fields:
//   public string ComponentPrefix { get; set; }
//   public string FeaturePluralName { get; set; }
//   public bool HasForm { get; set; }
//   public bool HasListing { get; set; }
//   public bool HasSelectList { get; set; }
//   public string SelectListModelType { get; set; }
//   public string SelectListDataType { get; set; }
// With:

public SliceDescriptor? Listing { get; set; }
public SliceDescriptor? Form { get; set; }
public SliceDescriptor? Action { get; set; }
public SelectListDescriptor? SelectList { get; set; }
```

The full updated `Feature` class (keep all other properties identical):

```csharp
public class Feature
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ModuleNamespace { get; set; } = string.Empty;
    public string ProjectNamespace { get; set; } = string.Empty;
    public string PrimaryKeyType { get; set; } = string.Empty;

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

    public string? ProfileConfiguration { get; set; }

    public List<FeatureFile> Files { get; set; } = new();
    public List<FeatureProject> Projects { get; set; } = new();
}
```

Note: `SliceDescriptor` and `SelectListDescriptor` are defined in `Cli/SliceManifest.cs`. Add the `using` at the top of `Feature.cs`:
```csharp
using {{RootNamespace}}.SliceFactory.Cli;
```

- [ ] **Step 2: Update TemplateEngineService.CreateParameterDictionary**

Add the optional `componentPrefixPlural` parameter. When provided, it overrides the `PluralizationService` result:

```csharp
public Dictionary<string, object> CreateParameterDictionary(
    string componentPrefix,
    string moduleNamespace,
    string projectNamespace,
    string primaryKeyType,
    string? uiFramework = null,
    string? selectListModelType = null,
    string? selectListDataType = null,
    string? componentPrefixPlural = null)   // NEW: pass listing name directly
{
    var pluralizedPrefix = componentPrefixPlural
        ?? _pluralizationService.Pluralize(componentPrefix);

    var parameters = new Dictionary<string, object>
    {
        ["ComponentPrefix"] = componentPrefix,
        ["componentPrefix"] = componentPrefix.ToLowerInvariant(),
        ["ComponentPrefixPlural"] = pluralizedPrefix,
        ["componentPrefixPlural"] = pluralizedPrefix.ToLowerInvariant(),
        ["moduleNamespace"] = moduleNamespace,
        ["projectNamespace"] = projectNamespace,
        ["primaryKeyType"] = primaryKeyType
    };

    if (!string.IsNullOrEmpty(uiFramework))
        parameters["UIFramework"] = uiFramework;

    if (!string.IsNullOrEmpty(selectListModelType))
    {
        parameters["selectListModelType"] = selectListModelType;
        parameters["SelectListModelType"] = selectListModelType;
    }

    if (!string.IsNullOrEmpty(selectListDataType))
    {
        parameters["selectListDataType"] = selectListDataType;
        parameters["SelectListDataType"] = selectListDataType;
    }

    return parameters;
}
```

- [ ] **Step 3: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Models/Feature.cs \
        src/VanillaStudio/Templates/SliceFactory/Services/TemplateEngineService.cs
git commit -m "feat: Feature v2 - per-slice SliceDescriptor fields; TemplateEngineService plural override"
```

---

## Task 6: FeatureManagementService v2

**Files:**
- Modify: `src/VanillaStudio/Templates/SliceFactory/Services/FeatureManagementService.cs`

**Interfaces:**
- Consumes: `Feature.Listing?`, `.Form?`, `.Action?`, `.SelectList?` (Task 5); `CreateParameterDictionary(..., componentPrefixPlural)` (Task 5)
- Produces: updated `CreateFeatureAsync(...)` — new signature; `GenerateFeatureFilesAsync` handles Action slice

- [ ] **Step 1: Update CreateFeatureAsync signature and body**

Replace the `CreateFeatureAsync` signature and body. The new signature passes descriptors directly (callers in Task 8 build them from `NameDerivationService`):

```csharp
public async Task<Feature> CreateFeatureAsync(
    SliceDescriptor? listing,
    SliceDescriptor? form,
    SliceDescriptor? action,
    SelectListDescriptor? selectList,
    string moduleNamespace,
    string projectNamespace,
    string primaryKeyType,
    string basePath,
    string directoryName,
    List<Project> projects,
    string? profileConfiguration = null,
    string uiFramework = "Bootstrap")
{
    // Uniqueness check by namespace + directory
    var existingFeature = _store.Features
        .FirstOrDefault(f => f.ModuleNamespace == moduleNamespace
                          && f.DirectoryName == directoryName);

    if (existingFeature != null)
        throw new InvalidOperationException(
            $"Feature at '{directoryName}' already exists in module '{moduleNamespace}'");

    var feature = new Feature
    {
        Listing          = listing,
        Form             = form,
        Action           = action,
        SelectList       = selectList,
        ModuleNamespace  = moduleNamespace,
        ProjectNamespace = projectNamespace,
        PrimaryKeyType   = primaryKeyType,
        BasePath         = basePath,
        DirectoryName    = directoryName,
        UIFramework      = uiFramework,
        ProfileConfiguration = profileConfiguration,
        CreatedAt        = DateTime.UtcNow
    };

    _store.Features.Add(feature);
    await GenerateFeatureFilesAsync(feature, projects);
    await _store.SaveAsync();
    return feature;
}
```

- [ ] **Step 2: Update GenerateFeatureFilesAsync**

Replace the private `GenerateFeatureFilesAsync`. Each slice type now gets its own parameter dictionary:

```csharp
private async Task GenerateFeatureFilesAsync(Feature feature, List<Project> projects)
{
    foreach (var project in projects)
    {
        var templateDir = GetTemplateDirectoryName(project.ProjectType);
        if (string.IsNullOrEmpty(templateDir)) continue;

        var baseFeaturePath = Path.Combine(feature.BasePath, project.Path, feature.DirectoryName);

        feature.Projects.Add(new FeatureProject
        {
            FeatureId     = feature.Id,
            ProjectType   = project.ProjectType,
            ProjectPath   = Path.GetRelativePath(feature.BasePath, baseFeaturePath),
            ProjectNamespace = project.NameSpace ?? "",
            CreatedAt     = DateTime.UtcNow
        });

        if (feature.Listing is { } listing)
        {
            var p = _templateEngine.CreateParameterDictionary(
                listing.Prefix, feature.ModuleNamespace, feature.ProjectNamespace,
                feature.PrimaryKeyType, feature.UIFramework,
                componentPrefixPlural: listing.Name);  // listing name IS the plural
            var path = Path.Combine(baseFeaturePath, $"{listing.Name}Listing");
            await GenerateSliceFilesAsync(feature, templateDir, "Listing", path, p);
        }

        if (feature.Form is { } form)
        {
            var p = _templateEngine.CreateParameterDictionary(
                form.Prefix, feature.ModuleNamespace, feature.ProjectNamespace,
                feature.PrimaryKeyType, feature.UIFramework);
            var path = Path.Combine(baseFeaturePath, $"{form.Prefix}Form");
            await GenerateSliceFilesAsync(feature, templateDir, "Form", path, p);
        }

        if (feature.Action is { } action)
        {
            var p = _templateEngine.CreateParameterDictionary(
                action.Prefix, feature.ModuleNamespace, feature.ProjectNamespace,
                feature.PrimaryKeyType, feature.UIFramework);
            var path = Path.Combine(baseFeaturePath, $"{action.Prefix}Action");
            await GenerateSliceFilesAsync(feature, templateDir, "Action", path, p);
        }

        if (feature.SelectList is { } selectList)
        {
            var p = _templateEngine.CreateParameterDictionary(
                selectList.Prefix, feature.ModuleNamespace, feature.ProjectNamespace,
                feature.PrimaryKeyType, feature.UIFramework,
                selectList.ModelType, selectList.DataType);
            var path = Path.Combine(baseFeaturePath, $"{selectList.Name}SelectList");
            await GenerateSliceFilesAsync(feature, templateDir, "SelectList", path, p);
        }
    }

    await _registrationService.UpdateRegistrationsForFeatureAsync(feature, projects);
    await _navigationService.UpdateNavigationForFeatureAsync(feature, projects);
}
```

- [ ] **Step 3: Update PreviewFeatureFilesAsync**

Replace the `PreviewFeatureFilesAsync` signature to match `CreateFeatureAsync`:

```csharp
public async Task<List<FeatureFilePreview>> PreviewFeatureFilesAsync(
    SliceDescriptor? listing,
    SliceDescriptor? form,
    SliceDescriptor? action,
    SelectListDescriptor? selectList,
    string moduleNamespace,
    string projectNamespace,
    string primaryKeyType,
    string basePath,
    string directoryName,
    List<Project> projects)
{
    var previews = new List<FeatureFilePreview>();

    foreach (var project in projects)
    {
        var templateDir = GetTemplateDirectoryName(project.ProjectType);
        if (string.IsNullOrEmpty(templateDir)) continue;

        var projectRootPath = Path.Combine(basePath, project.Path ?? "");
        var projectNs = project.NameSpace ?? "";
        var baseFeaturePath = Path.Combine(projectRootPath, directoryName);

        async Task AddPreviews(string sliceType, string slicePath, Dictionary<string, object> p)
        {
            var files = await GetTemplateFilesPreviewAsync(templateDir, sliceType, slicePath, p);
            foreach (var f in files)
            {
                var fp = Path.Combine(slicePath, f.Key);
                previews.Add(new FeatureFilePreview
                {
                    ProjectType = templateDir, SliceType = sliceType,
                    FileName = f.Key, FilePath = fp,
                    DirectoryPath = Path.GetDirectoryName(fp) ?? "",
                    ProjectRootPath = projectRootPath, ProjectNamespace = projectNs,
                    ModuleNamespace = moduleNamespace,
                    ComponentPrefix = p["ComponentPrefix"].ToString() ?? "",
                    IsNew = true, Content = f.Value
                });
            }
        }

        if (listing is not null)
            await AddPreviews("Listing",
                Path.Combine(baseFeaturePath, $"{listing.Name}Listing"),
                _templateEngine.CreateParameterDictionary(listing.Prefix, moduleNamespace,
                    projectNamespace, primaryKeyType, componentPrefixPlural: listing.Name));

        if (form is not null)
            await AddPreviews("Form",
                Path.Combine(baseFeaturePath, $"{form.Prefix}Form"),
                _templateEngine.CreateParameterDictionary(form.Prefix, moduleNamespace,
                    projectNamespace, primaryKeyType));

        if (action is not null)
            await AddPreviews("Action",
                Path.Combine(baseFeaturePath, $"{action.Prefix}Action"),
                _templateEngine.CreateParameterDictionary(action.Prefix, moduleNamespace,
                    projectNamespace, primaryKeyType));

        if (selectList is not null)
            await AddPreviews("SelectList",
                Path.Combine(baseFeaturePath, $"{selectList.Name}SelectList"),
                _templateEngine.CreateParameterDictionary(selectList.Prefix, moduleNamespace,
                    projectNamespace, primaryKeyType, selectListModelType: selectList.ModelType,
                    selectListDataType: selectList.DataType));
    }

    return previews;
}
```

- [ ] **Step 4: Update GetAllExistingPreviewsAsync**

The `ComponentPrefix` field is gone from `Feature`. Replace the `ComponentPrefix` reference with the first available descriptor prefix:

```csharp
// Old: ComponentPrefix = feature.ComponentPrefix
// New:
ComponentPrefix = feature.Listing?.Prefix
               ?? feature.Form?.Prefix
               ?? feature.Action?.Prefix
               ?? feature.SelectList?.Prefix
               ?? ""
```

Also update `GetAllFeaturesAsync` ordering:
```csharp
// Old: .ThenBy(f => f.ComponentPrefix)
// New: .ThenBy(f => f.DirectoryName)
```

- [ ] **Step 5: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Services/FeatureManagementService.cs
git commit -m "feat: FeatureManagementService v2 - per-slice params, Action slice support"
```

---

## Task 7: Action Slice Templates

**Files:**
- Create: `src/VanillaStudio/Templates/SliceFactory/Templates/ServiceContracts/Action/I__ComponentPrefix__ActionDataService.cs`
- Create: `src/VanillaStudio/Templates/SliceFactory/Templates/Controllers/Action/__ComponentPrefix__ActionController.cs`
- Create: `src/VanillaStudio/Templates/SliceFactory/Templates/ServerSideServices/Action/__ComponentPrefix__ActionServerDataService.cs`
- Create: `src/VanillaStudio/Templates/SliceFactory/Templates/ClientShared/Action/__ComponentPrefix__ActionClientDataService.cs`

**Interfaces:**
- Template placeholders used: `__ComponentPrefix__`, `__moduleNamespace__`, `__primaryKeyType__`, `{{RootNamespace}}`

- [ ] **Step 1: Create ServiceContracts Action template**

`src/VanillaStudio/Templates/SliceFactory/Templates/ServiceContracts/Action/I__ComponentPrefix__ActionDataService.cs`:

```csharp
namespace {{RootNamespace}}.ServiceContracts.Features.__moduleNamespace__;

public interface I__ComponentPrefix__ActionDataService
{
    Task ExecuteAsync(__primaryKeyType__ id);
}
```

- [ ] **Step 2: Create Controllers Action template**

`src/VanillaStudio/Templates/SliceFactory/Templates/Controllers/Action/__ComponentPrefix__ActionController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using {{RootNamespace}}.ServiceContracts.Features.__moduleNamespace__;

namespace {{RootNamespace}}.Controllers;

[ApiController]
[Route("api/[controller]")]
public class __ComponentPrefix__ActionController : ControllerBase
{
    private readonly I__ComponentPrefix__ActionDataService _service;

    public __ComponentPrefix__ActionController(I__ComponentPrefix__ActionDataService service)
        => _service = service;

    [HttpPost("{id}/execute")]
    public async Task<IActionResult> ExecuteAsync(__primaryKeyType__ id)
    {
        await _service.ExecuteAsync(id);
        return NoContent();
    }
}
```

- [ ] **Step 3: Create ServerSideServices Action template**

`src/VanillaStudio/Templates/SliceFactory/Templates/ServerSideServices/Action/__ComponentPrefix__ActionServerDataService.cs`:

```csharp
using {{RootNamespace}}.ServiceContracts.Features.__moduleNamespace__;

namespace {{RootNamespace}}.ServerSideServices.Features.__moduleNamespace__;

internal class __ComponentPrefix__ActionServerDataService : I__ComponentPrefix__ActionDataService
{
    public Task ExecuteAsync(__primaryKeyType__ id)
    {
        // Implement the discrete mutation (e.g., disable, approve, archive).
        throw new NotImplementedException();
    }
}
```

- [ ] **Step 4: Create ClientShared Action template**

`src/VanillaStudio/Templates/SliceFactory/Templates/ClientShared/Action/__ComponentPrefix__ActionClientDataService.cs`:

```csharp
using {{RootNamespace}}.Framework;
using {{RootNamespace}}.ServiceContracts.Features.__moduleNamespace__;

namespace {{RootNamespace}}.ClientShared.Features.__moduleNamespace__;

internal class __ComponentPrefix__ActionClientDataService : I__ComponentPrefix__ActionDataService
{
    private readonly BaseHttpClient _httpClient;

    public __ComponentPrefix__ActionClientDataService(BaseHttpClient httpClient)
        => _httpClient = httpClient;

    public async Task ExecuteAsync(__primaryKeyType__ id)
    {
        await _httpClient.PostAsJsonAsync<object>(
            $"api/__ComponentPrefix__Action/{id}/execute", new { });
    }
}
```

- [ ] **Step 5: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Templates/ServiceContracts/Action/ \
        src/VanillaStudio/Templates/SliceFactory/Templates/Controllers/Action/ \
        src/VanillaStudio/Templates/SliceFactory/Templates/ServerSideServices/Action/ \
        src/VanillaStudio/Templates/SliceFactory/Templates/ClientShared/Action/
git commit -m "feat: Action slice templates - interface, controller, server service, client service"
```

---

## Task 8: CliRunner v2 + ManifestService.FromCliOptions

**Files:**
- Modify: `src/VanillaStudio/Templates/SliceFactory/Cli/CliRunner.cs`
- Modify: `src/VanillaStudio/Templates/SliceFactory/Cli/ManifestService.cs` (update `FromCliOptions`)

**Interfaces:**
- Consumes: `CliOptions.ListingName?`, `.FormName?`, `.ActionName?`, `.SelectListName?` (Task 4); `NameDerivationService.DerivePrefix` (Task 1); `FeatureManagementService.CreateFeatureAsync(listing, form, action, selectList, ...)` (Task 6)

- [ ] **Step 1: Update ManifestService.FromCliOptions**

Replace the static `FromCliOptions` method in `ManifestService.cs`:

```csharp
public static SliceDefinition FromCliOptions(CliOptions options)
{
    SliceDescriptor? listing = options.ListingName is { } ln
        ? new SliceDescriptor(ln, NameDerivationService.DerivePrefix(ln))
        : null;

    SliceDescriptor? form = options.FormName is { } fn
        ? new SliceDescriptor(fn, NameDerivationService.DerivePrefix(fn))
        : null;

    SliceDescriptor? action = options.ActionName is { } an
        ? new SliceDescriptor(an, NameDerivationService.DerivePrefix(an))
        : null;

    SelectListDescriptor? selectList = options.SelectListName is { } sln
        ? new SelectListDescriptor(sln, NameDerivationService.DerivePrefix(sln),
            options.SelectListModelType, options.SelectListDataType)
        : null;

    return new SliceDefinition
    {
        Id             = SliceDefinition.GenerateId(options.Namespace!, options.DirectoryName!),
        Namespace      = options.Namespace!,
        Directory      = options.DirectoryName!,
        PrimaryKeyType = options.PrimaryKeyType,
        Listing        = listing,
        Form           = form,
        Action         = action,
        SelectList     = selectList
    };
}
```

- [ ] **Step 2: Update CliRunner.GenerateAsync**

Replace the `GenerateAsync` method in `CliRunner.cs`:

```csharp
private async Task<int> GenerateAsync(CliOptions options)
{
    var (isValid, errorMessage) = options.ValidateForGenerate();
    if (!isValid)
    {
        Console.WriteLine($"Error: {errorMessage}");
        Console.WriteLine();
        Console.WriteLine("Use --help for usage information.");
        return 1;
    }

    var profile = _codeConfig.Profiles.FirstOrDefault();
    if (profile?.Projects == null)
    {
        Console.WriteLine("Error: No profile configuration found in webportal-profile.json");
        return 1;
    }

    var sliceDefinition = ManifestService.FromCliOptions(options);

    Console.WriteLine();
    Console.WriteLine("SliceFactory - Generating Slice");
    Console.WriteLine("================================");
    Console.WriteLine($"  Namespace:    {sliceDefinition.Namespace}");
    Console.WriteLine($"  Directory:    {sliceDefinition.Directory}");
    Console.WriteLine($"  Primary Key:  {sliceDefinition.PrimaryKeyType}");
    if (sliceDefinition.Listing is { } l)  Console.WriteLine($"  Listing:      {l.Name} (prefix: {l.Prefix})");
    if (sliceDefinition.Form is { } f)     Console.WriteLine($"  Form:         {f.Name} (prefix: {f.Prefix})");
    if (sliceDefinition.Action is { } a)   Console.WriteLine($"  Action:       {a.Name} (prefix: {a.Prefix})");
    if (sliceDefinition.SelectList is { } s) Console.WriteLine($"  SelectList:   {s.Name} (prefix: {s.Prefix})");
    Console.WriteLine();

    var projectNs = $"{profile.Projects.FirstOrDefault()?.NameSpace}.{sliceDefinition.Namespace}";

    if (options.Preview)
    {
        Console.WriteLine("PREVIEW MODE - No files will be generated");
        Console.WriteLine();

        var previewFiles = await _featureService.PreviewFeatureFilesAsync(
            listing: sliceDefinition.Listing,
            form: sliceDefinition.Form,
            action: sliceDefinition.Action,
            selectList: sliceDefinition.SelectList,
            moduleNamespace: sliceDefinition.Namespace,
            projectNamespace: projectNs,
            primaryKeyType: sliceDefinition.PrimaryKeyType,
            basePath: _basePath,
            directoryName: sliceDefinition.Directory,
            projects: profile.Projects.ToList());

        Console.WriteLine("Files that would be generated:");
        foreach (var file in previewFiles)
            Console.WriteLine($"  [{file.ProjectType}/{file.SliceType}] {file.FilePath}");

        return 0;
    }

    try
    {
        Console.WriteLine("Generating files...");

        var feature = await _featureService.CreateFeatureAsync(
            listing: sliceDefinition.Listing,
            form: sliceDefinition.Form,
            action: sliceDefinition.Action,
            selectList: sliceDefinition.SelectList,
            moduleNamespace: sliceDefinition.Namespace,
            projectNamespace: projectNs,
            primaryKeyType: sliceDefinition.PrimaryKeyType,
            basePath: _basePath,
            directoryName: sliceDefinition.Directory,
            projects: profile.Projects.ToList(),
            profileConfiguration: JsonSerializer.Serialize(profile),
            uiFramework: profile.UIFramework ?? "TailwindCSS");

        var generatedFiles = feature.Files.Select(f => f.FilePath).ToList();
        sliceDefinition.GeneratedFiles = generatedFiles;
        await _manifestService.AddOrUpdateSliceAsync(sliceDefinition, generatedFiles);

        Console.WriteLine();
        Console.WriteLine($"Successfully generated {generatedFiles.Count} files!");
        Console.WriteLine();
        Console.WriteLine("Generated files:");
        foreach (var file in generatedFiles)
            Console.WriteLine($"  {Path.GetRelativePath(_basePath, file)}");

        Console.WriteLine();
        Console.WriteLine($"Manifest updated: {_manifestService.ManifestPath}");
        Console.WriteLine($"Slice ID: {sliceDefinition.Id}");
        return 0;
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine("Use 'regenerate' command to update an existing slice.");
        return 1;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error generating slice: {ex.Message}");
        return 1;
    }
}
```

- [ ] **Step 3: Update CliRunner.RegenerateAsync**

Replace the `RegenerateAsync` method to hydrate from v2 `SliceDefinition` fields:

```csharp
private async Task<int> RegenerateAsync(CliOptions options)
{
    if (string.IsNullOrEmpty(options.SliceId))
    {
        Console.WriteLine("Error: Slice ID is required. Use --id <slice-id> or pass as argument.");
        Console.WriteLine();
        Console.WriteLine("Available slices:");
        await ListAsync();
        return 1;
    }

    var slice = await _manifestService.GetSliceAsync(options.SliceId);
    if (slice == null)
    {
        Console.WriteLine($"Error: Slice '{options.SliceId}' not found in manifest.");
        Console.WriteLine();
        Console.WriteLine("Available slices:");
        await ListAsync();
        return 1;
    }

    Console.WriteLine($"Regenerating slice: {slice.Id}");
    Console.WriteLine($"  Namespace: {slice.Namespace}");
    Console.WriteLine($"  Directory: {slice.Directory}");
    Console.WriteLine();

    // Build CliOptions from v2 SliceDefinition (descriptors already have names+prefixes)
    var regenerateOptions = new CliOptions
    {
        Command           = CliCommand.Generate,
        ListingName       = slice.Listing?.Name,
        FormName          = slice.Form?.Name,
        ActionName        = slice.Action?.Name,
        SelectListName    = slice.SelectList?.Name,
        SelectListModelType = slice.SelectList?.ModelType ?? "SelectOption",
        SelectListDataType  = slice.SelectList?.DataType ?? "string",
        Namespace         = slice.Namespace,
        DirectoryName     = slice.Directory,
        PrimaryKeyType    = slice.PrimaryKeyType,
        Preview           = options.Preview
    };

    try
    {
        return await GenerateAsync(regenerateOptions);
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("Note: Feature already exists in database. Files have been updated.");
        return 0;
    }
}
```

- [ ] **Step 4: Update CliRunner.ListAsync**

Update the display to use descriptor presence instead of booleans:

```csharp
private async Task<int> ListAsync()
{
    var slices = await _manifestService.GetAllSlicesAsync();

    if (slices.Count == 0)
    {
        Console.WriteLine("No slices found in manifest.");
        Console.WriteLine($"Manifest location: {_manifestService.ManifestPath}");
        return 0;
    }

    Console.WriteLine();
    Console.WriteLine("SliceFactory - Registered Slices");
    Console.WriteLine("=================================");
    Console.WriteLine();
    Console.WriteLine($"{"ID",-40} {"Namespace",-25} {"Types",-20} {"Generated"}");
    Console.WriteLine(new string('-', 110));

    foreach (var slice in slices.OrderBy(s => s.Namespace).ThenBy(s => s.Directory))
    {
        var types = new List<string>();
        if (slice.Listing is not null) types.Add("Listing");
        if (slice.Form is not null)    types.Add("Form");
        if (slice.Action is not null)  types.Add("Action");
        if (slice.SelectList is not null) types.Add("Select");

        Console.WriteLine($"{slice.Id,-40} {slice.Namespace,-25} {string.Join(",", types),-20} {slice.LastGeneratedAt:yyyy-MM-dd HH:mm}");
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {slices.Count} slices");
    Console.WriteLine($"Manifest: {_manifestService.ManifestPath}");
    return 0;
}
```

- [ ] **Step 5: End-to-end smoke test**

Run the full generate + list + regenerate cycle:

```
# Generate a new slice
dotnet run -- generate \
  --listing "Doctors" \
  --form "Doctor Profile" \
  --action "Disable Doctor" \
  --namespace ZeroLegal.Doctors \
  --directory Features/Doctors \
  --preview
```

Expected output:
```
SliceFactory - Generating Slice
================================
  Namespace:    ZeroLegal.Doctors
  Directory:    Features/Doctors
  Primary Key:  Guid
  Listing:      Doctors (prefix: Doctor)
  Form:         Doctor Profile (prefix: DoctorProfile)
  Action:       Disable Doctor (prefix: DisableDoctor)

PREVIEW MODE - No files will be generated

Files that would be generated:
  [ServiceContracts/Listing] ...DoctorsListing/IDoctorListingDataService.cs
  [ServiceContracts/Form] ...DoctorProfileForm/IDoctorProfileFormDataService.cs
  [ServiceContracts/Action] ...DisableDoctorAction/IDisableDoctorActionDataService.cs
  ...
```

- [ ] **Step 6: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Cli/CliRunner.cs \
        src/VanillaStudio/Templates/SliceFactory/Cli/ManifestService.cs
git commit -m "feat: CliRunner v2 - named slice descriptors, Action support, v2 regenerate"
```

---

## Task 9: DirectoryTreePicker Component

**Files:**
- Create: `src/VanillaStudio/Templates/SliceFactory/Components/Shared/DirectoryTreePicker.razor`
- Create: `src/VanillaStudio/Templates/SliceFactory/Components/Shared/DirectoryTreePicker.razor.cs`

**Interfaces:**
- Parameters in: `IReadOnlyList<string> KnownPaths`, `string? Value`, `EventCallback<string> OnPathSelected`
- Emits: `OnPathSelected` with the selected full path string

- [ ] **Step 1: Create the codebehind**

Create `src/VanillaStudio/Templates/SliceFactory/Components/Shared/DirectoryTreePicker.razor.cs`:

```csharp
using Microsoft.AspNetCore.Components;

namespace {{RootNamespace}}.SliceFactory.Components.Shared;

public partial class DirectoryTreePicker
{
    [Parameter] public IReadOnlyList<string> KnownPaths { get; set; } = Array.Empty<string>();
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> OnPathSelected { get; set; }

    private List<DirectoryNode> _roots = new();
    private string? _selectedPath;
    private string _newFolderName = "";
    private bool _showNewFolderInput = false;

    protected override void OnParametersSet()
    {
        _roots = BuildTree(KnownPaths);
        _selectedPath = Value;
    }

    private List<DirectoryNode> BuildTree(IReadOnlyList<string> paths)
    {
        var roots = new List<DirectoryNode>();

        foreach (var path in paths.Distinct())
        {
            var segments = path.Split('/', '\\')
                               .Where(s => !string.IsNullOrEmpty(s))
                               .ToArray();
            var current = roots;
            var currentPath = "";

            foreach (var segment in segments)
            {
                currentPath = currentPath.Length == 0 ? segment : $"{currentPath}/{segment}";
                var node = current.FirstOrDefault(n => n.Name == segment);
                if (node == null)
                {
                    node = new DirectoryNode(segment, currentPath);
                    current.Add(node);
                }
                current = node.Children;
            }
        }

        return roots;
    }

    private async Task SelectNode(DirectoryNode node)
    {
        _selectedPath = node.FullPath;
        _showNewFolderInput = true;
        _newFolderName = "";
        await OnPathSelected.InvokeAsync(node.FullPath);
    }

    private void ToggleExpand(DirectoryNode node) => node.IsExpanded = !node.IsExpanded;

    private async Task ConfirmNewFolder()
    {
        if (string.IsNullOrWhiteSpace(_newFolderName)) return;

        var newPath = string.IsNullOrEmpty(_selectedPath)
            ? _newFolderName.Trim()
            : $"{_selectedPath}/{_newFolderName.Trim()}";

        // Add to tree
        var parent = FindNode(_roots, _selectedPath);
        var newNode = new DirectoryNode(_newFolderName.Trim(), newPath);
        if (parent != null)
        {
            parent.IsExpanded = true;
            parent.Children.Add(newNode);
        }
        else
        {
            _roots.Add(newNode);
        }

        _selectedPath = newPath;
        _showNewFolderInput = false;
        _newFolderName = "";
        await OnPathSelected.InvokeAsync(newPath);
    }

    private void CancelNewFolder()
    {
        _showNewFolderInput = false;
        _newFolderName = "";
    }

    private static DirectoryNode? FindNode(List<DirectoryNode> nodes, string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;
        foreach (var node in nodes)
        {
            if (node.FullPath == fullPath) return node;
            var found = FindNode(node.Children, fullPath);
            if (found != null) return found;
        }
        return null;
    }

    private class DirectoryNode
    {
        public string Name { get; }
        public string FullPath { get; }
        public List<DirectoryNode> Children { get; } = new();
        public bool IsExpanded { get; set; }

        public DirectoryNode(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }
    }
}
```

- [ ] **Step 2: Create the Razor template**

Create `src/VanillaStudio/Templates/SliceFactory/Components/Shared/DirectoryTreePicker.razor`:

```razor
@namespace {{RootNamespace}}.SliceFactory.Components.Shared

<div class="directory-tree-picker">
    @if (_roots.Count == 0)
    {
        <p class="text-muted small">No existing directories. Type a path directly or generate your first slice.</p>
    }
    else
    {
        <ul class="tree-root list-unstyled mb-0">
            @foreach (var node in _roots)
            {
                @RenderNode(node)
            }
        </ul>
    }

    @if (!string.IsNullOrEmpty(_selectedPath))
    {
        <div class="mt-2 text-muted small">
            Selected: <code>@_selectedPath</code>
        </div>
    }
</div>

@code {
    private RenderFragment RenderNode(DirectoryNode node) => __builder =>
    {
        <li class="tree-node">
            <div class="tree-node-row @(_selectedPath == node.FullPath ? "selected" : "")">
                <span class="tree-toggle" @onclick="() => ToggleExpand(node)">
                    @if (node.Children.Count > 0 || _selectedPath == node.FullPath)
                    {
                        @(node.IsExpanded ? "▾" : "▸")
                    }
                    else
                    {
                        <span style="width:1em;display:inline-block;"></span>
                    }
                </span>
                <span class="tree-label" @onclick="() => SelectNode(node)">
                    📁 @node.Name
                </span>
            </div>

            @if (node.IsExpanded || _selectedPath == node.FullPath)
            {
                <ul class="list-unstyled ms-3">
                    @foreach (var child in node.Children)
                    {
                        @RenderNode(child)
                    }

                    @* New folder input appears inside the selected node only *@
                    @if (_selectedPath == node.FullPath && _showNewFolderInput)
                    {
                        <li class="tree-node tree-new-folder">
                            <span>📁 </span>
                            <input class="form-control form-control-sm d-inline-block w-auto"
                                   @bind="_newFolderName"
                                   @onkeydown="HandleNewFolderKey"
                                   placeholder="new folder name"
                                   autofocus />
                        </li>
                    }
                </ul>
            }
        </li>
    };

    private async Task HandleNewFolderKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await ConfirmNewFolder();
        if (e.Key == "Escape") CancelNewFolder();
    }
}
```

- [ ] **Step 3: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Components/Shared/
git commit -m "feat: DirectoryTreePicker - manifest-driven folder tree with inline new-folder"
```

---

## Task 10: Index.razor + FormViewModel v2

**Files:**
- Modify: `src/VanillaStudio/Templates/SliceFactory/Components/Pages/Index.razor.cs`
- Modify: `src/VanillaStudio/Templates/SliceFactory/Components/Pages/Index.razor`

**Interfaces:**
- Consumes: `DirectoryTreePicker` (Task 9); `FeatureManagementService.PreviewFeatureFilesAsync(listing, form, action, selectList, ...)` (Task 6); `ManifestService.GetAllSlicesAsync()` for tree paths

- [ ] **Step 1: Update FormViewModel**

In `Index.razor.cs`, replace the `FormViewModel` class:

```csharp
public class FormViewModel
{
    [Required]
    public string? DirectoryName { get; set; }

    public string NameSpace { get; set; } = "";

    // Per-slice enable + name pairs
    public bool EnableListing { get; set; } = true;
    public string? ListingName { get; set; }

    public bool EnableForm { get; set; } = true;
    public string? FormName { get; set; }

    public bool EnableAction { get; set; }
    public string? ActionName { get; set; }

    public bool EnableSelectList { get; set; }
    public string? SelectListName { get; set; }
    public string SelectListModelType { get; set; } = "SelectOption";
    public string SelectListDataType { get; set; } = "string";

    public bool GenerateControllerAndClientService { get; set; } = true;

    [Required]
    public string? PkType { get; set; } = "Guid";

    public bool HasAnySlice =>
        (EnableListing && !string.IsNullOrWhiteSpace(ListingName)) ||
        (EnableForm && !string.IsNullOrWhiteSpace(FormName)) ||
        (EnableAction && !string.IsNullOrWhiteSpace(ActionName)) ||
        (EnableSelectList && !string.IsNullOrWhiteSpace(SelectListName));
}
```

- [ ] **Step 2: Update OnInitializedAsync to load tree paths**

In the `Index` partial class, add a field for tree paths and populate it:

```csharp
private IReadOnlyList<string> _directoryPaths = Array.Empty<string>();

// In OnInitializedAsync, after existing code:
var allSlices = await _manifestService.GetAllSlicesAsync();  // inject ManifestService
_directoryPaths = allSlices.Select(s => s.Directory).Distinct().ToList();
```

Inject `ManifestService` into the `Index` partial class:
```csharp
[Inject] private ManifestService ManifestService { get; set; } = default!;
```

- [ ] **Step 3: Update TriggerDebouncedPreview to use new FormViewModel fields**

Find the preview call in `Index.razor.cs` that calls `PreviewFeatureFilesAsync`. Replace it with the v2 signature:

```csharp
// Build SliceDescriptors from FormViewModel
var listing = M.EnableListing && !string.IsNullOrWhiteSpace(M.ListingName)
    ? new SliceDescriptor(M.ListingName!, NameDerivationService.DerivePrefix(M.ListingName!))
    : null;

var form = M.EnableForm && !string.IsNullOrWhiteSpace(M.FormName)
    ? new SliceDescriptor(M.FormName!, NameDerivationService.DerivePrefix(M.FormName!))
    : null;

var action = M.EnableAction && !string.IsNullOrWhiteSpace(M.ActionName)
    ? new SliceDescriptor(M.ActionName!, NameDerivationService.DerivePrefix(M.ActionName!))
    : null;

var selectList = M.EnableSelectList && !string.IsNullOrWhiteSpace(M.SelectListName)
    ? new SelectListDescriptor(M.SelectListName!, NameDerivationService.DerivePrefix(M.SelectListName!),
        M.SelectListModelType, M.SelectListDataType)
    : null;

PreviewFiles = await FeatureService.PreviewFeatureFilesAsync(
    listing: listing,
    form: form,
    action: action,
    selectList: selectList,
    moduleNamespace: M.NameSpace,
    projectNamespace: $"{profile?.Projects?.FirstOrDefault()?.NameSpace}.{M.NameSpace}",
    primaryKeyType: M.PkType ?? "Guid",
    basePath: basePath,
    directoryName: M.DirectoryName ?? "",
    projects: profile?.Projects?.ToList() ?? new());
```

- [ ] **Step 4: Replace DirectoryName text field in Index.razor**

Find the `DirectoryName` text input in `Index.razor`. Replace it with:

```razor
<div class="mb-3">
    <label class="form-label">Directory</label>
    <DirectoryTreePicker
        KnownPaths="_directoryPaths"
        Value="@M.DirectoryName"
        OnPathSelected="@(path => { M.DirectoryName = path; TriggerDebouncedPreview(); })" />
    @if (!string.IsNullOrEmpty(M.DirectoryName))
    {
        <input type="hidden" @bind="M.DirectoryName" />
    }
</div>
```

- [ ] **Step 5: Replace ComponentPrefix/FeaturePluralName fields with per-slice name inputs**

Find the `ComponentPrefix` and `FeaturePluralName` inputs in `Index.razor`. Replace them with the per-slice section:

```razor
<div class="mb-3">
    <div class="form-check mb-1">
        <input class="form-check-input" type="checkbox" @bind="M.EnableListing" id="chkListing" />
        <label class="form-check-label" for="chkListing">Listing</label>
    </div>
    @if (M.EnableListing)
    {
        <input class="form-control form-control-sm" placeholder='e.g. "Doctors"'
               @bind="M.ListingName" @oninput="TriggerDebouncedPreview" />
    }
</div>

<div class="mb-3">
    <div class="form-check mb-1">
        <input class="form-check-input" type="checkbox" @bind="M.EnableForm" id="chkForm" />
        <label class="form-check-label" for="chkForm">Form</label>
    </div>
    @if (M.EnableForm)
    {
        <input class="form-control form-control-sm" placeholder='e.g. "Doctor Profile"'
               @bind="M.FormName" @oninput="TriggerDebouncedPreview" />
    }
</div>

<div class="mb-3">
    <div class="form-check mb-1">
        <input class="form-check-input" type="checkbox" @bind="M.EnableAction" id="chkAction" />
        <label class="form-check-label" for="chkAction">Action</label>
    </div>
    @if (M.EnableAction)
    {
        <input class="form-control form-control-sm" placeholder='e.g. "Disable Doctor"'
               @bind="M.ActionName" @oninput="TriggerDebouncedPreview" />
    }
</div>

<div class="mb-3">
    <div class="form-check mb-1">
        <input class="form-check-input" type="checkbox" @bind="M.EnableSelectList" id="chkSelect" />
        <label class="form-check-label" for="chkSelect">Select List</label>
    </div>
    @if (M.EnableSelectList)
    {
        <input class="form-control form-control-sm" placeholder='e.g. "Doctor Types"'
               @bind="M.SelectListName" @oninput="TriggerDebouncedPreview" />
    }
</div>
```

- [ ] **Step 6: Smoke test the Web UI**

Run the SliceFactory web app:
```
dotnet run --project src/VanillaStudio/ZKnow.VanillaStudio.csproj
```

Open `http://localhost:5000`. Verify:
1. The directory tree picker shows existing manifest directories (or "no existing directories" if manifest is empty)
2. Clicking a directory node selects it and shows the path below the tree
3. Typing a name in the "new folder" input and pressing Enter creates a node and selects it
4. Checking "Listing" shows a text input for the listing name
5. Typing "Doctors" in the listing name input triggers live file preview
6. Derived prefixes appear correct in the preview file names

- [ ] **Step 7: Commit**

```
git add src/VanillaStudio/Templates/SliceFactory/Components/Pages/Index.razor \
        src/VanillaStudio/Templates/SliceFactory/Components/Pages/Index.razor.cs
git commit -m "feat: Index.razor v2 - per-slice name inputs, DirectoryTreePicker integration"
```

---

## Self-Review

**Spec coverage check:**
- ✅ §2 CLI API: `--listing`, `--form`, `--action`, `--select-list` accept string values (Task 4); `--prefix`/`--plural` removed; `--namespace`/`--directory` unchanged
- ✅ §2.3 Name derivation: `NameDerivationService` with `-ies→-y` and `-s` rules (Task 1)
- ✅ §3 Action Slice: 4 template files (Task 7); wired in `FeatureManagementService` (Task 6); CLI flag (Task 4)
- ✅ §4.1 Manifest v2 JSON shape: `SliceDescriptor`, `SelectListDescriptor`, nullable per-slice fields (Task 2)
- ✅ §4.2 C# model: `SliceDefinition` with nullable descriptors (Task 2); `Feature.cs` updated (Task 5)
- ✅ §4.3 v1→v2 migration: `MigrateV1()` in `ManifestService.LoadAsync()` with backup (Task 3)
- ✅ §5 Directory tree: `DirectoryTreePicker` (Task 9); built from manifest paths (Task 10); new-folder inside selected node (Task 9)
- ✅ `regenerate-all` still works — `RegenerateAsync` reads v2 manifest fields (Task 8)

**Placeholder scan:** No TBD, TODO, or "similar to" references. All steps show complete code.

**Type consistency:**
- `SliceDescriptor(string Name, string Prefix)` — used consistently in Tasks 2, 3, 5, 6, 8, 10
- `SelectListDescriptor(string Name, string Prefix, string ModelType, string DataType)` — used in Tasks 2, 3, 6, 8, 10
- `FeatureManagementService.CreateFeatureAsync(listing, form, action, selectList, ...)` — signature defined in Task 6, called in Task 8
- `PreviewFeatureFilesAsync` — same parameter order as `CreateFeatureAsync` (listing, form, action, selectList first)
- `NameDerivationService.DerivePrefix(string)` — static, called in Tasks 3, 8, 10
- `SliceDefinition.GenerateId(string ns, string directory)` — signature updated in Task 2, used in Tasks 3, 8
