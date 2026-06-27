using {{RootNamespace}}.SliceFactory.Components.Pages;
using {{RootNamespace}}.SliceFactory.Models;

namespace {{RootNamespace}}.SliceFactory.Services;

/// <summary>
/// Service responsible for automatically managing service registrations using placeholder replacement
/// </summary>
public class RegistrationManagementService
{
    private readonly ILogger<RegistrationManagementService> _logger;
    private const string SERVER_PLACEHOLDER = "//##ServerDataService##";
    private const string CLIENT_PLACEHOLDER = "//##ClientDataService##";

    public RegistrationManagementService(ILogger<RegistrationManagementService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Updates registration files for a newly created feature using placeholder replacement
    /// </summary>
    public async Task UpdateRegistrationsForFeatureAsync(Feature feature, List<Project> projects)
    {
        var label = feature.Listing?.Prefix ?? feature.Form?.Prefix
                 ?? feature.Action?.Prefix ?? feature.SelectList?.Prefix ?? feature.DirectoryName;
        try
        {
            _logger.LogInformation("Updating service registrations for feature: {Label}", label);

            foreach (var project in projects)
            {
                if (project.ProjectType == ProjectType.ServerSideServices)
                    await UpdateServerSideRegistrationsAsync(feature, project);
                else if (project.ProjectType == ProjectType.ClientShared)
                    await UpdateClientSideRegistrationsAsync(feature, project);
            }

            _logger.LogInformation("Successfully updated service registrations for feature: {Label}", label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update service registrations for feature: {Label}", label);
            throw;
        }
    }

    /// <summary>
    /// Removes registration entries for a deleted feature (placeholder-based approach doesn't support removal)
    /// </summary>
    public async Task RemoveRegistrationsForFeatureAsync(Feature feature, List<Project> projects)
    {
        var label = feature.Listing?.Prefix ?? feature.Form?.Prefix
                 ?? feature.Action?.Prefix ?? feature.SelectList?.Prefix ?? feature.DirectoryName;
        _logger.LogWarning("Registration removal not supported with placeholder-based approach for feature: {Label}", label);
        await Task.CompletedTask;
    }

    private async Task UpdateServerSideRegistrationsAsync(Feature feature, Project project)
    {
        var registrationFilePath = GetRegistrationFilePath(project, feature.BasePath);
        if (!File.Exists(registrationFilePath))
        {
            _logger.LogWarning("Server-side registration file not found: {FilePath}", registrationFilePath);
            return;
        }

        var registrations = GenerateServerSideRegistrations(feature);
        await UpdateRegistrationFileWithPlaceholderAsync(registrationFilePath, registrations, SERVER_PLACEHOLDER);
    }

    private async Task UpdateClientSideRegistrationsAsync(Feature feature, Project project)
    {
        var registrationFilePath = GetRegistrationFilePath(project, feature.BasePath);
        if (!File.Exists(registrationFilePath))
        {
            _logger.LogWarning("Client-side registration file not found: {FilePath}", registrationFilePath);
            return;
        }

        var registrations = GenerateClientSideRegistrations(feature);
        await UpdateRegistrationFileWithPlaceholderAsync(registrationFilePath, registrations, CLIENT_PLACEHOLDER);
    }

    private string GetRegistrationFilePath(Project project, string basePath)
    {
        var projectPath = project.Path?.Replace("\\Features", "") ?? "";
        return Path.Combine(basePath, projectPath, "Extensions", "FeaturesRegistrationExt.cs");
    }

    private List<string> GenerateServerSideRegistrations(Feature feature)
    {
        var registrations = new List<string>();
        var ns = feature.ModuleNamespace;

        if (feature.Listing is { } l)
        {
            registrations.Add($"            // {l.Prefix} Listing");
            registrations.Add($"            services.AddScoped<ServiceContracts.Features.{ns}.I{l.Prefix}ListingDataService, Features.{ns}.{l.Prefix}ListingServerDataService>();");
        }

        if (feature.Form is { } f)
        {
            registrations.Add($"            // {f.Prefix} Form");
            registrations.Add($"            services.AddScoped<ServiceContracts.Features.{ns}.I{f.Prefix}FormDataService, Features.{ns}.{f.Prefix}FormServerDataService>();");
        }

        if (feature.Action is { } a)
        {
            registrations.Add($"            // {a.Prefix} Action");
            registrations.Add($"            services.AddScoped<ServiceContracts.Features.{ns}.I{a.Prefix}ActionDataService, Features.{ns}.{a.Prefix}ActionServerDataService>();");
        }

        if (feature.SelectList is { } s)
        {
            registrations.Add($"            // {s.Prefix} SelectList");
            registrations.Add($"            services.AddScoped<ServiceContracts.Features.{ns}.I{s.Prefix}SelectListDataService, Features.{ns}.{s.Prefix}SelectListServerDataService>();");
        }

        return registrations;
    }

    private List<string> GenerateClientSideRegistrations(Feature feature)
    {
        var registrations = new List<string>();
        var ns = feature.ModuleNamespace;

        if (feature.Listing is { } l)
        {
            registrations.Add($"            // {l.Prefix} Listing");
            registrations.Add($"            services.AddScoped<ServiceContracts.Features.{ns}.I{l.Prefix}ListingDataService, Features.{ns}.{l.Prefix}ListingClientDataService>();");
        }

        if (feature.Form is { } f)
        {
            registrations.Add($"            // {f.Prefix} Form");
            registrations.Add($"            services.AddScoped<ServiceContracts.Features.{ns}.I{f.Prefix}FormDataService, Features.{ns}.{f.Prefix}FormClientDataService>();");
        }

        if (feature.Action is { } a)
        {
            registrations.Add($"            // {a.Prefix} Action");
            registrations.Add($"            services.AddScoped<ServiceContracts.Features.{ns}.I{a.Prefix}ActionDataService, Features.{ns}.{a.Prefix}ActionClientDataService>();");
        }

        if (feature.SelectList is { } s)
        {
            registrations.Add($"            // {s.Prefix} SelectList");
            registrations.Add($"            services.AddScoped<ServiceContracts.Features.{ns}.I{s.Prefix}SelectListDataService, Features.{ns}.{s.Prefix}SelectListClientDataService>();");
        }

        return registrations;
    }

    private async Task UpdateRegistrationFileWithPlaceholderAsync(string filePath, List<string> newRegistrations, string placeholder)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);

            if (!content.Contains(placeholder))
            {
                _logger.LogWarning("Placeholder {Placeholder} not found in file: {FilePath}", placeholder, filePath);
                return;
            }

            var lines = content.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == placeholder)
                {
                    var indentation = lines[i].Substring(0, lines[i].IndexOf(placeholder));
                    var indentedReplacements = newRegistrations.Select(reg =>
                        reg.StartsWith("            ") ? reg : indentation + reg.TrimStart()).ToList();

                    var finalReplacement = string.Join(Environment.NewLine, indentedReplacements) +
                                         Environment.NewLine + Environment.NewLine + indentation + placeholder;
                    lines[i] = finalReplacement;
                    break;
                }
            }

            var updatedContent = string.Join(Environment.NewLine, lines);
            await File.WriteAllTextAsync(filePath, updatedContent);
            _logger.LogInformation("Updated registration file: {FilePath} with {Count} registrations", filePath, newRegistrations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update registration file: {FilePath}", filePath);
            throw;
        }
    }
}
