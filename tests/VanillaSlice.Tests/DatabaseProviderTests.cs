using Xunit;

namespace VanillaSlice.Tests;

public class DatabaseProviderTests
{
    private static Dictionary<string, object> WebApiParams(string databaseProvider = "SqlServer") => new()
    {
        ["ProjectName"] = "Acme",
        ["RootNamespace"] = "Acme.WebAPI",
        ["TargetFramework"] = "net10.0",
        ["AspNetCoreVersion"] = "10.0.0",
        ["NpgsqlVersion"] = "10.0.0",
        ["DatabaseProvider"] = databaseProvider,
    };

    private static Dictionary<string, object> ServerDataParams(string databaseProvider = "SqlServer") => new()
    {
        ["ProjectName"] = "Acme",
        ["RootNamespace"] = "Acme.Server.Data",
        ["TargetFramework"] = "net10.0",
        ["AspNetCoreVersion"] = "10.0.0",
        ["NpgsqlVersion"] = "10.0.0",
        ["IncludeAuthentication"] = true,
        ["IncludeSampleData"] = true,
        ["DatabaseProvider"] = databaseProvider,
    };

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

    // C-2 regression: Npgsql does not track ASP.NET Core patch numbers, so the PostgreSQL
    // package must be versioned independently ({{NpgsqlVersion}}), not from {{AspNetCoreVersion}}.
    // 9.0.8 (the AspNetCoreVersion for net9.0) was never published for Npgsql and fails restore.
    [Theory]
    [InlineData("WebPortal", ".WebPortal.csproj")]
    [InlineData("WebAPI", ".WebAPI.csproj")]
    [InlineData("ServerData", ".Server.Data.csproj")]
    public void PostgreSQL_csproj_uses_a_plausible_Npgsql_version_and_not_the_raw_placeholder(
        string templateName, string csprojSuffix)
    {
        var parameters = templateName switch
        {
            "WebPortal" => IdentityGenerationTests.WebPortalParams(databaseProvider: "PostgreSQL"),
            "WebAPI" => WebApiParams(databaseProvider: "PostgreSQL"),
            _ => ServerDataParams(databaseProvider: "PostgreSQL"),
        };

        var files = TemplateTestFixture.Generate(templateName, parameters);
        var csproj = TemplateTestFixture.FileContent(files, csprojSuffix);

        Assert.DoesNotContain("{{NpgsqlVersion}}", csproj);
        Assert.Matches(
            "Npgsql\\.EntityFrameworkCore\\.PostgreSQL\" Version=\"\\d+\\.\\d+\\.\\d+\"",
            csproj);
    }

    [Fact]
    public void WebPortal_Program_emits_no_provider_call_when_DatabaseProvider_is_None()
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: "None"));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.DoesNotContain("UseSqlServer", program);
        Assert.DoesNotContain("UseNpgsql", program);
        Assert.DoesNotContain("UseSqlite", program);
    }

    [Fact]
    public void WebPortal_csproj_references_no_EF_provider_when_DatabaseProvider_is_None()
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: "None"));
        var csproj = TemplateTestFixture.FileContent(files, ".WebPortal.csproj");

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.SqlServer", csproj);
        Assert.DoesNotContain("Npgsql.EntityFrameworkCore.PostgreSQL", csproj);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.Sqlite", csproj);
    }

    [Theory]
    [InlineData("SqlServer", "UseSqlServer")]
    [InlineData("PostgreSQL", "UseNpgsql")]
    [InlineData("SQLite", "UseSqlite")]
    public void WebAPI_Program_uses_the_selected_provider(string provider, string expectedCall)
    {
        var files = TemplateTestFixture.Generate("WebAPI", WebApiParams(databaseProvider: provider));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains(expectedCall, program);
    }

    [Theory]
    [InlineData("SqlServer", "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("PostgreSQL", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("SQLite", "Microsoft.EntityFrameworkCore.Sqlite")]
    public void WebAPI_csproj_references_the_matching_EF_provider(string provider, string package)
    {
        var files = TemplateTestFixture.Generate("WebAPI", WebApiParams(databaseProvider: provider));
        var csproj = TemplateTestFixture.FileContent(files, ".WebAPI.csproj");

        Assert.Contains(package, csproj);
    }

    [Fact]
    public void WebAPI_Program_emits_no_provider_call_when_DatabaseProvider_is_None()
    {
        var files = TemplateTestFixture.Generate("WebAPI", WebApiParams(databaseProvider: "None"));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.DoesNotContain("UseSqlServer", program);
        Assert.DoesNotContain("UseNpgsql", program);
        Assert.DoesNotContain("UseSqlite", program);
    }

    [Theory]
    [InlineData("SqlServer", "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("PostgreSQL", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("SQLite", "Microsoft.EntityFrameworkCore.Sqlite")]
    public void ServerData_csproj_references_the_matching_EF_provider(string provider, string package)
    {
        var files = TemplateTestFixture.Generate("ServerData", ServerDataParams(databaseProvider: provider));
        var csproj = TemplateTestFixture.FileContent(files, ".Server.Data.csproj");

        Assert.Contains(package, csproj);
    }

    [Fact]
    public void ServerData_csproj_references_no_EF_provider_when_DatabaseProvider_is_None()
    {
        var files = TemplateTestFixture.Generate("ServerData", ServerDataParams(databaseProvider: "None"));
        var csproj = TemplateTestFixture.FileContent(files, ".Server.Data.csproj");

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.SqlServer", csproj);
        Assert.DoesNotContain("Npgsql.EntityFrameworkCore.PostgreSQL", csproj);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore.Sqlite", csproj);
    }

    [Theory]
    [InlineData("SqlServer", "Server=(local);Database=Acme;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;")]
    [InlineData("PostgreSQL", "Host=localhost;Database=Acme;Username=postgres;Password=postgres")]
    [InlineData("SQLite", "Data Source=%LOCALAPPDATA%/Acme.db")]
    public void WebPortal_appsettings_connection_string_matches_the_selected_provider(string provider, string expectedConnectionString)
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: provider));
        var appsettings = TemplateTestFixture.FileContent(files, "appsettings.Development.json");

        Assert.Contains(expectedConnectionString, appsettings);
        using var _ = System.Text.Json.JsonDocument.Parse(appsettings);
    }

    [Theory]
    [InlineData("SqlServer", "Server=(local);Database=Acme;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;")]
    [InlineData("PostgreSQL", "Host=localhost;Database=Acme;Username=postgres;Password=postgres")]
    [InlineData("SQLite", "Data Source=%LOCALAPPDATA%/Acme.db")]
    public void WebAPI_appsettings_connection_string_matches_the_selected_provider(string provider, string expectedConnectionString)
    {
        var files = TemplateTestFixture.Generate("WebAPI", WebApiParams(databaseProvider: provider));
        var appsettings = TemplateTestFixture.FileContent(files, "appsettings.Development.json");

        Assert.Contains(expectedConnectionString, appsettings);
        using var _ = System.Text.Json.JsonDocument.Parse(appsettings);
    }

    [Fact]
    public void WebPortal_appsettings_is_valid_json_with_empty_connection_strings_when_DatabaseProvider_is_None()
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: "None"));
        var appsettings = TemplateTestFixture.FileContent(files, "appsettings.Development.json");

        Assert.DoesNotContain("DefaultConnection", appsettings);
        using var _ = System.Text.Json.JsonDocument.Parse(appsettings);
    }

    // Finding 1: on SQLite, WebPortal and WebAPI must share ONE database file. A bare relative
    // filename resolves against each host's own working directory (they differ — WebPortal's
    // csproj is nested two folders deep, WebAPI's is one), so both hosts need an identical,
    // CWD-independent connection string rooted at the user profile instead.
    [Fact]
    public void WebPortal_and_WebAPI_render_the_identical_SQLite_connection_string()
    {
        var webPortalFiles = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: "SQLite"));
        var webApiFiles = TemplateTestFixture.Generate("WebAPI", WebApiParams(databaseProvider: "SQLite"));

        var webPortalAppsettings = TemplateTestFixture.FileContent(webPortalFiles, "appsettings.Development.json");
        var webApiAppsettings = TemplateTestFixture.FileContent(webApiFiles, "appsettings.Development.json");

        using var webPortalDoc = System.Text.Json.JsonDocument.Parse(webPortalAppsettings);
        using var webApiDoc = System.Text.Json.JsonDocument.Parse(webApiAppsettings);

        var webPortalConnection = webPortalDoc.RootElement
            .GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        var webApiConnection = webApiDoc.RootElement
            .GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();

        Assert.Equal(webPortalConnection, webApiConnection);
        // Rooted at the user profile, not a bare filename resolved against each host's CWD.
        Assert.StartsWith("Data Source=%LOCALAPPDATA%/", webPortalConnection);
    }

    // Finding 1: whichever host starts first must create the shared schema, so both hosts need
    // the same Development-only Migrate()/EnsureCreated() split WebPortal already had.
    [Theory]
    [InlineData("SqlServer", "dbContext.Database.Migrate();")]
    [InlineData("PostgreSQL", "dbContext.Database.EnsureCreated();")]
    [InlineData("SQLite", "dbContext.Database.EnsureCreated();")]
    public void WebPortal_Program_creates_schema_in_Development_for_the_selected_provider(string provider, string expectedCall)
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: provider));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains(expectedCall, program);
    }

    [Theory]
    [InlineData("SqlServer", "dbContext.Database.Migrate();")]
    [InlineData("PostgreSQL", "dbContext.Database.EnsureCreated();")]
    [InlineData("SQLite", "dbContext.Database.EnsureCreated();")]
    public void WebAPI_Program_creates_schema_in_Development_for_the_selected_provider(string provider, string expectedCall)
    {
        var files = TemplateTestFixture.Generate("WebAPI", WebApiParams(databaseProvider: provider));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains(expectedCall, program);
    }

    [Fact]
    public void WebAPI_Program_does_not_call_Migrate_when_provider_is_not_SqlServer()
    {
        var files = TemplateTestFixture.Generate("WebAPI", WebApiParams(databaseProvider: "SQLite"));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.DoesNotContain("dbContext.Database.Migrate();", program);
    }

    // Regression: Environment.ExpandEnvironmentVariables leaves "%LOCALAPPDATA%" untouched on
    // Linux/macOS (it's a Windows-only env var), which broke SQLite outright on non-Windows
    // rather than just re-splitting the database file. Both hosts must instead call the shared,
    // cross-platform SqliteConnectionStringResolver — never ExpandEnvironmentVariables directly.
    [Fact]
    public void WebPortal_Program_resolves_SQLite_connection_via_the_shared_resolver_not_EnvironmentExpand()
    {
        var files = TemplateTestFixture.Generate(
            "WebPortal", IdentityGenerationTests.WebPortalParams(databaseProvider: "SQLite"));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains("SqliteConnectionStringResolver.Resolve(connectionString)", program);
        Assert.DoesNotContain("ExpandEnvironmentVariables", program);
    }

    [Fact]
    public void WebAPI_Program_resolves_SQLite_connection_via_the_shared_resolver_not_EnvironmentExpand()
    {
        var files = TemplateTestFixture.Generate("WebAPI", WebApiParams(databaseProvider: "SQLite"));
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains("SqliteConnectionStringResolver.Resolve(connectionString)", program);
        Assert.DoesNotContain("ExpandEnvironmentVariables", program);
    }

    // The resolver both hosts share must use the cross-platform SpecialFolder API — not
    // ExpandEnvironmentVariables, which silently no-ops on Linux/macOS — and must create the
    // resolved directory itself, since SQLite will not create a missing parent folder.
    [Fact]
    public void ServerData_SqliteConnectionStringResolver_uses_the_cross_platform_special_folder_API()
    {
        var files = TemplateTestFixture.Generate("ServerData", ServerDataParams(databaseProvider: "SQLite"));
        var resolver = TemplateTestFixture.FileContent(files, "SqliteConnectionStringResolver.cs");

        Assert.Contains("Environment.SpecialFolder.LocalApplicationData", resolver);
        Assert.Contains("Directory.CreateDirectory", resolver);
        Assert.DoesNotContain("ExpandEnvironmentVariables", resolver);
    }
}
