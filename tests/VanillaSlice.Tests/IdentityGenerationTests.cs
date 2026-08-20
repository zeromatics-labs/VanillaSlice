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
