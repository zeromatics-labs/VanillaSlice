using Xunit;
using ZKnow.VanillaStudio.Models;
using ZKnow.VanillaStudio.Services;

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
}
