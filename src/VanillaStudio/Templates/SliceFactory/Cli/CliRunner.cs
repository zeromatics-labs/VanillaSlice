using System.Text.Json;
using {{RootNamespace}}.SliceFactory.Components.Pages;
using {{RootNamespace}}.SliceFactory.Models;
using {{RootNamespace}}.SliceFactory.Services;

namespace {{RootNamespace}}.SliceFactory.Cli;

/// <summary>
/// Handles CLI command execution for SliceFactory
/// </summary>
public class CliRunner
{
    private readonly FeatureManagementService _featureService;
    private readonly ManifestService _manifestService;
    private readonly string _basePath;
    private readonly CodeCofig _codeConfig;

    public CliRunner(
        FeatureManagementService featureService,
        string basePath)
    {
        _featureService = featureService;
        _basePath = basePath;
        _manifestService = new ManifestService(basePath);

        // Load code config
        var configPath = Path.Combine(AppContext.BaseDirectory, "webportal-profile.json");
        if (!File.Exists(configPath))
        {
            configPath = "webportal-profile.json";
        }
        var configJson = File.ReadAllText(configPath);
        _codeConfig = JsonSerializer.Deserialize<CodeCofig>(configJson)
            ?? throw new InvalidOperationException("Could not load webportal-profile.json");
    }

    /// <summary>
    /// Run the CLI with parsed options
    /// </summary>
    public async Task<int> RunAsync(CliOptions options)
    {
        if (options.ShowHelp)
        {
            Console.WriteLine(CliOptions.GetHelpText());
            return 0;
        }

        return options.Command switch
        {
            CliCommand.Generate => await GenerateAsync(options),
            CliCommand.Regenerate => await RegenerateAsync(options),
            CliCommand.RegenerateAll => await RegenerateAllAsync(),
            CliCommand.List => await ListAsync(),
            CliCommand.Remove => await RemoveAsync(options),
            _ => ShowHelpAndExit()
        };
    }

    private async Task<int> GenerateAsync(CliOptions options)
    {
        var (isValid, errorMessage) = options.ValidateForGenerate();
        if (!isValid)
        {
            Console.WriteLine($"Error: {errorMessage}");
            Console.WriteLine();
            Console.WriteLine("Use --help for usage information.");
            return 1;
        }

        var profile = _codeConfig.Profiles.FirstOrDefault();
        if (profile?.Projects == null)
        {
            Console.WriteLine("Error: No profile configuration found in webportal-profile.json");
            return 1;
        }

        var sliceDefinition = ManifestService.FromCliOptions(options);

        Console.WriteLine();
        Console.WriteLine("SliceFactory - Generating Slice");
        Console.WriteLine("================================");
        Console.WriteLine($"  Namespace:    {sliceDefinition.Namespace}");
        Console.WriteLine($"  Directory:    {sliceDefinition.Directory}");
        Console.WriteLine($"  Primary Key:  {sliceDefinition.PrimaryKeyType}");
        if (sliceDefinition.Listing is { } l)  Console.WriteLine($"  Listing:      {l.Name} (prefix: {l.Prefix})");
        if (sliceDefinition.Form is { } f)     Console.WriteLine($"  Form:         {f.Name} (prefix: {f.Prefix})");
        if (sliceDefinition.Action is { } a)   Console.WriteLine($"  Action:       {a.Name} (prefix: {a.Prefix})");
        if (sliceDefinition.SelectList is { } s) Console.WriteLine($"  SelectList:   {s.Name} (prefix: {s.Prefix})");
        Console.WriteLine();

        var projectNs = $"{profile.Projects.FirstOrDefault()?.NameSpace}.{sliceDefinition.Namespace}";

        if (options.Preview)
        {
            Console.WriteLine("PREVIEW MODE - No files will be generated");
            Console.WriteLine();

            var previewFiles = await _featureService.PreviewFeatureFilesAsync(
                listing: sliceDefinition.Listing,
                form: sliceDefinition.Form,
                action: sliceDefinition.Action,
                selectList: sliceDefinition.SelectList,
                moduleNamespace: sliceDefinition.Namespace,
                projectNamespace: projectNs,
                primaryKeyType: sliceDefinition.PrimaryKeyType,
                basePath: _basePath,
                directoryName: sliceDefinition.Directory,
                projects: profile.Projects.ToList());

            Console.WriteLine("Files that would be generated:");
            foreach (var file in previewFiles)
                Console.WriteLine($"  [{file.ProjectType}/{file.SliceType}] {file.FilePath}");

            return 0;
        }

        try
        {
            Console.WriteLine("Generating files...");

            var feature = await _featureService.CreateFeatureAsync(
                listing: sliceDefinition.Listing,
                form: sliceDefinition.Form,
                action: sliceDefinition.Action,
                selectList: sliceDefinition.SelectList,
                moduleNamespace: sliceDefinition.Namespace,
                projectNamespace: projectNs,
                primaryKeyType: sliceDefinition.PrimaryKeyType,
                basePath: _basePath,
                directoryName: sliceDefinition.Directory,
                projects: profile.Projects.ToList(),
                profileConfiguration: JsonSerializer.Serialize(profile),
                uiFramework: profile.UIFramework ?? "TailwindCSS");

            var generatedFiles = feature.Files.Select(f => f.FilePath).ToList();
            sliceDefinition.GeneratedFiles = generatedFiles;
            await _manifestService.AddOrUpdateSliceAsync(sliceDefinition, generatedFiles);

            Console.WriteLine();
            Console.WriteLine($"Successfully generated {generatedFiles.Count} files!");
            Console.WriteLine();
            Console.WriteLine("Generated files:");
            foreach (var file in generatedFiles)
                Console.WriteLine($"  {Path.GetRelativePath(_basePath, file)}");

            Console.WriteLine();
            Console.WriteLine($"Manifest updated: {_manifestService.ManifestPath}");
            Console.WriteLine($"Slice ID: {sliceDefinition.Id}");
            return 0;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Use 'regenerate' command to update an existing slice.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating slice: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> RegenerateAsync(CliOptions options)
    {
        if (string.IsNullOrEmpty(options.SliceId))
        {
            Console.WriteLine("Error: Slice ID is required. Use --id <slice-id> or pass as argument.");
            Console.WriteLine();
            Console.WriteLine("Available slices:");
            await ListAsync();
            return 1;
        }

        var slice = await _manifestService.GetSliceAsync(options.SliceId);
        if (slice == null)
        {
            Console.WriteLine($"Error: Slice '{options.SliceId}' not found in manifest.");
            Console.WriteLine();
            Console.WriteLine("Available slices:");
            await ListAsync();
            return 1;
        }

        Console.WriteLine($"Regenerating slice: {slice.Id}");
        Console.WriteLine($"  Namespace: {slice.Namespace}");
        Console.WriteLine($"  Directory: {slice.Directory}");
        Console.WriteLine();

        var profile = _codeConfig.Profiles.FirstOrDefault();
        if (profile?.Projects == null)
        {
            Console.WriteLine("Error: No profile configuration found in webportal-profile.json");
            return 1;
        }

        try
        {
            // Find the existing Feature in the store by namespace + directory
            var featuresInModule = await _featureService.GetFeaturesByModuleAsync(slice.Namespace);
            var existingFeature = featuresInModule.FirstOrDefault(f =>
                string.Equals(f.DirectoryName, slice.Directory, StringComparison.OrdinalIgnoreCase));

            Feature? feature;
            if (existingFeature != null)
            {
                Console.WriteLine("Updating existing feature files...");
                existingFeature.BasePath = _basePath; // not persisted in JSON; must be re-injected before UpdateFeatureAsync
                feature = await _featureService.UpdateFeatureAsync(
                    featureId: existingFeature.Id,
                    listing: slice.Listing,
                    form: slice.Form,
                    action: slice.Action,
                    selectList: slice.SelectList,
                    projects: profile.Projects.ToList(),
                    profileConfiguration: JsonSerializer.Serialize(profile));
            }
            else
            {
                // Feature not in store yet — create it fresh
                Console.WriteLine("Generating files...");
                var projectNs = $"{profile.Projects.FirstOrDefault()?.NameSpace}.{slice.Namespace}";
                feature = await _featureService.CreateFeatureAsync(
                    listing: slice.Listing,
                    form: slice.Form,
                    action: slice.Action,
                    selectList: slice.SelectList,
                    moduleNamespace: slice.Namespace,
                    projectNamespace: projectNs,
                    primaryKeyType: slice.PrimaryKeyType,
                    basePath: _basePath,
                    directoryName: slice.Directory,
                    projects: profile.Projects.ToList(),
                    profileConfiguration: JsonSerializer.Serialize(profile),
                    uiFramework: profile.UIFramework ?? "TailwindCSS");
            }

            if (feature == null)
            {
                Console.WriteLine("Error: Failed to update feature.");
                return 1;
            }

            var generatedFiles = feature.Files.Select(f => f.FilePath).ToList();
            slice.GeneratedFiles = generatedFiles;
            await _manifestService.AddOrUpdateSliceAsync(slice, generatedFiles);

            Console.WriteLine();
            Console.WriteLine($"Successfully regenerated {generatedFiles.Count} files!");
            Console.WriteLine();
            Console.WriteLine("Generated files:");
            foreach (var file in generatedFiles)
                Console.WriteLine($"  {Path.GetRelativePath(_basePath, file)}");

            Console.WriteLine();
            Console.WriteLine($"Manifest updated: {_manifestService.ManifestPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error regenerating slice: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> RegenerateAllAsync()
    {
        var slices = await _manifestService.GetAllSlicesAsync();

        if (slices.Count == 0)
        {
            Console.WriteLine("No slices found in manifest.");
            return 0;
        }

        Console.WriteLine($"Regenerating {slices.Count} slices...");
        Console.WriteLine();

        var successCount = 0;
        var failCount = 0;

        foreach (var slice in slices)
        {
            Console.WriteLine($"Regenerating: {slice.Id}");

            var options = new CliOptions
            {
                Command = CliCommand.Regenerate,
                SliceId = slice.Id
            };

            var result = await RegenerateAsync(options);
            if (result == 0)
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Completed: {successCount} succeeded, {failCount} failed");

        return failCount > 0 ? 1 : 0;
    }

    private async Task<int> ListAsync()
    {
        var slices = await _manifestService.GetAllSlicesAsync();

        if (slices.Count == 0)
        {
            Console.WriteLine("No slices found in manifest.");
            Console.WriteLine($"Manifest location: {_manifestService.ManifestPath}");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("SliceFactory - Registered Slices");
        Console.WriteLine("=================================");
        Console.WriteLine();
        Console.WriteLine($"{"ID",-40} {"Namespace",-25} {"Types",-20} {"Generated"}");
        Console.WriteLine(new string('-', 110));

        foreach (var slice in slices.OrderBy(s => s.Namespace).ThenBy(s => s.Directory))
        {
            var types = new List<string>();
            if (slice.Listing is not null) types.Add("Listing");
            if (slice.Form is not null)    types.Add("Form");
            if (slice.Action is not null)  types.Add("Action");
            if (slice.SelectList is not null) types.Add("Select");

            Console.WriteLine($"{slice.Id,-40} {slice.Namespace,-25} {string.Join(",", types),-20} {slice.LastGeneratedAt:yyyy-MM-dd HH:mm}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {slices.Count} slices");
        Console.WriteLine($"Manifest: {_manifestService.ManifestPath}");
        return 0;
    }

    private async Task<int> RemoveAsync(CliOptions options)
    {
        if (string.IsNullOrEmpty(options.SliceId))
        {
            Console.WriteLine("Error: Slice ID is required. Use --id <slice-id>");
            return 1;
        }

        var removed = await _manifestService.RemoveSliceAsync(options.SliceId);

        if (removed)
        {
            Console.WriteLine($"Removed slice '{options.SliceId}' from manifest.");
            Console.WriteLine("Note: Generated files were not deleted.");
        }
        else
        {
            Console.WriteLine($"Slice '{options.SliceId}' not found in manifest.");
            return 1;
        }

        return 0;
    }

    private int ShowHelpAndExit()
    {
        Console.WriteLine(CliOptions.GetHelpText());
        return 0;
    }
}
