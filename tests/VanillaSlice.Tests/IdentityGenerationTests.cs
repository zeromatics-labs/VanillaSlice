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
        ["EmailProvider"] = "Dev",
        ["EmailFromAddress"] = "noreply@localhost",
    };

    /// <summary>
    /// The email sender classes live in the ServerData template (Ruling O: WebAPI does not
    /// reference WebPortal, so a sender referenced by both hosts must live in the project both
    /// of them share — ServerData), not in WebPortal like the original task brief.
    /// </summary>
    internal static Dictionary<string, object> ServerDataParams(
        string emailProvider = "Dev",
        string emailFromAddress = "noreply@localhost") => new()
    {
        ["ProjectName"] = "Acme",
        ["RootNamespace"] = "Acme.Server.Data",
        ["TargetFramework"] = "net10.0",
        ["AspNetCoreVersion"] = "10.0.0",
        ["IncludeAuthentication"] = true,
        ["IncludeSampleData"] = true,
        ["DatabaseProvider"] = "SqlServer",
        ["EmailProvider"] = emailProvider,
        ["EmailFromAddress"] = emailFromAddress,
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
        ["EmailProvider"] = "Dev",
        ["EmailFromAddress"] = "noreply@localhost",
    };

    [Fact]
    public void WebAPI_Program_exposes_identity_endpoints_for_bearer_clients()
    {
        var files = TemplateTestFixture.Generate("WebAPI", WebApiParams());
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains("AddIdentityApiEndpoints<ApplicationUser>(", program);
        Assert.Contains("options.SignIn.RequireConfirmedAccount = true;", program);
        Assert.Contains("AddEntityFrameworkStores<AppDbContext>()", program);
        Assert.Contains("app.UseAuthentication();", program);
        Assert.Contains("app.UseAuthorization();", program);
        Assert.Contains("app.MapGroup(\"/identity\").MapIdentityApi<ApplicationUser>();", program);
    }

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
    [InlineData("Components/Account/IdentityNoOpEmailSender.cs")]
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

    // The senders themselves live in the ServerData template (shared by WebPortal and WebAPI),
    // not WebPortal — see Ruling O. WebAPI does not reference WebPortal, so a sender that both
    // hosts register must live in a project both of them reference.
    [Fact]
    public void Dev_email_sender_is_generated_and_registered_by_default()
    {
        var serverDataFiles = TemplateTestFixture.Generate("ServerData", ServerDataParams());
        Assert.True(TemplateTestFixture.HasFile(serverDataFiles, "Services/DevEmailSender.cs"));

        var p = WebPortalParams();
        p["EmailProvider"] = "Dev";
        p["EmailFromAddress"] = "noreply@localhost";

        var files = TemplateTestFixture.Generate("WebPortal", p);
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains("IEmailSender<ApplicationUser>, DevEmailSender", program);
        Assert.DoesNotContain("IdentityNoOpEmailSender", program);
    }

    [Fact]
    public void Dev_email_sender_writes_links_to_disk_not_just_the_logger()
    {
        var serverDataFiles = TemplateTestFixture.Generate("ServerData", ServerDataParams());
        var sender = TemplateTestFixture.FileContent(serverDataFiles, "Services/DevEmailSender.cs");

        // A phone or emulator has no console to read a confirmation link from.
        Assert.Contains("sent-emails", sender);
    }

    [Fact]
    public void Dev_email_sender_logs_the_absolute_path_it_wrote_to()
    {
        // A developer testing on a phone or emulator has no console — but when they do have
        // one, the log line must say exactly where the file went, at a level that survives
        // default log filtering.
        var serverDataFiles = TemplateTestFixture.Generate("ServerData", ServerDataParams());
        var sender = TemplateTestFixture.FileContent(serverDataFiles, "Services/DevEmailSender.cs");

        Assert.Contains("LogWarning", sender);
        Assert.Contains("FilePath", sender);
    }

    [Fact]
    public void Dev_email_sender_does_not_depend_on_hosting_types()
    {
        // ServerData is a plain Microsoft.NET.Sdk library, not Sdk.Web, so IWebHostEnvironment
        // is not available to it.
        var serverDataFiles = TemplateTestFixture.Generate("ServerData", ServerDataParams());
        var sender = TemplateTestFixture.FileContent(serverDataFiles, "Services/DevEmailSender.cs");

        Assert.DoesNotContain("IWebHostEnvironment", sender);
        Assert.Contains("AppContext.BaseDirectory", sender);
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

    [Fact]
    public void WebAPI_also_registers_an_email_sender_for_MapIdentityApi_register()
    {
        // WebAPI's Program.cs registered no IEmailSender at all before this task, so
        // MapIdentityApi's /register endpoint (which resolves IEmailSender<ApplicationUser>)
        // failed to start — breaking MAUI signup.
        var p = WebApiParams();
        p["EmailProvider"] = "Dev";

        var files = TemplateTestFixture.Generate("WebAPI", p);
        var program = TemplateTestFixture.FileContent(files, "Program.cs");

        Assert.Contains("IEmailSender<ApplicationUser>, DevEmailSender", program);
    }

    [Fact]
    public void IdentityNoOpEmailSender_class_still_exists_for_RegisterConfirmation_razor()
    {
        // Microsoft's RegisterConfirmation.razor_ does `EmailSender is IdentityNoOpEmailSender`,
        // so the type must keep existing even though it is never registered anymore.
        var files = TemplateTestFixture.Generate("WebPortal", WebPortalParams());
        Assert.True(TemplateTestFixture.HasFile(files, "Components/Account/IdentityNoOpEmailSender.cs"));
    }

    internal static Dictionary<string, object> ClientSharedParams() => new()
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
    };

    internal static Dictionary<string, object> MauiNativeAppParams() => new()
    {
        ["ProjectName"] = "Acme",
        ["TargetFramework"] = "net10.0",
    };

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

    // Ruling R (task 8 correction): ClientShared is a plain library referenced by the WASM
    // WebPortalClient too, so it cannot carry MAUI types like DeviceInfo/DevicePlatform. The
    // per-platform base address instead lives in each MAUI app's own MauiProgram.cs, resolved
    // via MAUI's native #if ANDROID multi-targeting rather than a shared HttpClientHelper
    // runtime switch.
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
        Assert.Contains("#if ANDROID", mauiProgram);
        Assert.Contains("new Uri(\"https://10.0.2.2:7202\")", mauiProgram);
        Assert.Contains("new Uri(\"https://localhost:7202\")", mauiProgram);
    }

    [Fact]
    public void MauiNativeApp_base_address_is_reachable_from_an_android_emulator()
    {
        var files = TemplateTestFixture.Generate("MauiNativeApp", MauiNativeAppParams());
        var mauiProgram = TemplateTestFixture.FileContent(files, "MauiProgram.cs");

        Assert.Contains("#if ANDROID", mauiProgram);
        Assert.Contains("new Uri(\"https://10.0.2.2:7202\")", mauiProgram);
    }

    [Fact]
    public void HttpClientHelper_is_generated_from_the_ClientShared_template()
    {
        var files = TemplateTestFixture.Generate("ClientShared", ClientSharedParams());

        Assert.True(TemplateTestFixture.HasFile(files, "Identity/HttpClientHelper.cs"));
        var helper = TemplateTestFixture.FileContent(files, "HttpClientHelper.cs");

        // Framework-neutral: no MAUI types, so it stays compilable from Blazor WebAssembly too.
        Assert.DoesNotContain("DeviceInfo", helper);
        Assert.DoesNotContain("Microsoft.Maui", helper);
        Assert.Contains("IdentityBasePath", helper);
    }

    [Fact]
    public void TokenStorage_is_generated_from_the_ClientShared_template_with_explicit_keys()
    {
        var files = TemplateTestFixture.Generate("ClientShared", ClientSharedParams());

        Assert.True(TemplateTestFixture.HasFile(files, "Identity/TokenStorage.cs"));
        var storage = TemplateTestFixture.FileContent(files, "TokenStorage.cs");

        // ILocalStorageService's [CallerMemberName] default means an omitted key silently
        // stores under the calling method's name — every call here must pass one explicitly.
        Assert.Contains("identity_access_token", storage);
        Assert.Contains("identity_refresh_token", storage);
        Assert.Contains("identity_expires_at", storage);
        Assert.Contains("GetAccessTokenAsync", storage);
        Assert.Contains("GetRefreshTokenAsync", storage);
        Assert.Contains("SaveAsync", storage);
        Assert.Contains("ClearAsync", storage);
        Assert.Contains("IsExpiringSoonAsync", storage);
    }

    [Fact]
    public void Identity_client_targets_the_mapped_identity_endpoints()
    {
        var files = TemplateTestFixture.Generate("ClientShared", new Dictionary<string, object>
        {
            ["ProjectName"] = "Acme",
            ["TargetFramework"] = "net10.0",
        });

        var client = TemplateTestFixture.FileContent(files, "Identity/IdentityClient.cs");

        // The prefix must be derived from the shared constant, never re-typed.
        Assert.Contains("HttpClientHelper.IdentityBasePath", client);

        // Endpoint segments appear literally in the interpolated URLs.
        Assert.Contains("{Base}/register", client);
        Assert.Contains("{Base}/login", client);
        Assert.Contains("{Base}/refresh", client);
        Assert.Contains("{Base}/forgotPassword", client);
        Assert.Contains("{Base}/manage/info", client);

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

    // Ruling V: transport failure (offline device, timeout) must be a failed result, never an
    // exception — an uncaught HttpRequestException/TaskCanceledException out of
    // MauiAuthenticationStateProvider.GetAuthenticationStateAsync would crash the app shell
    // instead of showing a login screen.
    [Fact]
    public void IdentityClient_treats_transport_failure_as_a_failed_result_not_an_exception()
    {
        var files = TemplateTestFixture.Generate("ClientShared", new Dictionary<string, object>
        {
            ["ProjectName"] = "Acme",
            ["TargetFramework"] = "net10.0",
        });

        var client = TemplateTestFixture.FileContent(files, "Identity/IdentityClient.cs");

        Assert.Contains("catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)", client);

        // Being offline is not proof the refresh token is invalid: RefreshAsync's transport-failure
        // catch block must not call ClearAsync — only an actual server rejection (elsewhere in the
        // same method, on a non-success status code) may clear stored tokens.
        var refreshMethodStart = client.IndexOf("public async Task<bool> RefreshAsync()", StringComparison.Ordinal);
        Assert.True(refreshMethodStart >= 0, "RefreshAsync method not found.");
        var refreshMethodEnd = client.IndexOf("\n    public Task LogoutAsync", refreshMethodStart, StringComparison.Ordinal);
        Assert.True(refreshMethodEnd > refreshMethodStart, "Could not locate end of RefreshAsync method.");
        var refreshMethod = client[refreshMethodStart..refreshMethodEnd];

        var catchStart = refreshMethod.IndexOf(
            "catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)", StringComparison.Ordinal);
        Assert.True(catchStart >= 0, "RefreshAsync must catch transport failures separately from status-code failures.");

        var catchBraceOpen = refreshMethod.IndexOf('{', catchStart);
        var catchBraceClose = refreshMethod.IndexOf('}', catchBraceOpen);
        var catchBody = refreshMethod[catchBraceOpen..catchBraceClose];

        Assert.DoesNotContain("ClearAsync", catchBody);
        // Still called elsewhere in the method, on the real-rejection (non-success status) path.
        Assert.Contains("ClearAsync", refreshMethod);
    }

    // Ruling W (task 11 correction): these screens must live in HybridApp, not RazorLibrary.
    // RazorLibrary is loaded into WebPortal via AddAdditionalAssemblies (Program.cs:120-122),
    // and Blazor route matching is case-insensitive, so /account/login here would collide with
    // WebPortal's own /Account/Login scaffold page and throw an ambiguous-route exception.
    // HybridApp's Routes.razor_ uses AppAssembly="@typeof(MauiProgram).Assembly", so pages here
    // are routable only in the Hybrid app and invisible to WebPortal.
    [Theory]
    [InlineData("Components/Pages/Account/Login.razor")]
    [InlineData("Components/Pages/Account/Register.razor")]
    [InlineData("Components/Pages/Account/Logout.razor")]
    [InlineData("Components/Pages/Account/ForgotPassword.razor")]
    public void HybridApp_provides_the_account_screens(string expectedPath)
    {
        var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
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
        var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
        {
            ["ProjectName"] = "Acme",
            ["TargetFramework"] = "net10.0",
            ["UIFramework"] = "Bootstrap",
        });

        var login = TemplateTestFixture.FileContent(files, "Components/Pages/Account/Login.razor");
        Assert.Contains("MauiAuthenticationStateProvider", login);
        Assert.Contains("@page \"/account/login\"", login);
    }

    [Fact]
    public void Hybrid_register_screen_does_not_sign_in_on_success()
    {
        // RequireConfirmedAccount is true (WebAPI Program.cs), so a successful registration
        // must show a "check your email" state rather than navigating home or calling login.
        var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
        {
            ["ProjectName"] = "Acme",
            ["TargetFramework"] = "net10.0",
            ["UIFramework"] = "Bootstrap",
        });

        var register = TemplateTestFixture.FileContent(files, "Components/Pages/Account/Register.razor");
        Assert.Contains("@page \"/account/register\"", register);
        Assert.Contains("RegisterAsync", register);
        Assert.DoesNotContain("LogInAsync", register);
        Assert.DoesNotContain("NavigateTo(\"\")", register);
    }

    [Fact]
    public void Hybrid_forgot_password_screen_never_reveals_account_existence()
    {
        var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
        {
            ["ProjectName"] = "Acme",
            ["TargetFramework"] = "net10.0",
            ["UIFramework"] = "Bootstrap",
        });

        var forgot = TemplateTestFixture.FileContent(files, "Components/Pages/Account/ForgotPassword.razor");
        Assert.Contains("@page \"/account/forgot-password\"", forgot);
        Assert.Contains("ForgotPasswordAsync", forgot);
        // The confirmation branch must not be gated on the call's success/failure result.
        Assert.DoesNotContain("if (result.Succeeded)", forgot);
    }

    [Fact]
    public void Hybrid_logout_screen_signs_out_and_redirects_to_login()
    {
        var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
        {
            ["ProjectName"] = "Acme",
            ["TargetFramework"] = "net10.0",
            ["UIFramework"] = "Bootstrap",
        });

        var logout = TemplateTestFixture.FileContent(files, "Components/Pages/Account/Logout.razor");
        Assert.Contains("@page \"/account/logout\"", logout);
        Assert.Contains("LogOutAsync", logout);
        Assert.Contains("/account/login", logout);
    }

    [Theory]
    [InlineData("Bootstrap")]
    [InlineData("FluentUI")]
    [InlineData("MudBlazor")]
    [InlineData("Radzen")]
    [InlineData("TailwindCSS")]
    public void Hybrid_account_screens_generate_for_every_UI_framework(string uiFramework)
    {
        var files = TemplateTestFixture.Generate("HybridApp", new Dictionary<string, object>
        {
            ["ProjectName"] = "Acme",
            ["TargetFramework"] = "net10.0",
            ["UIFramework"] = uiFramework,
        });

        Assert.True(TemplateTestFixture.HasFile(files, "Components/Pages/Account/Login.razor"));
        Assert.True(TemplateTestFixture.HasFile(files, "Components/Pages/Account/Register.razor"));
        Assert.True(TemplateTestFixture.HasFile(files, "Components/Pages/Account/Logout.razor"));
        Assert.True(TemplateTestFixture.HasFile(files, "Components/Pages/Account/ForgotPassword.razor"));
    }

    [Fact]
    public void Hybrid_account_screens_are_not_generated_under_RazorLibrary()
    {
        // The screens must be invisible to WebPortal, which loads RazorLibrary via
        // AddAdditionalAssemblies. Confirms the corrected file location took effect.
        var files = TemplateTestFixture.Generate("RazorLibrary", new Dictionary<string, object>
        {
            ["ProjectName"] = "Acme",
            ["TargetFramework"] = "net10.0",
            ["UIFramework"] = "Bootstrap",
        });

        Assert.False(TemplateTestFixture.HasFile(files, "Account/Login.razor"));
        Assert.False(TemplateTestFixture.HasFile(files, "Account/Register.razor"));
        Assert.False(TemplateTestFixture.HasFile(files, "Account/Logout.razor"));
        Assert.False(TemplateTestFixture.HasFile(files, "Account/ForgotPassword.razor"));
    }
}
