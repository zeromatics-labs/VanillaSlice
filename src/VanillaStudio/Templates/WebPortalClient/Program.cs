using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using {{ProjectName}}.ClientShared;
using {{ProjectName}}.ClientShared.Extensions;
using {{ProjectName}}.Framework;
using {{ProjectName}}.WebPortal.Client;
{{#if (eq UIFramework "FluentUI")}}
using Microsoft.FluentUI.AspNetCore.Components;
{{/if}}

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
builder.Services.AddClientSideFeatureServices();

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

builder.Services.AddSingleton<ILocalStorageService, LocalStorageService>();
builder.Services.AddTransient<CookieHandler>();

// Dialog Service
builder.Services.AddSingleton<{{ProjectName}}.Framework.Services.DialogService>();

// Toast Service
builder.Services.AddSingleton<{{ProjectName}}.Framework.Services.ToastService>();

builder.Services.AddHttpClient<BaseHttpClient, HttpCookieClient>("ServerAPI", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
}).AddHttpMessageHandler<CookieHandler>();

await builder.Build().RunAsync();
