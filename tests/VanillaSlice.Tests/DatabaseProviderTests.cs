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
