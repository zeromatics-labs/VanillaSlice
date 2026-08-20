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
    [InlineData("SQLite", "Data Source=Acme.db")]
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
    [InlineData("SQLite", "Data Source=Acme.db")]
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
}
