using {{RootNamespace}}.SliceFactory.Cli;
using {{RootNamespace}}.SliceFactory.Components;
using {{RootNamespace}}.SliceFactory.Data;
using {{RootNamespace}}.SliceFactory.Services;

// Parse CLI arguments first
var cliOptions = CliOptions.Parse(args);

// If CLI command is specified, run in CLI mode
if (cliOptions.Command != CliCommand.None || cliOptions.ShowHelp)
{
    return await RunCliModeAsync(args, cliOptions);
}

// Otherwise, run in Web UI mode
return await RunWebUiModeAsync(args);

/// <summary>
/// Run SliceFactory in CLI mode (non-interactive)
/// </summary>
static async Task<int> RunCliModeAsync(string[] args, CliOptions cliOptions)
{
    // Build minimal services for CLI mode
    var services = new ServiceCollection();

    // Add logging
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise in CLI
    });

    // JSON-based metadata store (singleton — loaded once, held in memory)
    services.AddSingleton<JsonFeatureStore>();

    // Add content root provider for CLI mode
    services.AddSingleton<IContentRootProvider>(new CliContentRootProvider());

    // Add required services
    services.AddScoped<PluralizationService>();
    services.AddScoped<TemplateEngineService>();
    services.AddScoped<FeatureManagementService>();
    services.AddScoped<RegistrationManagementService>();
    services.AddScoped<NavigationManagementService>();
    services.AddScoped<PlacementGuidanceService>();

    var serviceProvider = services.BuildServiceProvider();

    // Detect solution root for base path
    var basePath = FindSolutionRoot() ?? Directory.GetCurrentDirectory();

    // Run CLI
    using (var scope = serviceProvider.CreateScope())
    {
        var featureService = scope.ServiceProvider.GetRequiredService<FeatureManagementService>();
        var cliRunner = new CliRunner(featureService, basePath);
        return await cliRunner.RunAsync(cliOptions);
    }
}

/// <summary>
/// Run SliceFactory in Web UI mode (interactive)
/// </summary>
static async Task<int> RunWebUiModeAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // JSON-based metadata store (singleton — loaded once, held in memory)
    builder.Services.AddSingleton<JsonFeatureStore>();

    // Content root provider (web mode uses IWebHostEnvironment)
    builder.Services.AddSingleton<IContentRootProvider, WebContentRootProvider>();

    // Add pluralization service
    builder.Services.AddScoped<PluralizationService>();

    // Add template engine service
    builder.Services.AddScoped<TemplateEngineService>();

    // Add feature management service
    builder.Services.AddScoped<FeatureManagementService>();

    // Add registration management service
    builder.Services.AddScoped<RegistrationManagementService>();

    // Add navigation management service
    builder.Services.AddScoped<NavigationManagementService>();

    // Add placement guidance service
    builder.Services.AddScoped<PlacementGuidanceService>();

    // Add path detection service
    builder.Services.AddScoped<PathDetectionService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
    return 0;
}

static string? FindSolutionRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory != null)
    {
        if (directory.GetFiles("*.sln").Length > 0)
            return directory.FullName;
        directory = directory.Parent;
    }
    return null;
}
