using {{ProjectName}}.ClientShared.Identity;

namespace {{ProjectName}}.MauiNativeApp.Features.Account;

public partial class LoginPage : ContentPage
{
    private readonly MauiAuthenticationStateProvider _authStateProvider;

    public LoginPage(MauiAuthenticationStateProvider authStateProvider)
    {
        InitializeComponent();
        _authStateProvider = authStateProvider;
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        SignInButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        var result = await _authStateProvider.LogInAsync(EmailEntry.Text ?? string.Empty,
                                                         PasswordEntry.Text ?? string.Empty);

        SignInButton.IsEnabled = true;

        if (!result.Succeeded)
        {
            ErrorLabel.Text = result.Error ?? "Sign in failed. Check your email and password.";
            ErrorLabel.IsVisible = true;
            return;
        }

        // "MainPage" is the ShellContent route declared by both AppShellTabs and
        // AppShellFlyout (Views/AppShellTabs.xaml, Views/AppShellFlyout.xaml). Routes
        // registered via Routing.RegisterRoute are global routes navigated relatively —
        // the double-slash prefix only addresses the Shell's visual hierarchy, and using
        // it with a route that isn't part of that hierarchy throws "unable to figure out
        // route" at runtime.
        await Shell.Current.GoToAsync("//MainPage");
    }

    private Task OnRegisterClickedAsync() => Shell.Current.GoToAsync("register");

    private async void OnRegisterClicked(object? sender, EventArgs e) => await OnRegisterClickedAsync();

    private async void OnForgotPasswordClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("forgot-password");
}
