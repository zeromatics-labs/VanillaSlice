using {{ProjectName}}.ClientShared.Identity;

namespace {{ProjectName}}.MauiNativeApp.Features.Account;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly IdentityClient _identityClient;

    public ForgotPasswordPage(IdentityClient identityClient)
    {
        InitializeComponent();
        _identityClient = identityClient;
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        SendButton.IsEnabled = false;
        await _identityClient.ForgotPasswordAsync(EmailEntry.Text ?? string.Empty);
        SendButton.IsEnabled = true;

        // Never reveal whether the account exists: identical confirmation regardless of
        // outcome, so the branch is not gated on the call's result.
        FormLayout.IsVisible = false;
        ConfirmationLabel.Text =
            $"If an account exists for {EmailEntry.Text}, we sent a reset link. " +
            "Opening it will complete the reset in your browser.";
        ConfirmationLabel.IsVisible = true;
    }
}
