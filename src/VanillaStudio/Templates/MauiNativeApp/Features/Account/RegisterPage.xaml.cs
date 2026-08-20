using {{ProjectName}}.ClientShared.Identity;

namespace {{ProjectName}}.MauiNativeApp.Features.Account;

public partial class RegisterPage : ContentPage
{
    private readonly MauiAuthenticationStateProvider _authStateProvider;

    public RegisterPage(MauiAuthenticationStateProvider authStateProvider)
    {
        InitializeComponent();
        _authStateProvider = authStateProvider;
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        RegisterButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        var result = await _authStateProvider.RegisterAsync(EmailEntry.Text ?? string.Empty,
                                                            PasswordEntry.Text ?? string.Empty);

        RegisterButton.IsEnabled = true;

        if (!result.Succeeded)
        {
            ErrorLabel.Text = result.Error ?? "Registration failed.";
            ErrorLabel.IsVisible = true;
            return;
        }

        // RequireConfirmedAccount is true (WebAPI Program.cs), so a successful registration
        // does not sign the user in. Hide the form and tell them to confirm by email, then
        // sign in themselves from LoginPage.
        FormLayout.IsVisible = false;
        ConfirmationLabel.Text =
            $"We sent a confirmation link to {EmailEntry.Text}. Confirm your address, then sign in.";
        ConfirmationLabel.IsVisible = true;
    }
}
