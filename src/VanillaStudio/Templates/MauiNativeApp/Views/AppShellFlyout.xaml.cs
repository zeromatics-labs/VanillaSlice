using {{ProjectName}}.MauiNativeApp.Views.Products;
using {{ProjectName}}.MauiNativeApp.Features.Account;

namespace {{ProjectName}}.MauiNativeApp.Views;

public partial class AppShellFlyout : Shell
{
    public AppShellFlyout()
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

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
        await DisplayAlert("Settings", "Settings functionality coming soon!", "OK");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool result = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
        if (result)
        {
            // Implement logout logic here
            await DisplayAlert("Logout", "Logout functionality coming soon!", "OK");
        }
    }
}