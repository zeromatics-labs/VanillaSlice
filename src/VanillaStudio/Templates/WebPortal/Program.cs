using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using {{ProjectName}}.Server.Data;
using {{ProjectName}}.Server.DataServices.Extensions;
using {{ProjectName}}.WebPortal.Components;
using {{ProjectName}}.WebPortal.Components.Account;
{{#if (eq UIFramework "FluentUI")}}
using Microsoft.FluentUI.AspNetCore.Components;
{{/if}}

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

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

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddServerSideFeatureServices();

// Dialog Service
builder.Services.AddSingleton<{{ProjectName}}.Framework.Services.DialogService>();

// Toast Service
builder.Services.AddSingleton<{{ProjectName}}.Framework.Services.ToastService>();

// Add services to the container.
var controllersAssembly = new AssemblyPart((typeof(FeaturesRegistrationExt)).Assembly);
builder.Services.AddControllers().PartManager.ApplicationParts.Add(controllersAssembly);
builder.Services.AddControllers();
var app = builder.Build();

app.MapDefaultEndpoints();
app.UseMiddleware<ErrorHandlerMiddleware>();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
    var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies([
        typeof({{ProjectName}}.WebPortal.Client._Imports).Assembly,
        typeof({{ProjectName}}.Razor._Imports).Assembly]);

// Add additional endpoints required by the Identity /Account Razor components.
//app.MapAdditionalIdentityEndpoints();
app.MapControllers();
app.Run();
