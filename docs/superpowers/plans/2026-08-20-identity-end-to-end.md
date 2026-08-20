# Identity End-to-End (Phase A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make ASP.NET Core Identity actually work — register, confirm, sign in, call an authorized endpoint, refresh, sign out — in every project VanillaStudio generates, across Blazor Web, MAUI Hybrid, and MAUI Native.

**Architecture:** `WebPortal` gets Microsoft's `dotnet new blazor --auth Individual` Account scaffold copied in wholesale and authenticates in-process via `SignInManager` with cookies. `WebAPI` exposes `app.MapGroup("/identity").MapIdentityApi<ApplicationUser>()` for bearer clients. Both share one `AppDbContext`, so one user store with two entry points. MAUI hosts get four screens each over a `MauiAuthenticationStateProvider` backed by `SecureStorage`.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core (SQL Server / PostgreSQL / SQLite), Blazor (SSR + Interactive), .NET MAUI (Blazor Hybrid + XAML Native), xUnit 2.9.3.

**Spec:** `docs/superpowers/specs/2026-08-20-identity-end-to-end-design.md`

## Global Constraints

- **Templates are not compiled.** `ZKnow.VanillaStudio.csproj` has `<Compile Remove="Templates\**\*" />` and includes them as `EmbeddedResource` with `CopyToOutputDirectory=Always`. Template `.cs` files contain `{{ProjectName}}` and would never compile. Never assume a template change is validated by `dotnet build`.
- **Trailing-underscore convention.** A template file whose name ends in `_` has that character stripped at generation (`RestoreFileExtensions`). Files that would break the Studio build or IDE tooling if left as-is — `.razor`, `.json`, `.css` — are stored as `Login.razor_`, `appsettings.json_`. `.cs` files under `Templates/` are already excluded from compilation and are stored **without** a trailing underscore. Follow the convention already present in the folder you are editing.
- **File-name placeholder substitution is by parameter NAME, not `{{...}}`.** `ProcessFileNamePlaceholders` runs `path.Replace(parameter.Key, parameter.Value)` for every parameter. After Task 7 the parameter set includes `EmailProvider` and `EmailFromAddress`. **No file or directory added by this plan may contain the literal text `ProjectName`, `RootNamespace`, `TargetFramework`, `AspNetCoreVersion`, `RenderingMode`, `IncludeAuthentication`, `UserSecretsId`, `UIFramework`, `EmailProvider`, or `EmailFromAddress` unless substitution is intended.**
- **Target framework for all verification:** `DotNetVersion.Net10`. This machine has no .NET 9 runtime installed; a generated net9 project cannot be built locally.
- **MAUI builds are not part of CI.** They require the MAUI workload. Tasks 9–13 are verified by generated-content assertions plus manual device/emulator runs.
- **`RequireConfirmedAccount` stays `true`.** Never weaken it to make a flow pass.
- **Identity tokens are not JWTs.** They are opaque ASP.NET Core Identity tokens. Do not describe them as JWTs in code comments, docs, or the README.
- **Conditional block syntax** is `{{#if (eq ParameterName "Value")}}…{{/if}}` and `{{#unless (eq ParameterName "Value")}}…{{/unless}}`. Matching is `Singleline` + `IgnoreCase`. Nested conditionals are **not** supported by the regex — do not nest them.
- **Commit after every task.** Branch off `main` before starting.

---

## File Structure

**New — test infrastructure**
- `tests/VanillaSlice.Tests/TemplateTestFixture.cs` — locates `src/VanillaStudio/Templates`, builds a `TemplateEngineService`, runs a generation and returns `List<GeneratedFile>` for assertions.
- `tests/VanillaSlice.Tests/IdentityGenerationTests.cs` — all assertions about identity output.
- `tests/VanillaSlice.Tests/DatabaseProviderTests.cs` — provider-conditional assertions.

**Modified — generator**
- `src/VanillaStudio/Services/TemplateEngineService.cs` — testable templates path; file-level exclusion.
- `src/VanillaStudio/Services/WebPortalProjectsGenerator.cs` — pass exclusions and new parameters.
- `src/VanillaStudio/Services/PlatformProjectsGenerator.cs` — WebAPI parameters.
- `src/VanillaStudio/Models/ProjectConfiguration.cs` — `EmailProvider`, `EmailFromAddress`.
- `src/VanillaStudio/Components/Pages/ProjectWizard.razor` — email provider control.

**Modified — templates**
- `Templates/WebPortal/Program.cs`, `Templates/WebAPI/Program.cs` — auth pipeline, identity endpoints, provider conditionals.
- `Templates/WebPortal/ProjectName.WebPortal.csproj`, `Templates/ServerData/ProjectName.Server.Data.csproj`, `Templates/WebAPI/ProjectName.WebAPI.csproj` — provider-conditional EF packages.
- `Templates/HybridApp/MauiProgram.cs`, `Templates/MauiNativeApp/MauiProgram.cs` — auth service registration.
- `Templates/ClientShared/Helpers/BaseHttpClient.cs` — refresh-on-401.

**New — templates**
- `Templates/WebPortal/Components/Account/**` — the copied scaffold (~20 pages + support classes).
- `Templates/WebPortal/Services/DevEmailSender.cs`, `SmtpEmailSender.cs`, `SendGridEmailSender.cs`.
- `Templates/ClientShared/Identity/` — `IdentityClient.cs`, `TokenStorage.cs`, `HttpClientHelper.cs`, `MauiAuthenticationStateProvider.cs`, and the four ViewModels.
- `Templates/RazorLibrary/Features/Account/` — four Hybrid screens.
- `Templates/MauiNativeApp/Features/Account/` — four Native screens.

**Deleted**
- `Templates/HybridApp/Services/TokenHandler.cs`.

---

## Task 1: Test harness for generated output

There is currently no way to assert on generated content. Every later task depends on this.

**Files:**
- Modify: `src/VanillaStudio/Services/TemplateEngineService.cs:14-45`
- Modify: `tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj`
- Create: `tests/VanillaSlice.Tests/TemplateTestFixture.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `TemplateTestFixture.Generate(string templateName, Dictionary<string, object> parameters)` returning `List<GeneratedFile>`; `TemplateTestFixture.FileContent(List<GeneratedFile> files, string endsWith)` returning `string`.

- [ ] **Step 1: Add the test project's dependencies**

In `tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj`, add inside the existing `<ItemGroup>`:

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
```

And add a new item group:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\VanillaStudio\ZKnow.VanillaStudio.csproj" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing test**

Create `tests/VanillaSlice.Tests/TemplateTestFixture.cs`:

```csharp
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
```

Then create `tests/VanillaSlice.Tests/IdentityGenerationTests.cs`:

```csharp
using Xunit;

namespace VanillaSlice.Tests;

public class IdentityGenerationTests
{
    internal static Dictionary<string, object> WebPortalParams(
        bool includeAuth = true,
        string uiFramework = "Bootstrap",
        string databaseProvider = "SqlServer") => new()
    {
        ["ProjectName"] = "Acme",
        ["RootNamespace"] = "Acme.WebPortal",
        ["TargetFramework"] = "net10.0",
        ["AspNetCoreVersion"] = "10.0.0",
        ["RenderingMode"] = "Auto",
        ["IncludeAuthentication"] = includeAuth,
        ["UserSecretsId"] = "test-secrets-id",
        ["UIFramework"] = uiFramework,
        ["DatabaseProvider"] = databaseProvider,
    };

    [Fact]
    public void WebPortal_template_generates_a_Program_file()
    {
        var files = TemplateTestFixture.Generate("WebPortal", WebPortalParams());
        Assert.True(TemplateTestFixture.HasFile(files, "Program.cs"));
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~IdentityGenerationTests
```

Expected: **compile error** — `TemplateEngineService` has no constructor taking a templates path, and `GenerateFromTemplateAsync` has no `includeFile` parameter.

- [ ] **Step 4: Add the testable constructor and the exclusion hook**

In `src/VanillaStudio/Services/TemplateEngineService.cs`, replace the existing constructor with an overload pair. Keep the existing discovery logic as the default path:

```csharp
public TemplateEngineService(ILogger<TemplateEngineService> logger, string templatesBasePath)
{
    _logger = logger;
    _templatesBasePath = templatesBasePath;
    _logger.LogInformation("📁 Templates base path (explicit): {TemplatesPath}", _templatesBasePath);
    InitializeTemplateEngine();
}
```

Change the signature of `GenerateFromTemplateAsync` to accept an optional predicate and forward it:

```csharp
public async Task<List<GeneratedFile>> GenerateFromTemplateAsync(
    string templateName,
    Dictionary<string, object> parameters,
    string outputBasePath = "",
    Func<string, bool>? includeFile = null)
```

and pass it through to `ProcessTemplateFilesAsync(templatePath, parameters, outputBasePath, includeFile)`.

In `ProcessTemplateFilesAsync`, add the parameter and apply it to the file list. The predicate receives the **template-relative path with forward slashes**, before placeholder substitution:

```csharp
private async Task<List<GeneratedFile>> ProcessTemplateFilesAsync(
    string templatePath,
    Dictionary<string, object> parameters,
    string outputBasePath,
    Func<string, bool>? includeFile = null)
{
    var generatedFiles = new List<GeneratedFile>();

    var templateFiles = Directory.GetFiles(templatePath, "*", SearchOption.AllDirectories)
        .Where(f => !f.Contains(".template.config"))
        .Where(f => includeFile is null ||
                    includeFile(Path.GetRelativePath(templatePath, f).Replace('\\', '/')))
        .ToArray();
    // ... rest of the method unchanged
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~IdentityGenerationTests
```

Expected: **PASS**, 1 test.

- [ ] **Step 6: Confirm nothing else broke**

```bash
dotnet build src/ZKnow.VanillaStudio.sln
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: build succeeds; all pre-existing tests still pass.

- [ ] **Step 7: Commit**

```bash
git add tests/VanillaSlice.Tests src/VanillaStudio/Services/TemplateEngineService.cs
git commit -m "test(studio): harness for asserting on generated template output"
```

---

## Task 2: Exclude the Account folder when authentication is off

Task 6 adds ~20 Account pages. Without this, they are emitted even when the user unticks authentication.

**Files:**
- Modify: `src/VanillaStudio/Services/WebPortalProjectsGenerator.cs:44-70`
- Modify: `tests/VanillaSlice.Tests/IdentityGenerationTests.cs`

**Interfaces:**
- Consumes: `GenerateFromTemplateAsync(…, Func<string,bool>? includeFile)` from Task 1.
- Produces: `WebPortalProjectsGenerator.IncludeFileFor(ProjectConfiguration config)` returning `Func<string, bool>`.

- [ ] **Step 1: Write the failing test**

Append to `tests/VanillaSlice.Tests/IdentityGenerationTests.cs`:

```csharp
using ZKnow.VanillaStudio.Models;
using ZKnow.VanillaStudio.Services;

// ...inside IdentityGenerationTests

[Fact]
public void Account_pages_are_excluded_when_authentication_is_off()
{
    var config = new ProjectConfiguration { ProjectName = "Acme", IncludeAuthentication = false };
    var predicate = WebPortalProjectsGenerator.IncludeFileFor(config);

    Assert.False(predicate("Components/Account/Pages/Login.razor_"));
    Assert.False(predicate("Components/Account/IdentityUserAccessor.cs"));
    Assert.True(predicate("Program.cs"));
}

[Fact]
public void Account_pages_are_included_when_authentication_is_on()
{
    var config = new ProjectConfiguration { ProjectName = "Acme", IncludeAuthentication = true };
    var predicate = WebPortalProjectsGenerator.IncludeFileFor(config);

    Assert.True(predicate("Components/Account/Pages/Login.razor_"));
    Assert.True(predicate("Program.cs"));
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~Account_pages
```

Expected: **compile error** — `IncludeFileFor` does not exist.

- [ ] **Step 3: Implement the predicate**

In `src/VanillaStudio/Services/WebPortalProjectsGenerator.cs`, add:

```csharp
/// <summary>
/// Decides which template files belong in the generated WebPortal project.
/// The path is template-relative with forward slashes, before placeholder substitution.
/// </summary>
public static Func<string, bool> IncludeFileFor(ProjectConfiguration config) => path =>
{
    if (!config.IncludeAuthentication &&
        path.StartsWith("Components/Account/", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return true;
};
```

- [ ] **Step 4: Wire it into generation**

In `GenerateWebPortalProjectAsync`, pass the predicate as the fourth argument:

```csharp
var generatedFiles = await _templateEngine.GenerateFromTemplateAsync(
    "WebPortal",
    parameters,
    $"{config.ProjectName}.WebPortal/{config.ProjectName}.WebPortal",
    IncludeFileFor(config));
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 6: Commit**

```bash
git add src/VanillaStudio/Services/WebPortalProjectsGenerator.cs tests/VanillaSlice.Tests/IdentityGenerationTests.cs
git commit -m "feat(studio): exclude Account templates when authentication is disabled"
```

---

## Task 3: Database provider conditionals

Spec §3.4. Identity ships the first EF migrations, so a PostgreSQL or SQLite user hits this immediately and reads it as "Identity is broken". Both `Program.cs` **and** the `PackageReference` entries must branch.

**Files:**
- Modify: `Templates/WebPortal/Program.cs:47-49`
- Modify: `Templates/WebAPI/Program.cs:10-12`
- Modify: `Templates/WebPortal/ProjectName.WebPortal.csproj`
- Modify: `Templates/WebAPI/ProjectName.WebAPI.csproj`
- Modify: `Templates/ServerData/ProjectName.Server.Data.csproj`
- Modify: `src/VanillaStudio/Services/WebPortalProjectsGenerator.cs`, `PlatformProjectsGenerator.cs`
- Create: `tests/VanillaSlice.Tests/DatabaseProviderTests.cs`

**Interfaces:**
- Consumes: `TemplateTestFixture` (Task 1).
- Produces: a `DatabaseProvider` template parameter on WebPortal and WebAPI generation, with values `SqlServer`, `SQLite`, `PostgreSQL`, `None`.

- [ ] **Step 1: Write the failing test**

Create `tests/VanillaSlice.Tests/DatabaseProviderTests.cs`:

```csharp
using Xunit;

namespace VanillaSlice.Tests;

public class DatabaseProviderTests
{
    [Theory]
    [InlineData("SqlServer", "UseSqlServer")]
    [InlineData("PostgreSQL", "UseNpgsql")]
    [InlineData("SQLite", "UseSqlite")]
    public void WebPortal_Program_uses_the_selected_provider(string provider, string expectedCall)
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: provider));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains(expectedCall, program);
    }

    [Theory]
    [InlineData("PostgreSQL", "UseSqlServer")]
    [InlineData("SQLite", "UseSqlServer")]
    public void WebPortal_Program_does_not_leak_other_providers(string provider, string absentCall)
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: provider));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.DoesNotContain(absentCall, program);
    }

    [Theory]
    [InlineData("SqlServer", "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("PostgreSQL", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("SQLite", "Microsoft.EntityFrameworkCore.Sqlite")]
    public void WebPortal_csproj_references_the_matching_EF_provider(string provider, string package)
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: provider));
        var csproj = TemplateTestFixture.FileContent(files, ".WebPortal.csproj");

        Assert.Contains(package, csproj);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~DatabaseProviderTests
```

Expected: **FAIL** — every case asserts `UseSqlServer` today; `UseNpgsql`, `UseSqlite`, and the non-SqlServer packages are absent.

- [ ] **Step 3: Branch the DbContext registration**

In `Templates/WebPortal/Program.cs`, replace the hardcoded registration:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{{#if (eq DatabaseProvider "SqlServer")}}
    options.UseSqlServer(connectionString));
{{/if}}
{{#if (eq DatabaseProvider "PostgreSQL")}}
    options.UseNpgsql(connectionString));
{{/if}}
{{#if (eq DatabaseProvider "SQLite")}}
    options.UseSqlite(connectionString));
{{/if}}
```

Apply the identical change to `Templates/WebAPI/Program.cs`.

- [ ] **Step 4: Branch the package references**

In `Templates/WebPortal/ProjectName.WebPortal.csproj`, replace the unconditional SqlServer reference:

```xml
{{#if (eq DatabaseProvider "SqlServer")}}
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="{{AspNetCoreVersion}}" />
{{/if}}
{{#if (eq DatabaseProvider "PostgreSQL")}}
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="{{AspNetCoreVersion}}" />
{{/if}}
{{#if (eq DatabaseProvider "SQLite")}}
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="{{AspNetCoreVersion}}" />
{{/if}}
```

Apply the same block to `Templates/WebAPI/ProjectName.WebAPI.csproj` and `Templates/ServerData/ProjectName.Server.Data.csproj`.

- [ ] **Step 5: Pass the parameter from the generators**

In `WebPortalProjectsGenerator.GenerateWebPortalProjectAsync`, add to the `parameters` dictionary:

```csharp
["DatabaseProvider"] = config.DatabaseProvider.ToString(),
```

Add the same entry wherever `PlatformProjectsGenerator` builds parameters for the `WebAPI` and `ServerData` templates.

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 7: Commit**

```bash
git add Templates src/VanillaStudio/Services tests/VanillaSlice.Tests/DatabaseProviderTests.cs
git commit -m "fix(templates): honour the selected DatabaseProvider in Program.cs and csproj"
```

---

## Task 4: WebPortal authentication pipeline

Spec §3.1. `AddIdentityCore` + `AddSignInManager` + `.AddIdentityCookies()` all stay exactly as the scaffold emits them — WebPortal does **not** map `MapIdentityApi`.

**Files:**
- Modify: `Templates/WebPortal/Program.cs`
- Modify: `tests/VanillaSlice.Tests/IdentityGenerationTests.cs`

**Interfaces:**
- Consumes: `TemplateTestFixture` (Task 1).
- Produces: a generated `Program.cs` with a complete auth pipeline. Task 6 relies on `MapAdditionalIdentityEndpoints()` being called.

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Fact]
public void WebPortal_Program_has_a_complete_auth_pipeline()
{
    var files = TemplateTestFixture.Generate("WebPortal", WebPortalParams());
    var program = TemplateTestFixture.FileContent(files, "Program.cs");

    Assert.Contains("app.UseAuthentication();", program);
    Assert.Contains("app.UseAuthorization();", program);
    Assert.Contains("app.MapAdditionalIdentityEndpoints();", program);
    Assert.DoesNotContain("//app.MapAdditionalIdentityEndpoints();", program);

    // Authentication must be established before antiforgery runs.
    Assert.True(program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal)
              < program.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal),
        "UseAuthentication must precede UseAntiforgery");
}

[Fact]
public void WebPortal_Program_keeps_cookie_registration_and_maps_no_identity_api()
{
    var files = TemplateTestFixture.Generate("WebPortal", WebPortalParams());
    var program = TemplateTestFixture.FileContent(files, "Program.cs");

    // WebPortal authenticates in-process; MapIdentityApi belongs to WebAPI only.
    Assert.Contains(".AddIdentityCookies()", program);
    Assert.DoesNotContain("MapIdentityApi", program);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~WebPortal_Program_has
```

Expected: **FAIL** — `app.UseAuthentication();` is absent and `MapAdditionalIdentityEndpoints` is commented out.

- [ ] **Step 3: Fix the pipeline**

In `Templates/WebPortal/Program.cs`, replace:

```csharp
app.UseHttpsRedirection();


app.UseAntiforgery();
```

with:

```csharp
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
```

Then uncomment the endpoint mapping near the end of the file:

```csharp
// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapControllers();
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

> `MapAdditionalIdentityEndpoints()` is an extension method supplied by `IdentityComponentsEndpointRouteBuilderExtensions.cs`, which Task 6 copies in. Between this task and Task 6 the *generated* project will not compile. That is expected and acceptable — templates are not compiled by CI, and Task 14's smoke test is the gate that proves it.

- [ ] **Step 5: Commit**

```bash
git add Templates/WebPortal/Program.cs tests/VanillaSlice.Tests/IdentityGenerationTests.cs
git commit -m "fix(templates): complete the WebPortal authentication pipeline"
```

---

## Task 5: WebAPI identity endpoints

Spec §3.2.

**Files:**
- Modify: `Templates/WebAPI/Program.cs`
- Modify: `Templates/WebAPI/ProjectName.WebAPI.csproj`
- Modify: `tests/VanillaSlice.Tests/IdentityGenerationTests.cs`

**Interfaces:**
- Consumes: `TemplateTestFixture` (Task 1).
- Produces: bearer endpoints under `/identity`. Tasks 8–12 hard-code that prefix — `/identity/login`, `/identity/register`, `/identity/refresh`, `/identity/forgotPassword`, `/identity/manage/info`.

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
internal static Dictionary<string, object> WebApiParams(bool includeAuth = true) => new()
{
    ["ProjectName"] = "Acme",
    ["RootNamespace"] = "Acme.WebAPI",
    ["TargetFramework"] = "net10.0",
    ["AspNetCoreVersion"] = "10.0.0",
    ["IncludeAuthentication"] = includeAuth,
    ["DatabaseProvider"] = "SqlServer",
};

[Fact]
public void WebAPI_Program_exposes_identity_endpoints_for_bearer_clients()
{
    var files = TemplateTestFixture.Generate("WebAPI", WebApiParams());
    var program = TemplateTestFixture.FileContent(files, "Program.cs");

    Assert.Contains("AddIdentityApiEndpoints<ApplicationUser>()", program);
    Assert.Contains("AddEntityFrameworkStores<AppDbContext>()", program);
    Assert.Contains("app.UseAuthentication();", program);
    Assert.Contains("app.UseAuthorization();", program);
    Assert.Contains("app.MapGroup(\"/identity\").MapIdentityApi<ApplicationUser>();", program);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~WebAPI_Program
```

Expected: **FAIL** — `Templates/WebAPI/Program.cs` has no authentication of any kind.

- [ ] **Step 3: Add the identity services**

In `Templates/WebAPI/Program.cs`, after the `AddDbContext` block, insert:

```csharp
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthorization();
```

Add `using Microsoft.AspNetCore.Identity;` to the top of the file.

- [ ] **Step 4: Add the pipeline steps and endpoint mapping**

Replace:

```csharp
app.UseHttpsRedirection();

app.MapControllers();
```

with:

```csharp
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/identity").MapIdentityApi<ApplicationUser>();

app.MapControllers();
```

- [ ] **Step 5: Add the Identity package reference**

In `Templates/WebAPI/ProjectName.WebAPI.csproj`, add to the main `ItemGroup`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="{{AspNetCoreVersion}}" />
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 7: Commit**

```bash
git add Templates/WebAPI tests/VanillaSlice.Tests/IdentityGenerationTests.cs
git commit -m "feat(templates): expose MapIdentityApi under /identity on WebAPI"
```

---

## Task 6: Copy the `--auth Individual` Account scaffold

Spec §4.1. This is the largest task by file count and the smallest by decision count — it is a mechanical copy, deliberately.

**Files:**
- Create: `Templates/WebPortal/Components/Account/**` (~20 pages + support classes)
- Delete: the three stub pages currently there, superseded by their scaffold equivalents
- Modify: `tests/VanillaSlice.Tests/IdentityGenerationTests.cs`

**Interfaces:**
- Consumes: `app.MapAdditionalIdentityEndpoints()` wired in Task 4.
- Produces: `IdentityComponentsEndpointRouteBuilderExtensions.MapAdditionalIdentityEndpoints()`; Razor pages routed at `/Account/Login`, `/Account/Register`, `/Account/ForgotPassword`, `/Account/ResetPassword`, `/Account/ConfirmEmail`, `/Account/Manage/*`. Task 11 links to `/Account/ResetPassword` and `/Account/ConfirmEmail` from emails.

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Theory]
[InlineData("Components/Account/Pages/Register.razor")]
[InlineData("Components/Account/Pages/Login.razor")]
[InlineData("Components/Account/Pages/ForgotPassword.razor")]
[InlineData("Components/Account/Pages/ResetPassword.razor")]
[InlineData("Components/Account/Pages/ConfirmEmail.razor")]
[InlineData("Components/Account/Pages/ResendEmailConfirmation.razor")]
[InlineData("Components/Account/Pages/Manage/ChangePassword.razor")]
[InlineData("Components/Account/Pages/Manage/EnableAuthenticator.razor")]
[InlineData("Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs")]
public void WebPortal_generates_the_full_Account_scaffold(string expectedPath)
{
    var files = TemplateTestFixture.Generate("WebPortal", WebPortalParams());
    Assert.True(TemplateTestFixture.HasFile(files, expectedPath),
        $"Expected generated file '{expectedPath}'");
}

[Fact]
public void Account_scaffold_namespaces_are_tokenised_to_the_project_name()
{
    var files = TemplateTestFixture.Generate("WebPortal", WebPortalParams());
    var register = TemplateTestFixture.FileContent(files, "Account/Pages/Register.razor");

    Assert.Contains("Acme.", register);
    Assert.DoesNotContain("{{ProjectName}}", register);
    // The scaffold's own placeholder namespace must not survive the copy.
    Assert.DoesNotContain("BlazorIdentityScaffold", register);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~Account_scaffold
```

Expected: **FAIL** — only `Login.razor_`, `ExternalLoginPicker.razor_`, `StatusMessage.razor_` and four support classes exist today.

- [ ] **Step 3: Generate the scaffold into a scratch directory**

```bash
cd "$TMPDIR" || cd /tmp
dotnet new blazor --name BlazorIdentityScaffold --auth Individual --interactivity Auto --framework net10.0
```

This produces `BlazorIdentityScaffold/Components/Account/` containing the full page set plus `IdentityComponentsEndpointRouteBuilderExtensions.cs`, `IdentityRedirectManager.cs`, `IdentityRevalidatingAuthenticationStateProvider.cs`, `IdentityUserAccessor.cs`, and `IdentityNoOpEmailSender.cs`.

- [ ] **Step 4: Copy and tokenise**

Run this from the repository root, with `SCAFFOLD` pointing at the scratch project:

```bash
SCAFFOLD="${TMPDIR:-/tmp}/BlazorIdentityScaffold"
DEST="src/VanillaStudio/Templates/WebPortal/Components/Account"

rm -rf "$DEST"
cp -r "$SCAFFOLD/Components/Account" "$DEST"

# Namespace and using-directive tokenisation.
find "$DEST" -type f \( -name '*.razor' -o -name '*.cs' \) -print0 |
  xargs -0 sed -i \
    -e 's/BlazorIdentityScaffold\.Data/{{ProjectName}}.Server.Data/g' \
    -e 's/BlazorIdentityScaffold/{{ProjectName}}.WebPortal/g' \
    -e 's/ApplicationDbContext/AppDbContext/g'

# .razor files are stored with a trailing underscore; .cs files are not
# (Templates/** is already excluded from compilation).
find "$DEST" -type f -name '*.razor' -exec sh -c 'mv "$1" "$1_"' _ {} \;

# The scaffold's no-op sender is replaced by the provider-selected sender in Task 7.
rm -f "$DEST/IdentityNoOpEmailSender.cs"

# Remove the superseded stubs.
rm -f "$DEST/Pages/Login.razor_.orig" "$DEST/Shared/StatusMessage.razor_.orig"
```

Verify the tokenisation caught everything:

```bash
grep -rn "BlazorIdentityScaffold\|ApplicationDbContext" src/VanillaStudio/Templates/WebPortal/Components/Account/ || echo "clean"
```

Expected: `clean`.

- [ ] **Step 5: Point the scaffold at `ApplicationUser`**

The scaffold references `ApplicationUser` from its own `Data` namespace. It now lives in `{{ProjectName}}.Server.Data` (`Templates/ServerData/EF/ApplicationUser.cs`). Confirm each file that references it carries the right using directive:

```bash
grep -rln "ApplicationUser" src/VanillaStudio/Templates/WebPortal/Components/Account/ |
  xargs grep -L "using {{ProjectName}}.Server.Data;"
```

Expected: no output. For any file listed, add `@using {{ProjectName}}.Server.Data` (`.razor_`) or `using {{ProjectName}}.Server.Data;` (`.cs`).

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests including Task 2's exclusion tests.

- [ ] **Step 7: Commit**

```bash
git add src/VanillaStudio/Templates/WebPortal/Components/Account tests/VanillaSlice.Tests/IdentityGenerationTests.cs
git commit -m "feat(templates): copy the full ASP.NET Core Identity Account scaffold into WebPortal"
```

---

## Task 7: Email senders and the wizard option

Spec §5 and §6. `RequireConfirmedAccount` stays `true`, so without this no account can be confirmed and every later task is unverifiable.

**Files:**
- Create: `Templates/WebPortal/Services/DevEmailSender.cs`, `SmtpEmailSender.cs`, `SendGridEmailSender.cs`
- Modify: `Templates/WebPortal/Program.cs`, `Templates/WebAPI/Program.cs`
- Modify: `src/VanillaStudio/Models/ProjectConfiguration.cs`
- Modify: `src/VanillaStudio/Components/Pages/ProjectWizard.razor`
- Modify: `src/VanillaStudio/Services/WebPortalProjectsGenerator.cs`, `PlatformProjectsGenerator.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `EmailProvider` enum (`Dev`, `Smtp`, `SendGrid`); `ProjectConfiguration.EmailProvider`, `ProjectConfiguration.EmailFromAddress`; generated `IEmailSender<ApplicationUser>` implementations. Task 14's smoke test reads confirmation links from `sent-emails/*.html`.

> **Naming constraint:** the parameter keys `EmailProvider` and `EmailFromAddress` are substituted into *file paths*. No file added here may contain that literal text in its name. `DevEmailSender.cs` is safe; a file named `EmailProviderOptions.cs` would be silently renamed.

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Fact]
public void Dev_email_sender_is_generated_and_registered_by_default()
{
    var p = WebPortalParams();
    p["EmailProvider"] = "Dev";
    p["EmailFromAddress"] = "noreply@localhost";

    var files = TemplateTestFixture.Generate("WebPortal", p);

    Assert.True(TemplateTestFixture.HasFile(files, "Services/DevEmailSender.cs"));

    var program = TemplateTestFixture.FileContent(files, "Program.cs");
    Assert.Contains("IEmailSender<ApplicationUser>, DevEmailSender", program);
    Assert.DoesNotContain("IdentityNoOpEmailSender", program);
}

[Fact]
public void Dev_email_sender_writes_links_to_disk_not_just_the_logger()
{
    var p = WebPortalParams();
    p["EmailProvider"] = "Dev";
    p["EmailFromAddress"] = "noreply@localhost";

    var files = TemplateTestFixture.Generate("WebPortal", p);
    var sender = TemplateTestFixture.FileContent(files, "Services/DevEmailSender.cs");

    // A phone or emulator has no console to read a confirmation link from.
    Assert.Contains("sent-emails", sender);
}

[Fact]
public void Smtp_sender_replaces_the_dev_sender_when_selected()
{
    var p = WebPortalParams();
    p["EmailProvider"] = "Smtp";
    p["EmailFromAddress"] = "noreply@acme.test";

    var files = TemplateTestFixture.Generate("WebPortal", p);
    var program = TemplateTestFixture.FileContent(files, "Program.cs");

    Assert.Contains("IEmailSender<ApplicationUser>, SmtpEmailSender", program);
    Assert.DoesNotContain("DevEmailSender", program);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~email_sender
```

Expected: **FAIL** — no sender templates exist; `Program.cs` still registers `IdentityNoOpEmailSender`.

- [ ] **Step 3: Create the dev sender**

Create `Templates/WebPortal/Services/DevEmailSender.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using {{ProjectName}}.Server.Data;

namespace {{ProjectName}}.WebPortal.Services;

/// <summary>
/// Development email sender. Writes each message to the logger AND to
/// sent-emails/*.html under the content root.
///
/// The file output is not redundant: when testing on a phone or emulator there is
/// no console to read a confirmation link from, and account confirmation is
/// required (SignIn.RequireConfirmedAccount = true).
/// </summary>
public sealed class DevEmailSender : IEmailSender<ApplicationUser>
{
    private readonly ILogger<DevEmailSender> _logger;
    private readonly string _outputDirectory;

    public DevEmailSender(ILogger<DevEmailSender> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _outputDirectory = Path.Combine(environment.ContentRootPath, "sent-emails");
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        WriteAsync(email, "Confirm your account", confirmationLink);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        WriteAsync(email, "Reset your password", resetLink);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        WriteAsync(email, "Your password reset code", resetCode);

    private async Task WriteAsync(string email, string subject, string body)
    {
        _logger.LogWarning("[DevEmailSender] To: {Email} | {Subject} | {Body}", email, subject, body);

        Directory.CreateDirectory(_outputDirectory);
        var safeEmail = string.Concat(email.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{safeEmail}.html";

        var html = $"""
            <html><body>
              <p><strong>To:</strong> {email}</p>
              <p><strong>From:</strong> {{EmailFromAddress}}</p>
              <p><strong>Subject:</strong> {subject}</p>
              <hr />
              <p>{body}</p>
            </body></html>
            """;

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, fileName), html);
    }
}
```

- [ ] **Step 4: Create the SMTP and SendGrid senders**

Create `Templates/WebPortal/Services/SmtpEmailSender.cs`:

```csharp
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using {{ProjectName}}.Server.Data;

namespace {{ProjectName}}.WebPortal.Services;

/// <summary>Sends account emails over SMTP. Reads credentials from the "Smtp" configuration section.</summary>
public sealed class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration) => _configuration = configuration;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your account",
            $"<p>Please confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.</p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password",
            $"<p>Reset your password by <a href=\"{resetLink}\">clicking here</a>.</p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Your password reset code", $"<p>Your reset code is: <strong>{resetCode}</strong></p>");

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var section = _configuration.GetSection("Smtp");
        using var client = new SmtpClient(section["Host"], int.Parse(section["Port"] ?? "587"))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(section["UserName"], section["Password"]),
        };

        using var message = new MailMessage("{{EmailFromAddress}}", to, subject, htmlBody) { IsBodyHtml = true };
        await client.SendMailAsync(message);
    }
}
```

Create `Templates/WebPortal/Services/SendGridEmailSender.cs` with the same three methods, posting to the SendGrid v3 API with `HttpClient` and reading the key from `configuration["SendGrid:ApiKey"]`:

```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using {{ProjectName}}.Server.Data;

namespace {{ProjectName}}.WebPortal.Services;

/// <summary>Sends account emails through the SendGrid v3 API.</summary>
public sealed class SendGridEmailSender : IEmailSender<ApplicationUser>
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public SendGridEmailSender(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your account",
            $"<p>Please confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.</p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password",
            $"<p>Reset your password by <a href=\"{resetLink}\">clicking here</a>.</p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Your password reset code", $"<p>Your reset code is: <strong>{resetCode}</strong></p>");

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send")
        {
            Content = JsonContent.Create(new
            {
                personalizations = new[] { new { to = new[] { new { email = to } } } },
                from = new { email = "{{EmailFromAddress}}" },
                subject,
                content = new[] { new { type = "text/html", value = htmlBody } },
            }),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _configuration["SendGrid:ApiKey"]);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
```

- [ ] **Step 5: Register the selected sender**

In `Templates/WebPortal/Program.cs`, replace:

```csharp
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
```

with:

```csharp
{{#if (eq EmailProvider "Dev")}}
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, DevEmailSender>();
{{/if}}
{{#if (eq EmailProvider "Smtp")}}
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();
{{/if}}
{{#if (eq EmailProvider "SendGrid")}}
builder.Services.AddHttpClient<IEmailSender<ApplicationUser>, SendGridEmailSender>();
{{/if}}
```

Add `using {{ProjectName}}.WebPortal.Services;` to the top of the file. Apply the same registration block to `Templates/WebAPI/Program.cs` — `MapIdentityApi`'s `/register` resolves the same abstraction, so a missing sender breaks MAUI signup too.

- [ ] **Step 6: Add the configuration properties**

In `src/VanillaStudio/Models/ProjectConfiguration.cs`, add next to `IncludeAuthentication`:

```csharp
public EmailProvider EmailProvider { get; set; } = EmailProvider.Dev;
public string EmailFromAddress { get; set; } = "noreply@localhost";
```

And a new enum beside `DatabaseProvider`:

```csharp
public enum EmailProvider
{
    [Display(Name = "Development (writes links to console and sent-emails/)")]
    Dev = 1,

    [Display(Name = "SMTP")]
    Smtp = 2,

    [Display(Name = "SendGrid")]
    SendGrid = 3
}
```

- [ ] **Step 7: Pass the parameters and add the wizard control**

Add to the `parameters` dictionary in `WebPortalProjectsGenerator.GenerateWebPortalProjectAsync` and to the WebAPI parameters in `PlatformProjectsGenerator`:

```csharp
["EmailProvider"] = config.EmailProvider.ToString(),
["EmailFromAddress"] = config.EmailFromAddress,
```

In `src/VanillaStudio/Components/Pages/ProjectWizard.razor`, inside the existing `@if (config.IncludeAuthentication)` block near line 591, add:

```razor
<div class="mb-3">
    <label for="emailProvider" class="form-label">Email Provider</label>
    <InputSelect id="emailProvider" class="form-select" @bind-Value="config.EmailProvider">
        <option value="@EmailProvider.Dev">Development (console + sent-emails/)</option>
        <option value="@EmailProvider.Smtp">SMTP</option>
        <option value="@EmailProvider.SendGrid">SendGrid</option>
    </InputSelect>
    <div class="form-text">
        Account confirmation is required. Development writes the confirmation link to the
        console and to sent-emails/ so you can complete signup without configuring a mail server.
    </div>
</div>
<div class="mb-3">
    <label for="emailFrom" class="form-label">From Address</label>
    <InputText id="emailFrom" class="form-control" @bind-Value="config.EmailFromAddress" />
</div>
```

- [ ] **Step 8: Run the tests to verify they pass**

```bash
dotnet build src/ZKnow.VanillaStudio.sln
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: build succeeds; **PASS**, all tests.

- [ ] **Step 9: Commit**

```bash
git add src/VanillaStudio tests/VanillaSlice.Tests
git commit -m "feat(studio): email provider selection with a working development sender"
```

---

## Task 8: MAUI token storage and base address

Spec §4.3 and §4.6. Removes the dead `TokenHandler` and fixes the emulator-unreachable base address.

**Files:**
- Delete: `Templates/HybridApp/Services/TokenHandler.cs`
- Modify: `Templates/HybridApp/MauiProgram.cs`
- Create: `Templates/ClientShared/Identity/TokenStorage.cs`, `Templates/ClientShared/Identity/HttpClientHelper.cs`

**Interfaces:**
- Consumes: `ILocalStorageService` from `Templates/FrameworkCore/Interfaces/ILocalStorageService.cs`.
- Produces:
  - `TokenStorage.GetAccessTokenAsync()` → `Task<string?>`
  - `TokenStorage.GetRefreshTokenAsync()` → `Task<string?>`
  - `TokenStorage.SaveAsync(string accessToken, string refreshToken, int expiresInSeconds)` → `Task`
  - `TokenStorage.ClearAsync()` → `Task`
  - `TokenStorage.IsExpiringSoonAsync()` → `Task<bool>`
  - `HttpClientHelper.ApiBaseAddress` → `string`

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Fact]
public void Dead_TokenHandler_is_gone_from_the_hybrid_app()
{
    var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
        ["UIFramework"] = "Bootstrap",
    });

    Assert.False(TemplateTestFixture.HasFile(files, "Services/TokenHandler.cs"));

    var mauiProgram = TemplateTestFixture.FileContent(files, "MauiProgram.cs");
    Assert.DoesNotContain("TokenHandler", mauiProgram);
}

[Fact]
public void Hybrid_base_address_is_reachable_from_an_android_emulator()
{
    var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
        ["UIFramework"] = "Bootstrap",
    });

    var mauiProgram = TemplateTestFixture.FileContent(files, "MauiProgram.cs");

    // localhost is unreachable from the Android emulator; it needs 10.0.2.2.
    Assert.Contains("HttpClientHelper", mauiProgram);
    Assert.DoesNotContain("new Uri(\"https://localhost:7202\")", mauiProgram);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~TokenHandler
```

Expected: **FAIL** — `TokenHandler.cs` exists and the base address is hardcoded.

- [ ] **Step 3: Create `HttpClientHelper`**

Create `Templates/ClientShared/Identity/HttpClientHelper.cs`:

```csharp
namespace {{ProjectName}}.ClientShared.Identity;

/// <summary>
/// Resolves the API base address per platform.
///
/// Android emulators route host loopback through 10.0.2.2 — "localhost" inside the
/// emulator is the emulated device itself, so a hardcoded localhost address fails at
/// login before any feature call is made.
/// </summary>
public static class HttpClientHelper
{
    private const string LocalhostAddress = "https://localhost:7202";
    private const string AndroidEmulatorAddress = "https://10.0.2.2:7202";

    /// <summary>Production API address. Replace with your deployed endpoint.</summary>
    private const string ProductionAddress = "https://localhost:7202";

    public static string ApiBaseAddress
    {
#if DEBUG
        get => DeviceInfo.Platform == DevicePlatform.Android ? AndroidEmulatorAddress : LocalhostAddress;
#else
        get => ProductionAddress;
#endif
    }

    /// <summary>Base path for the Identity endpoints mapped by MapIdentityApi on the API host.</summary>
    public const string IdentityBasePath = "/identity";
}
```

- [ ] **Step 4: Create `TokenStorage`**

Create `Templates/ClientShared/Identity/TokenStorage.cs`:

```csharp
using {{ProjectName}}.Framework;

namespace {{ProjectName}}.ClientShared.Identity;

/// <summary>
/// Access token, refresh token, and expiry, stored through ILocalStorageService
/// (SecureStorage on MAUI — platform Keychain / Android Keystore).
/// </summary>
public sealed class TokenStorage
{
    // Keys are passed explicitly. ILocalStorageService declares
    // [CallerMemberName] defaults, so an omitted key silently stores under the
    // calling method's name.
    private const string AccessTokenKey = "identity_access_token";
    private const string RefreshTokenKey = "identity_refresh_token";
    private const string ExpiresAtKey = "identity_expires_at";

    private readonly ILocalStorageService _storage;

    public TokenStorage(ILocalStorageService storage) => _storage = storage;

    public Task<string?> GetAccessTokenAsync() => _storage.GetValue(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync() => _storage.GetValue(RefreshTokenKey);

    public async Task SaveAsync(string accessToken, string refreshToken, int expiresInSeconds)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        await _storage.SetValue(accessToken, AccessTokenKey);
        await _storage.SetValue(refreshToken, RefreshTokenKey);
        await _storage.SetValue(expiresAt.ToUnixTimeSeconds().ToString(), ExpiresAtKey);
    }

    public async Task ClearAsync()
    {
        await _storage.RemoveValue(AccessTokenKey);
        await _storage.RemoveValue(RefreshTokenKey);
        await _storage.RemoveValue(ExpiresAtKey);
    }

    /// <summary>True when the access token expires within two minutes, or is already gone.</summary>
    public async Task<bool> IsExpiringSoonAsync()
    {
        var raw = await _storage.GetValue(ExpiresAtKey);
        if (!long.TryParse(raw, out var unixSeconds)) return true;

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return expiresAt - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(2);
    }
}
```

- [ ] **Step 5: Delete the dead handler and rewire `MauiProgram`**

```bash
rm src/VanillaStudio/Templates/HybridApp/Services/TokenHandler.cs
```

In `Templates/HybridApp/MauiProgram.cs`, remove `builder.Services.AddScoped<TokenHandler>();` and replace the `AddHttpClient` block:

```csharp
builder.Services.AddSingleton<TokenStorage>();
builder.Services.AddHttpClient<BaseHttpClient, HttpTokenClient>("ServerAPI", client =>
{
    client.BaseAddress = new Uri(HttpClientHelper.ApiBaseAddress);
});
```

Add `using {{ProjectName}}.ClientShared.Identity;` to the file. Apply the same `AddHttpClient` change to `Templates/MauiNativeApp/MauiProgram.cs`.

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 7: Commit**

```bash
git add src/VanillaStudio/Templates
git commit -m "feat(templates): secure token storage and emulator-aware API base address"
```

---

## Task 9: Identity client and refresh-on-401

Spec §4.4. `HttpTokenClient` currently repeats the same header block in four methods and has no refresh path.

**Files:**
- Modify: `Templates/ClientShared/Helpers/BaseHttpClient.cs`
- Create: `Templates/ClientShared/Identity/IdentityClient.cs`

**Interfaces:**
- Consumes: `TokenStorage`, `HttpClientHelper.IdentityBasePath` (Task 8).
- Produces:
  - `IdentityClient.RegisterAsync(string email, string password)` → `Task<IdentityResultDto>`
  - `IdentityClient.LoginAsync(string email, string password)` → `Task<IdentityResultDto>`
  - `IdentityClient.ForgotPasswordAsync(string email)` → `Task<IdentityResultDto>`
  - `IdentityClient.RefreshAsync()` → `Task<bool>`
  - `IdentityClient.LogoutAsync()` → `Task`
  - `IdentityClient.GetInfoAsync()` → `Task<UserInfoDto?>`
  - `record IdentityResultDto(bool Succeeded, string? Error)`
  - `record UserInfoDto(string Email, bool IsEmailConfirmed)`

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Fact]
public void Identity_client_targets_the_mapped_identity_endpoints()
{
    var files = TemplateTestFixture.Generate("ClientShared", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
    });

    var client = TemplateTestFixture.FileContent(files, "Identity/IdentityClient.cs");

    // Must match the group prefix mapped in Task 5.
    Assert.Contains("/identity/login", client);
    Assert.Contains("/identity/register", client);
    Assert.Contains("/identity/refresh", client);
    Assert.Contains("/identity/manage/info", client);
    // Bearer mode: useCookies must not be set from a native client.
    Assert.DoesNotContain("useCookies=true", client);
}

[Fact]
public void HttpTokenClient_refreshes_once_on_401_instead_of_failing()
{
    var files = TemplateTestFixture.Generate("ClientShared", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
    });

    var http = TemplateTestFixture.FileContent(files, "Helpers/BaseHttpClient.cs");

    Assert.Contains("SendWithRefreshAsync", http);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~Identity_client
```

Expected: **FAIL** — neither file has the required content.

- [ ] **Step 3: Create `IdentityClient`**

Create `Templates/ClientShared/Identity/IdentityClient.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;

namespace {{ProjectName}}.ClientShared.Identity;

public record IdentityResultDto(bool Succeeded, string? Error);
public record UserInfoDto(string Email, bool IsEmailConfirmed);

/// <summary>
/// Typed wrapper over the endpoints mapped by MapIdentityApi on the API host.
///
/// These are ASP.NET Core Identity's own opaque tokens, not JWTs. The login
/// endpoint returns tokens when useCookies is omitted, which is what native
/// clients need.
/// </summary>
public sealed class IdentityClient
{
    private const string Base = HttpClientHelper.IdentityBasePath;

    private readonly HttpClient _httpClient;
    private readonly TokenStorage _tokens;

    public IdentityClient(HttpClient httpClient, TokenStorage tokens)
    {
        _httpClient = httpClient;
        _tokens = tokens;
    }

    public async Task<IdentityResultDto> RegisterAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/register", new { email, password });
        return response.IsSuccessStatusCode
            ? new IdentityResultDto(true, null)
            : new IdentityResultDto(false, await DescribeFailureAsync(response));
    }

    public async Task<IdentityResultDto> LoginAsync(string email, string password)
    {
        // No useCookies parameter: bearer mode.
        var response = await _httpClient.PostAsJsonAsync($"{Base}/login", new { email, password });
        if (!response.IsSuccessStatusCode)
            return new IdentityResultDto(false, await DescribeFailureAsync(response));

        var payload = await response.Content.ReadFromJsonAsync<AccessTokenPayload>();
        if (payload is null)
            return new IdentityResultDto(false, "Login succeeded but no tokens were returned.");

        await _tokens.SaveAsync(payload.AccessToken, payload.RefreshToken, payload.ExpiresIn);
        return new IdentityResultDto(true, null);
    }

    public async Task<IdentityResultDto> ForgotPasswordAsync(string email)
    {
        var response = await _httpClient.PostAsJsonAsync($"{Base}/forgotPassword", new { email });
        return response.IsSuccessStatusCode
            ? new IdentityResultDto(true, null)
            : new IdentityResultDto(false, await DescribeFailureAsync(response));
    }

    /// <summary>Exchanges the stored refresh token for a new access token. False means re-login is required.</summary>
    public async Task<bool> RefreshAsync()
    {
        var refreshToken = await _tokens.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken)) return false;

        var response = await _httpClient.PostAsJsonAsync($"{Base}/refresh", new { refreshToken });
        if (!response.IsSuccessStatusCode)
        {
            await _tokens.ClearAsync();
            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<AccessTokenPayload>();
        if (payload is null) return false;

        await _tokens.SaveAsync(payload.AccessToken, payload.RefreshToken, payload.ExpiresIn);
        return true;
    }

    public Task LogoutAsync() => _tokens.ClearAsync();

    public async Task<UserInfoDto?> GetInfoAsync()
    {
        var accessToken = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken)) return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Base}/manage/info");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        return new UserInfoDto(
            root.GetProperty("email").GetString() ?? string.Empty,
            root.GetProperty("isEmailConfirmed").GetBoolean());
    }

    private static async Task<string> DescribeFailureAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return $"Request failed: {response.StatusCode}";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString() ?? body;
            if (document.RootElement.TryGetProperty("errors", out var errors))
                return string.Join(" ", errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(v => v.GetString())));
        }
        catch (JsonException)
        {
            // Fall through to the raw body.
        }

        return body;
    }

    private sealed record AccessTokenPayload(
        string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);
}
```

- [ ] **Step 4: Add refresh-on-401 to `HttpTokenClient`**

In `Templates/ClientShared/Helpers/BaseHttpClient.cs`, add a shared private helper to `HttpTokenClient` and route all four public methods through it. Replace the per-method header block with:

```csharp
/// <summary>
/// Attaches the bearer token, sends, and on a 401 refreshes once and retries.
/// Without this a expired access token surfaces as a login prompt mid-session.
/// </summary>
private async Task<HttpResponseMessage> SendWithRefreshAsync(
    Func<HttpRequestMessage> buildRequest)
{
    _ = _httpClient ?? throw new ArgumentNullException($"{nameof(_httpClient)} is null in {GetType().Name}");

    async Task<HttpResponseMessage> SendOnceAsync()
    {
        var request = buildRequest();
        var authToken = await localStorageService.GetValue("identity_access_token");
        if (!string.IsNullOrEmpty(authToken))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
        }
        return await _httpClient.SendAsync(request);
    }

    var response = await SendOnceAsync();
    if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

    if (!await identityClient.RefreshAsync())
    {
        throw new UnauthorizedAccessException("Please login to continue");
    }

    response.Dispose();
    return await SendOnceAsync();
}
```

Add an `IdentityClient identityClient` constructor parameter to `HttpTokenClient` and store it. Rewrite `GetFromJsonAsync`, `PostAsJsonAsync`, `PutAsJsonAsync`, and `DeleteAsync` to build an `HttpRequestMessage` and call `SendWithRefreshAsync`, keeping their existing response-parsing behaviour unchanged.

> The token key changes from `auth_token` to `identity_access_token` to match `TokenStorage` (Task 8). Both must agree or the header is never attached.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 6: Commit**

```bash
git add src/VanillaStudio/Templates/ClientShared
git commit -m "feat(templates): typed identity client with refresh-on-401"
```

---

## Task 10: MAUI authentication state provider

Spec §4.3.

**Files:**
- Create: `Templates/ClientShared/Identity/MauiAuthenticationStateProvider.cs`
- Modify: `Templates/HybridApp/MauiProgram.cs`, `Templates/MauiNativeApp/MauiProgram.cs`

**Interfaces:**
- Consumes: `IdentityClient` (Task 9), `TokenStorage` (Task 8).
- Produces:
  - `MauiAuthenticationStateProvider.LogInAsync(string email, string password)` → `Task<IdentityResultDto>`
  - `MauiAuthenticationStateProvider.LogOutAsync()` → `Task`
  - `MauiAuthenticationStateProvider.RegisterAsync(string email, string password)` → `Task<IdentityResultDto>`
  - Task 11 and Task 12 inject this type.

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Theory]
[InlineData("HybridApp")]
[InlineData("MauiNativeApp")]
public void Maui_hosts_register_authentication_services(string template)
{
    var files = TemplateTestFixture.Generate(template, new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
        ["UIFramework"] = "Bootstrap",
    });

    var mauiProgram = TemplateTestFixture.FileContent(files, "MauiProgram.cs");

    Assert.Contains("AddAuthorizationCore()", mauiProgram);
    Assert.Contains("MauiAuthenticationStateProvider", mauiProgram);
    Assert.Contains("AddScoped<AuthenticationStateProvider>", mauiProgram);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~Maui_hosts_register
```

Expected: **FAIL** — neither `MauiProgram.cs` registers authorization.

- [ ] **Step 3: Create the state provider**

Create `Templates/ClientShared/Identity/MauiAuthenticationStateProvider.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace {{ProjectName}}.ClientShared.Identity;

/// <summary>
/// Supplies AuthenticationState on MAUI hosts from tokens held in SecureStorage.
/// Claims are hydrated from GET /identity/manage/info, because Identity's tokens
/// are opaque and carry no readable claims of their own.
/// </summary>
public sealed class MauiAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly IdentityClient _identityClient;
    private readonly TokenStorage _tokens;

    public MauiAuthenticationStateProvider(IdentityClient identityClient, TokenStorage tokens)
    {
        _identityClient = identityClient;
        _tokens = tokens;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var accessToken = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken)) return Anonymous;

        if (await _tokens.IsExpiringSoonAsync() && !await _identityClient.RefreshAsync())
        {
            return Anonymous;
        }

        var info = await _identityClient.GetInfoAsync();
        if (info is null) return Anonymous;

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, info.Email), new Claim(ClaimTypes.Email, info.Email)],
            authenticationType: "Identity.Bearer");

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task<IdentityResultDto> LogInAsync(string email, string password)
    {
        var result = await _identityClient.LoginAsync(email, password);
        if (result.Succeeded)
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
        return result;
    }

    public Task<IdentityResultDto> RegisterAsync(string email, string password) =>
        _identityClient.RegisterAsync(email, password);

    public async Task LogOutAsync()
    {
        await _identityClient.LogoutAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }
}
```

- [ ] **Step 4: Register in both MAUI hosts**

Add to `Templates/HybridApp/MauiProgram.cs` and `Templates/MauiNativeApp/MauiProgram.cs`, before `return builder.Build();`:

```csharp
builder.Services.AddAuthorizationCore();
builder.Services.AddHttpClient<IdentityClient>(client =>
{
    client.BaseAddress = new Uri(HttpClientHelper.ApiBaseAddress);
});
builder.Services.AddScoped<MauiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(s =>
    (MauiAuthenticationStateProvider)s.GetRequiredService<MauiAuthenticationStateProvider>());
```

Add `using Microsoft.AspNetCore.Components.Authorization;` and `using {{ProjectName}}.ClientShared.Identity;`.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 6: Commit**

```bash
git add src/VanillaStudio/Templates
git commit -m "feat(templates): MAUI authentication state provider over Identity bearer tokens"
```

---

## Task 11: Hybrid identity screens

Spec §4.2 — four screens. Confirm-email and reset-password are reached by clicking a link in an email, which opens a browser; those land on WebPortal's scaffold pages from Task 6.

**Files:**
- Create: `Templates/RazorLibrary/Features/Account/Login.razor_`, `Register.razor_`, `Logout.razor_`, `ForgotPassword.razor_`

**Interfaces:**
- Consumes: `MauiAuthenticationStateProvider` (Task 10).
- Produces: routes `/account/login`, `/account/register`, `/account/logout`, `/account/forgot-password`. Task 13 redirects unauthenticated users to `/account/login`.

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Theory]
[InlineData("Features/Account/Login.razor")]
[InlineData("Features/Account/Register.razor")]
[InlineData("Features/Account/Logout.razor")]
[InlineData("Features/Account/ForgotPassword.razor")]
public void RazorLibrary_provides_the_shared_account_screens(string expectedPath)
{
    var files = TemplateTestFixture.Generate("RazorLibrary", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
        ["UIFramework"] = "Bootstrap",
    });

    Assert.True(TemplateTestFixture.HasFile(files, expectedPath));
}

[Fact]
public void Hybrid_login_screen_uses_the_maui_state_provider()
{
    var files = TemplateTestFixture.Generate("RazorLibrary", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
        ["UIFramework"] = "Bootstrap",
    });

    var login = TemplateTestFixture.FileContent(files, "Features/Account/Login.razor");
    Assert.Contains("MauiAuthenticationStateProvider", login);
    Assert.Contains("@page \"/account/login\"", login);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~account_screens
```

Expected: **FAIL** — `Templates/RazorLibrary/Features/Account/` does not exist.

- [ ] **Step 3: Create `Login.razor_`**

Create `Templates/RazorLibrary/Features/Account/Login.razor_`. Follow the UI-framework branching pattern already used in `Templates/RazorLibrary/Features/Products/ProductForm/ProductForm.razor_` — five sibling `{{#if (eq UIFramework "…")}}` blocks for the markup, one shared `@code` block. Bootstrap branch shown; repeat the markup for `FluentUI`, `MudBlazor`, `Radzen`, and `TailwindCSS` using each library's field and button components.

```razor
@page "/account/login"
@using Microsoft.AspNetCore.Components.Authorization
@using {{ProjectName}}.ClientShared.Identity
@inject MauiAuthenticationStateProvider AuthStateProvider
@inject NavigationManager Navigation

<PageTitle>Sign in</PageTitle>

{{#if (eq UIFramework "Bootstrap")}}
<div class="container" style="max-width: 26rem;">
    <h1 class="h3 mb-4">Sign in</h1>

    @if (!string.IsNullOrEmpty(error))
    {
        <div class="alert alert-danger" role="alert">@error</div>
    }

    <EditForm Model="this" OnValidSubmit="SignInAsync" FormName="login">
        <div class="mb-3">
            <label class="form-label" for="email">Email</label>
            <InputText id="email" class="form-control" @bind-Value="email" autocomplete="username" />
        </div>
        <div class="mb-3">
            <label class="form-label" for="password">Password</label>
            <InputText id="password" type="password" class="form-control"
                       @bind-Value="password" autocomplete="current-password" />
        </div>
        <button class="btn btn-primary w-100" type="submit" disabled="@busy">
            @(busy ? "Signing in…" : "Sign in")
        </button>
    </EditForm>

    <div class="mt-3 d-flex justify-content-between">
        <a href="/account/register">Create an account</a>
        <a href="/account/forgot-password">Forgot password?</a>
    </div>
</div>
{{/if}}

@code {
    private string email = string.Empty;
    private string password = string.Empty;
    private string? error;
    private bool busy;

    private async Task SignInAsync()
    {
        busy = true;
        error = null;

        var result = await AuthStateProvider.LogInAsync(email, password);

        busy = false;

        if (!result.Succeeded)
        {
            error = result.Error ?? "Sign in failed. Check your email and password.";
            return;
        }

        Navigation.NavigateTo("");
    }
}
```

- [ ] **Step 4: Create `Register.razor_`**

Same structure, routed at `/account/register`. On success it must **not** navigate home — account confirmation is required, so it shows a "check your email" state:

```razor
@page "/account/register"
@using {{ProjectName}}.ClientShared.Identity
@inject MauiAuthenticationStateProvider AuthStateProvider

<PageTitle>Create account</PageTitle>

{{#if (eq UIFramework "Bootstrap")}}
<div class="container" style="max-width: 26rem;">
    @if (registered)
    {
        <h1 class="h3 mb-3">Check your email</h1>
        <p>
            We sent a confirmation link to <strong>@email</strong>.
            Confirm your address, then <a href="/account/login">sign in</a>.
        </p>
    }
    else
    {
        <h1 class="h3 mb-4">Create account</h1>

        @if (!string.IsNullOrEmpty(error))
        {
            <div class="alert alert-danger" role="alert">@error</div>
        }

        <EditForm Model="this" OnValidSubmit="RegisterAsync" FormName="register">
            <div class="mb-3">
                <label class="form-label" for="email">Email</label>
                <InputText id="email" class="form-control" @bind-Value="email" autocomplete="username" />
            </div>
            <div class="mb-3">
                <label class="form-label" for="password">Password</label>
                <InputText id="password" type="password" class="form-control"
                           @bind-Value="password" autocomplete="new-password" />
            </div>
            <button class="btn btn-primary w-100" type="submit" disabled="@busy">
                @(busy ? "Creating…" : "Create account")
            </button>
        </EditForm>

        <div class="mt-3"><a href="/account/login">Already have an account?</a></div>
    }
</div>
{{/if}}

@code {
    private string email = string.Empty;
    private string password = string.Empty;
    private string? error;
    private bool busy;
    private bool registered;

    private async Task RegisterAsync()
    {
        busy = true;
        error = null;

        var result = await AuthStateProvider.RegisterAsync(email, password);

        busy = false;

        if (!result.Succeeded)
        {
            error = result.Error ?? "Registration failed.";
            return;
        }

        // Confirmation is required — do not sign in here.
        registered = true;
    }
}
```

- [ ] **Step 5: Create `Logout.razor_` and `ForgotPassword.razor_`**

`Logout.razor_`, routed at `/account/logout`:

```razor
@page "/account/logout"
@using {{ProjectName}}.ClientShared.Identity
@inject MauiAuthenticationStateProvider AuthStateProvider
@inject NavigationManager Navigation

<PageTitle>Signing out</PageTitle>

<p>Signing out…</p>

@code {
    protected override async Task OnInitializedAsync()
    {
        await AuthStateProvider.LogOutAsync();
        Navigation.NavigateTo("/account/login", replace: true);
    }
}
```

`ForgotPassword.razor_`, routed at `/account/forgot-password`. It requests the reset and then stops — the emailed link opens WebPortal's `/Account/ResetPassword` in a browser:

```razor
@page "/account/forgot-password"
@using {{ProjectName}}.ClientShared.Identity
@inject IdentityClient IdentityClient

<PageTitle>Reset password</PageTitle>

{{#if (eq UIFramework "Bootstrap")}}
<div class="container" style="max-width: 26rem;">
    @if (sent)
    {
        <h1 class="h3 mb-3">Check your email</h1>
        <p>
            If an account exists for <strong>@email</strong>, we sent a reset link.
            Opening it will complete the reset in your browser.
        </p>
    }
    else
    {
        <h1 class="h3 mb-4">Reset password</h1>

        <EditForm Model="this" OnValidSubmit="RequestAsync" FormName="forgot-password">
            <div class="mb-3">
                <label class="form-label" for="email">Email</label>
                <InputText id="email" class="form-control" @bind-Value="email" autocomplete="username" />
            </div>
            <button class="btn btn-primary w-100" type="submit" disabled="@busy">
                @(busy ? "Sending…" : "Send reset link")
            </button>
        </EditForm>

        <div class="mt-3"><a href="/account/login">Back to sign in</a></div>
    }
</div>
{{/if}}

@code {
    private string email = string.Empty;
    private bool busy;
    private bool sent;

    private async Task RequestAsync()
    {
        busy = true;
        await IdentityClient.ForgotPasswordAsync(email);
        busy = false;

        // Always report success — never reveal whether an account exists.
        sent = true;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 7: Commit**

```bash
git add src/VanillaStudio/Templates/RazorLibrary/Features/Account
git commit -m "feat(templates): shared Razor account screens for MAUI hybrid"
```

---

## Task 12: Native XAML identity screens

Spec §4.2, native equivalent. Same four flows, XAML over the same `MauiAuthenticationStateProvider`.

**Files:**
- Create: `Templates/MauiNativeApp/Features/Account/LoginPage.xaml`, `LoginPage.xaml.cs`, `RegisterPage.xaml`, `RegisterPage.xaml.cs`, `ForgotPasswordPage.xaml`, `ForgotPasswordPage.xaml.cs`
- Modify: `Templates/MauiNativeApp/MauiProgram.cs` — page registration

**Interfaces:**
- Consumes: `MauiAuthenticationStateProvider` (Task 10), `IdentityClient` (Task 9).
- Produces: Shell routes `login`, `register`, `forgot-password`. Task 13 navigates to `//login`.

> Logout is an action, not a page — it is a toolbar/flyout item that calls `LogOutAsync()` and navigates to `//login`. That is why this task creates three pages, not four.

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Theory]
[InlineData("Features/Account/LoginPage.xaml")]
[InlineData("Features/Account/LoginPage.xaml.cs")]
[InlineData("Features/Account/RegisterPage.xaml")]
[InlineData("Features/Account/ForgotPasswordPage.xaml")]
public void Native_app_provides_account_pages(string expectedPath)
{
    var files = TemplateTestFixture.Generate("MauiNativeApp", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
        ["UIFramework"] = "Bootstrap",
        ["MauiNavigationType"] = "Tabs",
    });

    Assert.True(TemplateTestFixture.HasFile(files, expectedPath));
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~Native_app_provides
```

Expected: **FAIL** — `Templates/MauiNativeApp/Features/Account/` does not exist.

- [ ] **Step 3: Create `LoginPage`**

Create `Templates/MauiNativeApp/Features/Account/LoginPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="{{ProjectName}}.NativeMauiApp.Features.Account.LoginPage"
             Title="Sign in"
             Shell.NavBarIsVisible="False">
    <ScrollView>
        <VerticalStackLayout Padding="24" Spacing="16" VerticalOptions="Center">
            <Label Text="Sign in" FontSize="28" FontAttributes="Bold" />

            <Label x:Name="ErrorLabel" TextColor="Red" IsVisible="False" />

            <Entry x:Name="EmailEntry" Placeholder="Email" Keyboard="Email" />
            <Entry x:Name="PasswordEntry" Placeholder="Password" IsPassword="True" />

            <Button x:Name="SignInButton" Text="Sign in" Clicked="OnSignInClicked" />

            <HorizontalStackLayout Spacing="16" HorizontalOptions="Center">
                <Button Text="Create account" Clicked="OnRegisterClicked" />
                <Button Text="Forgot password?" Clicked="OnForgotPasswordClicked" />
            </HorizontalStackLayout>
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

Create `Templates/MauiNativeApp/Features/Account/LoginPage.xaml.cs`:

```csharp
using {{ProjectName}}.ClientShared.Identity;

namespace {{ProjectName}}.NativeMauiApp.Features.Account;

public partial class LoginPage : ContentPage
{
    private readonly MauiAuthenticationStateProvider _authStateProvider;

    public LoginPage(MauiAuthenticationStateProvider authStateProvider)
    {
        InitializeComponent();
        _authStateProvider = authStateProvider;
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        SignInButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        var result = await _authStateProvider.LogInAsync(EmailEntry.Text ?? string.Empty,
                                                         PasswordEntry.Text ?? string.Empty);

        SignInButton.IsEnabled = true;

        if (!result.Succeeded)
        {
            ErrorLabel.Text = result.Error ?? "Sign in failed. Check your email and password.";
            ErrorLabel.IsVisible = true;
            return;
        }

        await Shell.Current.GoToAsync("//main");
    }

    private Task OnRegisterClickedAsync() => Shell.Current.GoToAsync("register");

    private async void OnRegisterClicked(object? sender, EventArgs e) => await OnRegisterClickedAsync();

    private async void OnForgotPasswordClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("forgot-password");
}
```

- [ ] **Step 4: Create `RegisterPage` and `ForgotPasswordPage`**

`RegisterPage.xaml` mirrors `LoginPage.xaml` with an `Entry` pair, a `Register` button, and a `ConfirmationLabel` that starts hidden. `RegisterPage.xaml.cs`:

```csharp
using {{ProjectName}}.ClientShared.Identity;

namespace {{ProjectName}}.NativeMauiApp.Features.Account;

public partial class RegisterPage : ContentPage
{
    private readonly MauiAuthenticationStateProvider _authStateProvider;

    public RegisterPage(MauiAuthenticationStateProvider authStateProvider)
    {
        InitializeComponent();
        _authStateProvider = authStateProvider;
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        RegisterButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        var result = await _authStateProvider.RegisterAsync(EmailEntry.Text ?? string.Empty,
                                                            PasswordEntry.Text ?? string.Empty);

        RegisterButton.IsEnabled = true;

        if (!result.Succeeded)
        {
            ErrorLabel.Text = result.Error ?? "Registration failed.";
            ErrorLabel.IsVisible = true;
            return;
        }

        // Confirmation is required; the link opens in a browser.
        FormLayout.IsVisible = false;
        ConfirmationLabel.Text =
            $"We sent a confirmation link to {EmailEntry.Text}. Confirm your address, then sign in.";
        ConfirmationLabel.IsVisible = true;
    }
}
```

`ForgotPasswordPage.xaml.cs` calls `IdentityClient.ForgotPasswordAsync` and always shows the same confirmation text, never revealing whether the account exists:

```csharp
using {{ProjectName}}.ClientShared.Identity;

namespace {{ProjectName}}.NativeMauiApp.Features.Account;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly IdentityClient _identityClient;

    public ForgotPasswordPage(IdentityClient identityClient)
    {
        InitializeComponent();
        _identityClient = identityClient;
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        SendButton.IsEnabled = false;
        await _identityClient.ForgotPasswordAsync(EmailEntry.Text ?? string.Empty);
        SendButton.IsEnabled = true;

        FormLayout.IsVisible = false;
        ConfirmationLabel.Text =
            $"If an account exists for {EmailEntry.Text}, we sent a reset link. " +
            "Opening it will complete the reset in your browser.";
        ConfirmationLabel.IsVisible = true;
    }
}
```

- [ ] **Step 5: Register the pages and routes**

In `Templates/MauiNativeApp/MauiProgram.cs`, before `return builder.Build();`:

```csharp
builder.Services.AddTransient<Features.Account.LoginPage>();
builder.Services.AddTransient<Features.Account.RegisterPage>();
builder.Services.AddTransient<Features.Account.ForgotPasswordPage>();
```

In both `Templates/MauiNativeApp/Views/AppShellTabs.xaml.cs` and `AppShellFlyout.xaml.cs`, register the routes in the constructor:

```csharp
Routing.RegisterRoute("login", typeof(Features.Account.LoginPage));
Routing.RegisterRoute("register", typeof(Features.Account.RegisterPage));
Routing.RegisterRoute("forgot-password", typeof(Features.Account.ForgotPasswordPage));
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 7: Commit**

```bash
git add src/VanillaStudio/Templates/MauiNativeApp
git commit -m "feat(templates): native XAML account pages for MAUI"
```

---

## Task 13: Route gating

Spec §4.5. Without this, signing out leaves protected screens reachable.

**Files:**
- Modify: `Templates/HybridApp/Components/Routes.razor_`
- Create: `Templates/HybridApp/Components/RedirectToLogin.razor_`
- Modify: `Templates/MauiNativeApp/Views/AppShellTabs.xaml.cs`, `AppShellFlyout.xaml.cs`
- Modify: `Templates/RazorLibrary/Features/Products/ProductListing/ProductListing.razor_`
- Verify only (no change): `Templates/WebPortalClient/Routes.razor_`

> **The web side is already gated.** `Templates/WebPortalClient/Routes.razor_` already uses `AuthorizeRouteView` with a `<NotAuthorized><RedirectToLogin /></NotAuthorized>` fallback, and `Templates/WebPortal/Components/_Imports.razor_` has `@using {{ProjectName}}.WebPortal.Client`, so the `<Routes />` element in `App.razor_` resolves to that component. Confirm it, change nothing.
>
> **Adjacent pre-existing gap — out of scope, flag it.** `WebPortalProjectsGenerator` only generates the `WebPortal.Client` project when `RenderingMode == Auto`. Under `ServerOnly` or `StaticSSR` there is no `Routes` component at all, yet `App.razor_` still renders `<Routes />`. That breaks those rendering modes independently of identity. Do not fix it here — open an issue referencing this note, and restrict Task 14's smoke test to `RenderingMode.Auto`.

**Interfaces:**
- Consumes: `Login.razor_` at `/account/login` (Task 11); native `login` route (Task 12).
- Produces: unauthenticated users are redirected to sign-in on every host.

- [ ] **Step 1: Write the failing test**

Append to `IdentityGenerationTests`:

```csharp
[Fact]
public void Hybrid_router_sends_unauthenticated_users_to_sign_in()
{
    var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
        ["UIFramework"] = "Bootstrap",
    });

    var routes = TemplateTestFixture.FileContent(files, "Components/Routes.razor");

    Assert.Contains("AuthorizeRouteView", routes);
    Assert.Contains("NotAuthorized", routes);
}

[Fact]
public void Sample_listing_requires_authentication_when_auth_is_enabled()
{
    var files = TemplateTestFixture.Generate("RazorLibrary", new Dictionary<string, object>
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
        ["UIFramework"] = "Bootstrap",
        ["IncludeAuthentication"] = true,
    });

    var listing = TemplateTestFixture.FileContent(files, "ProductListing.razor");
    Assert.Contains("@attribute [Authorize]", listing);
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter FullyQualifiedName~unauthenticated
```

Expected: **FAIL** — no `AuthorizeRouteView` and no `[Authorize]` attribute.

- [ ] **Step 3: Gate the Blazor routers**

Only the Hybrid app needs this change. Replace the whole of `Templates/HybridApp/Components/Routes.razor_`, which currently uses a plain `RouteView`:

```razor
<Router AppAssembly="@typeof(MauiProgram).Assembly" AdditionalAssemblies="@([typeof({{ProjectName}}.Razor._Imports).Assembly])">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(Layout.MainLayout)">
            <Authorizing>
                <p>Authorizing…</p>
            </Authorizing>
            <NotAuthorized>
                <RedirectToLogin />
            </NotAuthorized>
        </AuthorizeRouteView>
    </Found>
</Router>
```

Create `Templates/HybridApp/Components/RedirectToLogin.razor_` alongside it. It navigates to the Hybrid route from Task 11, **not** the web scaffold's `/Account/Login`:

```razor
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized() =>
        Navigation.NavigateTo("/account/login", replace: true);
}
```

Follow the shape of the existing `Templates/WebPortalClient/RedirectToLogin.razor_`.

- [ ] **Step 4: Gate the sample slice**

In `Templates/RazorLibrary/Features/Products/ProductListing/ProductListing.razor_`, add below the `@page` directive:

```razor
{{#if (eq IncludeAuthentication "True")}}
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]
{{/if}}
```

> The conditional compares against the string `"True"` — `ProcessConditionalBlocks` calls `.ToString()` on the boolean parameter and compares case-insensitively.

Ensure `IncludeAuthentication` is present in the `RazorLibrary` parameter dictionary in `CommonProjectGenerator` / `TemplateBasedCommonGenerator`; add it if missing.

- [ ] **Step 5: Gate native navigation**

In `AppShellTabs.xaml.cs` and `AppShellFlyout.xaml.cs`, after `InitializeComponent()`, add:

```csharp
protected override async void OnAppearing()
{
    base.OnAppearing();

    var state = await _authStateProvider.GetAuthenticationStateAsync();
    if (state.User.Identity?.IsAuthenticated != true)
    {
        await Shell.Current.GoToAsync("//login");
    }
}
```

Inject `MauiAuthenticationStateProvider` through the shell's constructor.

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj
```

Expected: **PASS**, all tests.

- [ ] **Step 7: Commit**

```bash
git add src/VanillaStudio/Templates
git commit -m "feat(templates): gate routes behind authentication on all hosts"
```

---

## Task 14: End-to-end smoke test and README correction

Spec §7. This plan exists because a status matrix asserted a capability the output lacked. This task is what stops that recurring.

**Files:**
- Create: `tests/VanillaSlice.Tests/GeneratedProjectSmokeTests.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: everything above.
- Produces: a test that fails if a generated project cannot complete the identity round trip.

- [ ] **Step 1: Write the failing test**

Create `tests/VanillaSlice.Tests/GeneratedProjectSmokeTests.cs`:

```csharp
using System.Diagnostics;
using Xunit;
using ZKnow.VanillaStudio.Models;

namespace VanillaSlice.Tests;

/// <summary>
/// Generates a real project and builds it. Guards the claim that the wizard's
/// "Authentication ✅" actually produces a compiling, runnable application.
///
/// SQLite and .NET 10 are deliberate: no external database server is required,
/// and no .NET 9 runtime is assumed to be installed.
/// </summary>
[Trait("Category", "Smoke")]
public class GeneratedProjectSmokeTests
{
    [Fact]
    public async Task Generated_project_with_identity_builds()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"vs-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var config = new ProjectConfiguration
            {
                ProjectName = "SmokeTest",
                RootNamespace = "SmokeTest",
                OutputDirectory = outputDirectory,
                DotNetVersion = DotNetVersion.Net10,
                DatabaseProvider = DatabaseProvider.SQLite,
                UIFramework = UIFramework.Bootstrap,
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

            var (exitCode, output) = await RunAsync(
                "dotnet",
                $"build \"{Path.Combine(outputDirectory, "SmokeTest.sln")}\" -warnaserror:CS0246",
                outputDirectory);

            Assert.True(exitCode == 0, $"Generated project failed to build:{Environment.NewLine}{output}");
        }
        finally
        {
            try { Directory.Delete(outputDirectory, recursive: true); } catch { /* best effort */ }
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout + Environment.NewLine + stderr);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter Category=Smoke
```

Expected: **compile error** — `GeneratedProjectHarness` does not exist.

- [ ] **Step 3: Add the generation harness**

Add to `tests/VanillaSlice.Tests/TemplateTestFixture.cs`:

```csharp
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
}
```

Implement `BuildGenerationService()` by constructing `EnhancedProjectGenerationService` with `NullLogger` instances and a `TemplateEngineService` pointed at `TemplateTestFixture.TemplatesPath`. Match the constructor signature in `src/VanillaStudio/Services/EnhancedProjectGenerationService.cs`.

- [ ] **Step 4: Run the smoke test**

```bash
dotnet test tests/VanillaSlice.Tests/VanillaSlice.Tests.csproj --filter Category=Smoke
```

Expected: **PASS**. If it fails, the failure output names the compile error in the generated project — fix the template, not the test.

- [ ] **Step 5: Run the manual round trip**

Generate a project through the wizard with SQLite + Identity, then:

1. `dotnet run` the WebPortal. Navigate to `/Account/Register` and register.
2. Open the newest file in the WebPortal's `sent-emails/` folder; click the confirmation link.
3. Sign in at `/Account/Login`. Confirm a protected page renders.
4. `dotnet run` the WebAPI. `POST /identity/login` with the same credentials; confirm an `accessToken` and `refreshToken` come back.
5. Call a slice controller endpoint with `Authorization: Bearer <accessToken>`; confirm `200`.
6. Call it without the header; confirm `401`.
7. `POST /identity/refresh` with the refresh token; confirm new tokens.

Record the result in the PR description.

- [ ] **Step 6: Correct the README**

In `README.md`, under **Authentication & Security**, replace:

```markdown
- **JWT Token Support** - ✅ **Fully Implemented**
```

with:

```markdown
- **Bearer Token Authentication** - ✅ **Fully Implemented** — ASP.NET Core Identity tokens via `MapIdentityApi` (opaque Identity tokens, not JWTs)
```

In the Implementation Status Matrix, update the Authentication row:

```markdown
| Authentication | ✅ **Complete** | Identity across Web, Hybrid, and Native — register, confirm, sign in, refresh, sign out |
```

Add to **Advanced Features**:

```markdown
| Two-Factor Authentication | ✅ **Complete** | Web only — TOTP with QR and recovery codes |
| External Logins | 🔄 **In Progress** | Web scaffold present; provider credentials not yet wizard-configurable |
```

- [ ] **Step 7: Commit**

```bash
git add tests/VanillaSlice.Tests README.md
git commit -m "test(studio): end-to-end smoke test for generated identity; correct README claims"
```

---

## Self-Review

**Spec coverage**

| Spec section | Task |
|---|---|
| §3.1 WebPortal `Program.cs` | 4 (pipeline), 7 (email sender), 3 (provider) |
| §3.2 WebAPI `Program.cs` | 5 |
| §3.3 `ApplicationUser` unchanged | — (no task needed; explicitly out of scope) |
| §3.4 Database provider fix | 3 |
| §3.5 Deletions (`TokenHandler`) | 8 |
| §4.1 Copy the web scaffold | 6 |
| §4.2 MAUI four screens | 11 (Hybrid), 12 (Native) |
| §4.3 Shared MAUI services | 8 (`TokenStorage`, `HttpClientHelper`), 10 (state provider) |
| §4.4 Token refresh | 9 |
| §4.5 Route gating | 13 |
| §4.6 `ILocalStorageService` hardening | 8 (explicit keys as constants) |
| §5 Email | 7 |
| §6 Wizard and configuration | 7 |
| §7 Verification + README | 14 |
| §8 Known limitations | — (documentation only) |
| §9 Feeding phase B | Recorded per-task in commit messages; no separate task |

**Gap found and closed during review:** the spec's §4.1 assumed the Account templates could simply be dropped in, but `TemplateEngineService` has no file-level exclusion — they would be emitted even with authentication disabled. Task 2 was added ahead of Task 6 to close this. Task 1 was added because no mechanism existed to assert on generated output at all.

**Type consistency**

- `TokenStorage` key `identity_access_token` is used identically in Task 8 (definition) and Task 9 (`HttpTokenClient.SendWithRefreshAsync`). The pre-existing `auth_token` key is replaced, not left alongside.
- `HttpClientHelper.IdentityBasePath` = `"/identity"` matches `app.MapGroup("/identity")` in Task 5 and every literal in Task 9.
- `IdentityResultDto(bool Succeeded, string? Error)` is returned by `IdentityClient` (Task 9) and re-exposed unchanged by `MauiAuthenticationStateProvider` (Task 10), consumed in Tasks 11 and 12 as `result.Succeeded` / `result.Error`.
- `MauiAuthenticationStateProvider.RegisterAsync` is defined in Task 10 and called in Tasks 11 and 12.
- `ProcessConditionalBlocks` compares `parameter.ToString()` case-insensitively, so boolean parameters are matched as `"True"` — used consistently in Task 13.

**Known ordering consequence:** between Task 4 and Task 6 the *generated* project references `MapAdditionalIdentityEndpoints()` before the extension method exists. This is called out inline in Task 4. Task 14's smoke test is the first gate that would catch it, and by then Task 6 has landed.
