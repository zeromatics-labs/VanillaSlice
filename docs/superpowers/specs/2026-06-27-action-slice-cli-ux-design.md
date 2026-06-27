# VanillaSlice: Action Slice + CLI UX Redesign
**Date:** 2026-06-27  
**Status:** Approved  
**Scope:** Three independent features delivered together — (1) per-slice semantic naming replacing `--prefix`/`--plural`, (2) Action Slice type, (3) Web UI directory tree picker built from manifest.

---

## 1. Motivation

The current CLI conflates naming concerns: `--prefix Doctor --plural Doctors --form --listing` forces all slice types to share one prefix and one plural, even when they need distinct identities (e.g., a listing titled "Doctors" and a form titled "Doctor Profile"). The generator is also used by agents reading the manifest, so the CLI must stay argument-driven while the Web UI gets richer interactive tooling.

---

## 2. CLI API Redesign (v2)

### 2.1 Removed flags
| Removed | Reason |
|---|---|
| `--prefix / -p` | Each slice carries its own name, prefix derived per-slice |
| `--plural` | Derived automatically from listing name |

### 2.2 New flag signatures

Each slice flag now accepts a **display name string** as its value. The flag enables the slice type AND names it.

```
slice generate
  --listing    "Doctors"           # enables listing slice; name = "Doctors"
  --form       "Doctor Profile"    # enables form slice; name = "Doctor Profile"
  --action     "Disable Doctor"    # enables action slice; name = "Disable Doctor"
  --select-list "Doctor Types"     # enables select list slice; name = "Doctor Types"
  --namespace   ZeroLegal.Doctors  # module namespace (required)
  --directory   Features/Doctors   # relative output path (required)
  --pk          Guid               # primary key type (default: Guid)
  --preview                        # preview without writing files
  --id          <slice-id>         # used by regenerate only
```

At least one slice flag must be provided. All four are independent — any combination is valid.

### 2.3 Name-to-prefix derivation

| Input name | Derived prefix | Rule |
|---|---|---|
| `"Doctors"` | `Doctor` | Single word ending in -s/-es: singularize |
| `"Babies"` | `Baby` | Single word ending in -ies: strip -ies, append -y |
| `"Doctor Profile"` | `DoctorProfile` | Multi-word: PascalCase each word, join |
| `"Disable Doctor"` | `DisableDoctor` | Multi-word: PascalCase each word, join |
| `"Doctor Types"` | `DoctorType` | Multi-word where last word is plural: singularize last word, PascalCase join |

The **display name** is preserved in the manifest for use in UI titles, page headings, and MAUI page labels. The **prefix** drives all code artifact names.

### 2.4 Unchanged flags
`--namespace`, `--directory`, `--pk`, `--preview`, `--id`, and all sub-commands (`regenerate`, `regenerate-all`, `list`, `remove`) are unchanged.

### 2.5 Validation rules
- At least one of `--listing`, `--form`, `--action`, `--select-list` must be provided.
- `--namespace` and `--directory` are required on `generate`.
- `--pk` must be one of: `Guid`, `string`, `int`, `long`.

---

## 3. Action Slice

### 3.1 Purpose
A discrete backend mutation with no UI template. Used for single-verb operations (Disable, Approve, Archive, Forward) where the triggering UI is hand-written in the consuming feature.

### 3.2 Generated artifacts for `--action "Disable Doctor"`

| Layer | File | Key content |
|---|---|---|
| ServiceContracts | `IDisableDoctorActionDataService.cs` | `Task ExecuteAsync(TKey id)` |
| Controllers | `DisableDoctorActionController.cs` | `POST api/DisableDoctorAction/{id}/execute` |
| ServerSideServices | `DisableDoctorActionServerDataService.cs` | Interface implementation, neutral DI |
| ClientShared | `DisableDoctorActionClientDataService.cs` | HTTP relay via `PostAsJsonAsync` |

No `.razor`, no ViewModel, no BusinessModel — purely backend.

### 3.3 Template placeholders
- `__ActionPrefix__` → derived prefix (e.g., `DisableDoctor`)
- `__PrimaryKeyType__` → from `--pk`
- `__Namespace__` → from `--namespace`

### 3.4 CLI validation
`--action` has no dependency on `--listing` or `--form`. It is fully standalone.

---

## 4. Manifest v2

### 4.1 Schema change

```json
{
  "version": 2,
  "slices": [
    {
      "id": "zerolegal.doctors-doctor",
      "namespace": "ZeroLegal.Doctors",
      "directory": "Features/Doctors",
      "primaryKeyType": "Guid",
      "createdAt": "2026-01-01T00:00:00Z",
      "lastGeneratedAt": "2026-06-27T00:00:00Z",
      "listing":    { "name": "Doctors",         "prefix": "Doctor" },
      "form":       { "name": "Doctor Profile",  "prefix": "DoctorProfile" },
      "action":     { "name": "Disable Doctor",  "prefix": "DisableDoctor" },
      "selectList": { "name": "Doctor Types",    "prefix": "DoctorType" },
      "generatedFiles": ["..."]
    }
  ]
}
```

Absent slice types are `null` / omitted — presence indicates the slice was generated.

### 4.2 C# model changes

```csharp
public record SliceDescriptor(string Name, string Prefix);

// SelectList carries extra config that other slice types don't need
public record SelectListDescriptor(string Name, string Prefix,
    string ModelType = "SelectOption", string DataType = "string")
    : SliceDescriptor(Name, Prefix);

public class SliceDefinition
{
    public int Version { get; set; } = 2;
    public string Id { get; set; }
    public string Namespace { get; set; }
    public string Directory { get; set; }
    public string PrimaryKeyType { get; set; } = "Guid";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastGeneratedAt { get; set; }

    public SliceDescriptor? Listing { get; set; }
    public SliceDescriptor? Form { get; set; }
    public SliceDescriptor? Action { get; set; }
    public SelectListDescriptor? SelectList { get; set; }  // typed subclass

    public List<string> GeneratedFiles { get; set; } = new();
}
```

Old flat booleans (`GenerateForm`, `GenerateListing`, `GenerateSelectList`, `ComponentPrefix`, `FeaturePluralName`, `SelectListModelType`, `SelectListDataType`) are removed from the v2 model. `SelectListModelType` and `SelectListDataType` move into `SelectListDescriptor`.

### 4.3 v1 → v2 migration

Triggered automatically on first load when `"version"` field is absent or `< 2`.

| v1 field | v2 mapping |
|---|---|
| `ComponentPrefix` + `FeaturePluralName` (if `GenerateListing: true`) | `listing: { name: FeaturePluralName, prefix: ComponentPrefix }` |
| `ComponentPrefix` (if `GenerateForm: true`) | `form: { name: ComponentPrefix, prefix: ComponentPrefix }` |
| `ComponentPrefix` (if `GenerateSelectList: true`) | `selectList: { name: ComponentPrefix + " Types", prefix: ComponentPrefix }` |
| No v1 action field | `action: null` |

Migration writes `"version": 2` and saves. Original file is backed up as `slices-manifest.v1.json` before writing. Migration is non-destructive.

---

## 5. Web UI Directory Tree Picker

### 5.1 Component
`Components/Shared/DirectoryTreePicker.razor`

**Parameters:**
```csharp
[Parameter] public IReadOnlyList<string> KnownPaths { get; set; }  // from manifest
[Parameter] public EventCallback<string> OnPathSelected { get; set; }
[Parameter] public string? Value { get; set; }  // current selected path
```

### 5.2 Tree construction
On component init, each `KnownPaths` entry (e.g., `"Features/Legal/Matters"`) is split on `/` and merged into an in-memory tree of `DirectoryNode` objects. No filesystem access.

```csharp
record DirectoryNode(string Name, List<DirectoryNode> Children);
```

### 5.3 UX behaviour

```
📁 Features
  📁 Doctors
  📁 Legal            ← selected (highlighted)
    📁 Matters
    📁 Parties
    [📁 _____________]  ← inline text input, only inside selected node
  📁 HR
    📁 Employees
```

- **Select** — click a node to select it; emits `OnPathSelected` with the full path
- **Expand/collapse** — arrow icon toggles children independently of selection  
- **New folder input** — appears as the last child of the selected node only; pressing Enter creates the node, selects it, and emits `OnPathSelected`; pressing Escape cancels
- **Breadcrumb** — selected path shown as read-only text below the tree: `Features / Legal / Matters`
- **New root-level paths** — user can type a brand-new top-level segment; it is appended to the tree root

### 5.4 Integration in Index.razor
- `DirectoryName` text field is replaced by `<DirectoryTreePicker>`
- `OnPathSelected` handler sets `FormViewModel.DirectoryName`
- `KnownPaths` fed from `ManifestService.GetAllSlicesAsync()` → `.Select(s => s.Directory)`

---

## 6. Out of Scope (this spec)
- Panel slices and Card slices (separate future spec)
- Fine-grained CRUD control flags (`--no-create`, `--no-edit`, `--delete`)
- Action prefix independence (`--action-prefix` separate from derived prefix)
- MAUI template polish (tracked separately as MAUI Day Germany track)

---

## 7. File Change Surface

| File | Change |
|---|---|
| `Cli/CliOptions.cs` | Remove `--prefix`, `--plural`; change `--form`/`--listing`/`--action`/`--select-list` to accept string values |
| `Cli/CliRunner.cs` | Update `GenerateAsync()` to build `Feature` from named slice descriptors |
| `Models/Feature.cs` | Add per-slice name+prefix fields; remove shared `ComponentPrefix`/`FeaturePluralName` |
| `Cli/SliceManifest.cs` | Replace flat booleans with `SliceDescriptor?` fields; add `Version` |
| `Services/ManifestService.cs` | Add v1→v2 migration on load |
| `Services/FeatureManagementService.cs` | Drive template selection from `SliceDescriptor` presence; add Action slice handling |
| `Templates/*/Action/` | New Action slice templates (4 files) |
| `Components/Shared/DirectoryTreePicker.razor` | New component |
| `Components/Pages/Index.razor(.cs)` | Replace directory text field with `DirectoryTreePicker` |
