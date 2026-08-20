using {{ProjectName}}.MauiNativeApp.Views.Products;
using {{ProjectName}}.MauiNativeApp.Features.Account;

namespace {{ProjectName}}.MauiNativeApp.Views;

public partial class AppShellTabs : Shell
{
    public AppShellTabs()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(ProductFormPage), typeof(ProductFormPage));
        Routing.RegisterRoute("ProductDetails", typeof(ProductFormPage));

        // Account routes. These are global routes navigated relatively (no "//" prefix) —
        // "//" only addresses the Shell's visual hierarchy, which these pages are not part of.
        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("forgot-password", typeof(ForgotPasswordPage));
    }
}