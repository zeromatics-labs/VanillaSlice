using {{ProjectName}}.ClientShared.Identity;
using {{ProjectName}}.MauiNativeApp.Views.Products;
using {{ProjectName}}.MauiNativeApp.Features.Account;

namespace {{ProjectName}}.MauiNativeApp.Views;

public partial class AppShellTabs : Shell
{
    private readonly MauiAuthenticationStateProvider _authStateProvider;

    public AppShellTabs(MauiAuthenticationStateProvider authStateProvider)
    {
        InitializeComponent();
        _authStateProvider = authStateProvider;

        // Register routes for navigation
        Routing.RegisterRoute(nameof(ProductFormPage), typeof(ProductFormPage));
        Routing.RegisterRoute("ProductDetails", typeof(ProductFormPage));

        // Account routes. These are global routes navigated relatively (no "//" prefix) —
        // "//" only addresses the Shell's visual hierarchy, which these pages are not part of.
        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("forgot-password", typeof(ForgotPasswordPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var state = await _authStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            // "login" is a global route registered above via Routing.RegisterRoute, navigated
            // relatively — "//" only addresses the Shell's visual hierarchy, which the login
            // page is not part of.
            await Shell.Current.GoToAsync("login");
        }
    }
}