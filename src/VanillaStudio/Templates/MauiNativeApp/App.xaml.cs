using Microsoft.Extensions.DependencyInjection;
using {{ProjectName}}.MauiNativeApp.Views;

namespace {{ProjectName}}.MauiNativeApp;

public partial class App : Application
{
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        // The shell is resolved from DI (rather than "new"-ed here) so it can take
        // MauiAuthenticationStateProvider as a constructor parameter — see MauiProgram.cs,
        // which registers the same shell type selected by NavigationType below.
        {{#if (eq NavigationType "Tabs")}}
        MainPage = serviceProvider.GetRequiredService<AppShellTabs>();
        {{/if}}

        {{#if (eq NavigationType "Flyout")}}
        MainPage = serviceProvider.GetRequiredService<AppShellFlyout>();
        {{/if}}
    }
}