using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using {{ProjectName}}.ClientShared;
using {{ProjectName}}.ClientShared.Extensions;
using {{ProjectName}}.ClientShared.Identity;
using {{ProjectName}}.Framework;
using {{ProjectName}}.NativeMauiApp.Services;
using {{ProjectName}}.MauiNativeApp.Views;
using {{ProjectName}}.MauiNativeApp.Features.Products;
using {{ProjectName}}.MauiNativeApp.Features.Account;
using {{ProjectName}}.MauiNativeApp.ViewModels;
using CommunityToolkit.Maui;

namespace {{ProjectName}}.MauiNativeApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<ProductListPageViewModel>();
        builder.Services.AddTransient<ProductFormPageViewModel>();

        // Register Views
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<Views.Products.ProductListPage>();
        builder.Services.AddTransient<Views.Products.ProductFormPage>();

        // Register the shell through DI (rather than "new"-ing it in App.xaml.cs) so it can
        // take MauiAuthenticationStateProvider as a constructor parameter.
        {{#if (eq NavigationType "Tabs")}}
        builder.Services.AddTransient<AppShellTabs>();
        {{/if}}

        {{#if (eq NavigationType "Flyout")}}
        builder.Services.AddTransient<AppShellFlyout>();
        {{/if}}

        // Add Client Services
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
        builder.Services.AddAuthorizationCore();
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

        // Account pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();

#if DEBUG
        builder.Services.AddLogging(logging =>
        {
            logging.AddDebug();
        });
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}