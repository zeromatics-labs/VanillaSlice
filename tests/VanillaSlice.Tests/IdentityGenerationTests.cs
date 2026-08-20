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

    [Fact]
    public void WebPortal_Program_has_a_complete_auth_pipeline()
    {
        var files = TemplateTestFixture.Generate("WebPortal", WebPortalParams());
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains("app.UseAuthentication();", program);
        Assert.Contains("app.UseAuthorization();", program);
        Assert.Contains("app.MapAdditionalIdentityEndpoints();", program);
        Assert.DoesNotContain("//app.MapAdditionalIdentityEndpoints();", program);

        // Verify complete middleware chain: Authentication → Authorization → Antiforgery.
        var authN = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var authZ = program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);
        var anti  = program.IndexOf("app.UseAntiforgery();", StringComparison.Ordinal);

        Assert.True(authN >= 0 && authZ >= 0 && anti >= 0,
            "all three middleware calls must be present");
        Assert.True(authN < authZ,
            "UseAuthentication must precede UseAuthorization");
        Assert.True(authZ < anti,
            "UseAuthorization must precede UseAntiforgery");
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

    internal static Dictionary<string, object> WebApiParams() => new()
    {
        ["ProjectName"] = "Acme",
        ["RootNamespace"] = "Acme.WebAPI",
        ["TargetFramework"] = "net10.0",
        ["AspNetCoreVersion"] = "10.0.0",
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
}
