using {{RootNamespace}}.SliceFactory.Models;
using {{RootNamespace}}.SliceFactory.Components.Pages;

namespace {{RootNamespace}}.SliceFactory.Services;

/// <summary>
/// Service responsible for automatically managing navigation menu items using placeholder replacement
/// </summary>
public class NavigationManagementService
{
    private readonly ILogger<NavigationManagementService> _logger;
    private readonly PluralizationService _pluralizationService;
    private const string NAVIGATION_PLACEHOLDER = "@* ##MenuItem## *@";

    public NavigationManagementService(
        ILogger<NavigationManagementService> logger,
        PluralizationService pluralizationService)
    {
        _logger = logger;
        _pluralizationService = pluralizationService;
    }

    /// <summary>
    /// Updates navigation menu files for a newly created feature using placeholder replacement
    /// </summary>
    public async Task UpdateNavigationForFeatureAsync(Feature feature, List<Project> projects)
    {
        var label = feature.Listing?.Prefix ?? feature.DirectoryName;
        try
        {
            _logger.LogInformation("Updating navigation menus for feature: {Label}", label);

            // Only add navigation items for features that have listings
            if (feature.Listing is null)
            {
                _logger.LogInformation("Feature {Label} has no listing, skipping navigation update", label);
                return;
            }

            foreach (var project in projects)
            {
                if (project.ProjectType == ProjectType.UILibrary)
                {
                    await UpdateNavigationMenuAsync(feature, project);
                }
            }

            // Also update navigation in WebPortal and HybridApp projects directly
            await UpdateWebPortalNavigationAsync(feature);
            await UpdateHybridAppNavigationAsync(feature);

            _logger.LogInformation("Successfully updated navigation menus for feature: {Label}", label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update navigation menus for feature: {Label}", label);
            throw;
        }
    }

    /// <summary>
    /// Removes navigation entries for a deleted feature (placeholder-based approach doesn't support removal)
    /// </summary>
    public async Task RemoveNavigationForFeatureAsync(Feature feature, List<Project> projects)
    {
        var label = feature.Listing?.Prefix ?? feature.DirectoryName;
        _logger.LogWarning("Navigation removal not supported with placeholder-based approach for feature: {Label}", label);
        // Note: With placeholder approach, we don't remove navigation items automatically
        // This would require manual cleanup or a more sophisticated approach
        await Task.CompletedTask;
    }

    private async Task UpdateNavigationMenuAsync(Feature feature, Project project)
    {
        var navMenuPath = GetNavMenuFilePath(project, feature.BasePath);
        if (!File.Exists(navMenuPath))
        {
            _logger.LogWarning("Navigation menu file not found: {FilePath}", navMenuPath);
            return;
        }

        var navigationItems = GenerateNavigationItems(feature);
        await UpdateNavigationFileWithPlaceholderAsync(navMenuPath, navigationItems, feature.Listing!.Prefix);
    }

    private string GetNavMenuFilePath(Project project, string basePath)
    {
        var projectPath = project.Path;

        if (project.ProjectType == ProjectType.UILibrary)
        {
            // For UI Library projects, we need to determine the correct path
            // This might need to be adjusted based on the actual project structure
            return Path.Combine(basePath, projectPath ?? "", "Components", "Layout", "NavMenu.razor");
        }

        return string.Empty;
    }

    private string GenerateNavigationItems(Feature feature)
    {
        // listing.Name IS already the plural display name (e.g. "Doctors")
        var listing = feature.Listing!;
        var pluralizedRoute = listing.Name.ToLowerInvariant();
        var displayName = listing.Prefix; // singular PascalCase prefix for label

        return $@"        
        {{#if (eq UIFramework "Bootstrap")}}
        <div class='nav-item px-3'>
            <NavLink class='nav-link' href='{pluralizedRoute}'>
                <span class='bi bi-list-nested-nav-menu' aria-hidden='true'></span> {displayName}
            </NavLink>
        </div>
        {{/if}}

        {{#if (eq UIFramework "FluentUI")}}
        <FluentNavLink Href='{pluralizedRoute}' Icon='@(new Icons.Regular.Size20.NumberSymbolSquare())' IconColor='Color.Accent'>{displayName}</FluentNavLink>
        {{/if}}

        {{#if (eq UIFramework "MudBlazor")}}
        <MudNavLink Href='{pluralizedRoute}' Icon='Icons.Material.Filled.Inventory'>
            {displayName}
        </MudNavLink>
        {{/if}}

        {{#if (eq UIFramework "Radzen")}}
        <RadzenPanelMenuItem Text='{displayName}' Path='{pluralizedRoute}' Icon='inventory' />
        {{/if}}

        {{#if (eq UIFramework "TailwindCSS")}}
        " + $"<a href='/{pluralizedRoute}' class='group flex items-center px-4 py-3 text-sm font-medium rounded-lg transition-all duration-200 @(IsActive(\"{pluralizedRoute}\") ? \"bg-black text-white\" : \"text-gray-700 hover:text-black hover:bg-gray-100\")'>" +
             $"<div class='flex items-center justify-center w-8 h-8 rounded-md @(IsActive(\"{{pluralizedRoute}}\") ? \"bg-white/20\" : \"bg-gray-100 group-hover:bg-gray-200\") mr-3 transition-colors duration-200'>" +
             $"<svg class='w-4 h-4 @(IsActive(\"{{pluralizedRoute}}\") ? \"text-white\" : \"text-gray-500 group-hover:text-gray-700\")' fill='none' stroke='currentColor' viewBox='0 0 24 24'>" +
             $"<path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M4 6h16M4 10h16M4 14h16M4 18h16'/>" +
             $"</svg>" +
             $"</div>" +
             $"<div class='flex-1'>" +
             $"<p class='font-medium'>{displayName}</p>" +
             $"<p class='text-xs opacity-75'>Manage {pluralizedRoute}</p>" +
             $"</div>" +
             $"@if (IsActive(\"{{pluralizedRoute}}\"))" +
             $"{{" +
             $"<div class='w-1.5 h-1.5 bg-white rounded-full'></div>" +
             $"}}" +
             $"</a>" +
             $"\"" + @"
        {{/if}}";
    }

    private async Task UpdateWebPortalNavigationAsync(Feature feature)
    {
        var webPortalNavMenuPath = Path.Combine(feature.BasePath,
            "{{ProjectName}}.WebPortal",
            "{{ProjectName}}.WebPortal.Client",
            "Layout",
            "NavMenu.razor");

        if (File.Exists(webPortalNavMenuPath))
        {
            var navigationItems = GenerateNavigationItems(feature);
            await UpdateNavigationFileWithPlaceholderAsync(webPortalNavMenuPath, navigationItems, feature.Listing!.Prefix);
        }
        else
        {
            _logger.LogWarning("WebPortal NavMenu file not found: {FilePath}", webPortalNavMenuPath);
        }
    }

    private async Task UpdateHybridAppNavigationAsync(Feature feature)
    {
        var hybridAppNavMenuPath = Path.Combine(feature.BasePath,
            "{{ProjectName}}.HybridApp",
            "Components",
            "Layout",
            "NavMenu.razor");

        if (File.Exists(hybridAppNavMenuPath))
        {
            var navigationItems = GenerateNavigationItems(feature);
            await UpdateNavigationFileWithPlaceholderAsync(hybridAppNavMenuPath, navigationItems, feature.Listing!.Prefix);
        }
        else
        {
            _logger.LogWarning("HybridApp NavMenu file not found: {FilePath}", hybridAppNavMenuPath);
        }
    }

    private async Task UpdateNavigationFileWithPlaceholderAsync(string filePath, string newNavigationItem, string componentPrefix)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);

            if (!content.Contains(NAVIGATION_PLACEHOLDER))
            {
                _logger.LogWarning("Navigation placeholder {Placeholder} not found in file: {FilePath}", NAVIGATION_PLACEHOLDER, filePath);
                return;
            }

            // Find the placeholder with its current indentation and replace it
            var lines = content.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == NAVIGATION_PLACEHOLDER)
                {
                    // Get the current indentation
                    var indentation = lines[i].Substring(0, lines[i].IndexOf(NAVIGATION_PLACEHOLDER));

                    // Build the replacement with proper indentation
                    var indentedNavigationItem = newNavigationItem.Replace("        ", indentation + "        ");
                    var finalReplacement = indentedNavigationItem + Environment.NewLine + 
                                         Environment.NewLine + indentation + NAVIGATION_PLACEHOLDER;

                    lines[i] = finalReplacement;
                    break;
                }
            }

            var updatedContent = string.Join(Environment.NewLine, lines);
            await File.WriteAllTextAsync(filePath, updatedContent);

            _logger.LogInformation("Updated navigation file: {FilePath} with navigation item for {ComponentPrefix}",
                filePath, componentPrefix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update navigation file: {FilePath}", filePath);
            throw;
        }
    }
}
