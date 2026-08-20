using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using {{ProjectName}}.ClientShared;
using {{ProjectName}}.ClientShared.Extensions;
using {{ProjectName}}.ClientShared.Identity;
using {{ProjectName}}.Framework;
using {{ProjectName}}.HybridApp.Services;
{{#if (eq UIFramework "FluentUI")}}
using Microsoft.FluentUI.AspNetCore.Components;
{{/if}}
namespace {{ProjectName}}.HybridApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Services.AddClientSideFeatureServices();
            builder.Services.AddSingleton<ILocalStorageService, LocalStorageService>();
            builder.Services.AddSingleton<TokenStorage>();

            // Dialog Service
            builder.Services.AddSingleton<{{ProjectName}}.Framework.Services.DialogService>();
            builder.Services.AddHttpClient<BaseHttpClient, HttpTokenClient>("ServerAPI", client =>
            {
    #if ANDROID
                    // Android emulators reach the host loopback via 10.0.2.2; "localhost" is the emulated device itself.
                    client.BaseAddress = new Uri("https://10.0.2.2:7202");
    #else
                    client.BaseAddress = new Uri("https://localhost:7202");
    #endif
            });

            // Auth state: AuthorizeView/[Authorize] read AuthenticationStateProvider, which
            // MauiAuthenticationStateProvider supplies from tokens held by TokenStorage.
            // AddCascadingAuthenticationState supplies the cascading AuthenticationState that
            // AuthorizeRouteView (Routes.razor_) and AuthorizeView need — without it every
            // AuthorizeRouteView renders as unauthorised regardless of sign-in state.
            builder.Services.AddAuthorizationCore();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddHttpClient<IdentityClient>(client =>
            {
    #if ANDROID
                    client.BaseAddress = new Uri("https://10.0.2.2:7202");
    #else
                    client.BaseAddress = new Uri("https://localhost:7202");
    #endif
            });
            builder.Services.AddScoped<MauiAuthenticationStateProvider>();
            builder.Services.AddScoped<AuthenticationStateProvider>(s =>
                s.GetRequiredService<MauiAuthenticationStateProvider>());

    #if DEBUG
                builder.Services.AddLogging(logging =>
                {
                    logging.AddDebug();
                });
    #endif

            // UI Framework Services
            {{#if (eq UIFramework "FluentUI")}}
            builder.Services.AddFluentUIComponents();
            {{/if}}

            {{#if (eq UIFramework "MudBlazor")}}
            builder.Services.AddMudServices();
            {{/if}}

            {{#if (eq UIFramework "Radzen")}}
            builder.Services.AddRadzenComponents();
            {{/if}}

            return builder.Build();
        }
    }
}
