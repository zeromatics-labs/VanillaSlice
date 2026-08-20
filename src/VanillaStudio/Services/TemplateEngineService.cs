using ZKnow.VanillaStudio.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;

namespace ZKnow.VanillaStudio.Services
{
    public class TemplateEngineService
    {
        private readonly ILogger<TemplateEngineService> _logger;
        private readonly string _templatesBasePath;

        public TemplateEngineService(ILogger<TemplateEngineService> logger)
        {
            _logger = logger;

            // Try to find the Templates directory in the source code location
            var currentDir = Directory.GetCurrentDirectory();
            var templatesPath = Path.Combine(currentDir, "Templates");

            // If not found in current directory, try relative to the executable
            if (!Directory.Exists(templatesPath))
            {
                templatesPath = Path.Combine(AppContext.BaseDirectory, "Templates");
            }

            // If still not found, try going up directories to find the source
            if (!Directory.Exists(templatesPath))
            {
                var sourceDir = FindSourceDirectory(currentDir);
                if (sourceDir != null)
                {
                    templatesPath = Path.Combine(sourceDir, "Templates");
                }
            }

            _templatesBasePath = templatesPath;
            _logger.LogInformation("📁 Templates base path: {TemplatesPath}", _templatesBasePath);

            InitializeTemplateEngine();
        }

        public TemplateEngineService(ILogger<TemplateEngineService> logger, string templatesBasePath)
        {
            _logger = logger;
            _templatesBasePath = templatesBasePath;
            _logger.LogInformation("📁 Templates base path (explicit): {TemplatesPath}", _templatesBasePath);
            InitializeTemplateEngine();
        }

        private string? FindSourceDirectory(string startPath)
        {
            var current = new DirectoryInfo(startPath);

            while (current != null)
            {
                var templatesDir = Path.Combine(current.FullName, "Templates");
                if (Directory.Exists(templatesDir))
                {
                    return current.FullName;
                }

                // Look for project file as indicator of source directory
                if (current.GetFiles("*.csproj").Any())
                {
                    var templatesInProject = Path.Combine(current.FullName, "Templates");
                    if (Directory.Exists(templatesInProject))
                    {
                        return current.FullName;
                    }
                }

                current = current.Parent;
            }

            return null;
        }

        private void InitializeTemplateEngine()
        {
            try
            {
                _logger.LogInformation("🔧 Initializing Template Engine...");

                // For now, we'll use a simple file-based approach without the complex TemplateEngine API
                // This is more reliable and easier to maintain

                _logger.LogInformation("✅ Template Engine initialized successfully (using simple file-based approach)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize Template Engine");
                throw;
            }
        }

        public async Task<List<GeneratedFile>> GenerateFromTemplateAsync(
            string templateName,
            Dictionary<string, object> parameters,
            string outputBasePath = "",
            Func<string, bool>? includeFile = null)
        {
            try
            {
                _logger.LogInformation("🎯 Generating files from template: {TemplateName}", templateName);

                var templatePath = Path.Combine(_templatesBasePath, templateName);
                if (!Directory.Exists(templatePath))
                {
                    throw new DirectoryNotFoundException($"Template directory not found: {templatePath}");
                }

                // For now, use a simple file-based template processing approach
                // This will be more reliable than the complex TemplateEngine API
                var generatedFiles = await ProcessTemplateFilesAsync(templatePath, parameters, outputBasePath, includeFile);

                _logger.LogInformation("✅ Successfully generated {FileCount} files from template {TemplateName}",
                    generatedFiles.Count, templateName);

                return generatedFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to generate files from template {TemplateName}", templateName);
                throw;
            }
        }

        private async Task<List<GeneratedFile>> ProcessTemplateFilesAsync(
            string templatePath,
            Dictionary<string, object> parameters,
            string outputBasePath,
            Func<string, bool>? includeFile = null)
        {
            var generatedFiles = new List<GeneratedFile>();

            // Get all files except the .template.config directory
            var templateFiles = Directory.GetFiles(templatePath, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(".template.config"))
                .Where(f => includeFile is null ||
                            includeFile(Path.GetRelativePath(templatePath, f).Replace('\\', '/')))
                .ToArray();

            foreach (var templateFile in templateFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(templateFile, Encoding.UTF8);

                    // Process template placeholders
                    var processedContent = ProcessTemplatePlaceholders(content, parameters);

                    // Calculate relative path
                    var relativePath = Path.GetRelativePath(templatePath, templateFile);

                    // Process placeholders in the file path as well (using simple replacement for file names)
                    relativePath = ProcessFileNamePlaceholders(relativePath, parameters);

                    // Adjust the relative path if outputBasePath is provided
                    if (!string.IsNullOrEmpty(outputBasePath))
                    {
                        relativePath = Path.Combine(outputBasePath, relativePath);
                    }

                    var fileType = DetermineFileType(templateFile);

                    generatedFiles.Add(new GeneratedFile
                    {
                        RelativePath = relativePath.Replace('\\', '/'), // Normalize path separators
                        Content = processedContent,
                        Type = fileType
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process template file: {TemplateFile}", templateFile);
                }
            }

            return generatedFiles;
        }

        private string ProcessTemplatePlaceholders(string content, Dictionary<string, object> parameters)
        {
            var processedContent = content;

            // First, process conditional blocks
            processedContent = ProcessConditionalBlocks(processedContent, parameters);

            // Then, process simple placeholders
            foreach (var parameter in parameters)
            {
                var placeholder = $"{{{{{parameter.Key}}}}}";
                var value = parameter.Value?.ToString() ?? string.Empty;
                processedContent = processedContent.Replace(placeholder, value);
            }

            return processedContent;
        }

        private string ProcessConditionalBlocks(string content, Dictionary<string, object> parameters)
        {
            var processedContent = content;

            // Process {{#if (eq UIFramework "FluentUI")}}...{{/if}} blocks
            var ifPattern = @"\{\{#if\s+\(eq\s+(\w+)\s+""([^""]+)""\)\}\}(.*?)\{\{/if\}\}";
            var regex = new System.Text.RegularExpressions.Regex(ifPattern,
                System.Text.RegularExpressions.RegexOptions.Singleline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            processedContent = regex.Replace(processedContent, match =>
            {
                var parameterName = match.Groups[1].Value;
                var expectedValue = match.Groups[2].Value;
                var blockContent = match.Groups[3].Value;

                // Check if the parameter exists and matches the expected value
                if (parameters.TryGetValue(parameterName, out var actualValue))
                {
                    var actualValueStr = actualValue?.ToString() ?? string.Empty;
                    if (string.Equals(actualValueStr, expectedValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return blockContent; // Include the content
                    }
                }

                return string.Empty; // Exclude the content
            });

            // Process {{#unless (eq UIFramework "Bootstrap")}}...{{/unless}} blocks
            var unlessPattern = @"\{\{#unless\s+\(eq\s+(\w+)\s+""([^""]+)""\)\}\}(.*?)\{\{/unless\}\}";
            var unlessRegex = new System.Text.RegularExpressions.Regex(unlessPattern,
                System.Text.RegularExpressions.RegexOptions.Singleline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            processedContent = unlessRegex.Replace(processedContent, match =>
            {
                var parameterName = match.Groups[1].Value;
                var expectedValue = match.Groups[2].Value;
                var blockContent = match.Groups[3].Value;

                // Check if the parameter exists and does NOT match the expected value
                if (parameters.TryGetValue(parameterName, out var actualValue))
                {
                    var actualValueStr = actualValue?.ToString() ?? string.Empty;
                    if (!string.Equals(actualValueStr, expectedValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return blockContent; // Include the content
                    }
                }
                else
                {
                    return blockContent; // Include if parameter doesn't exist
                }

                return string.Empty; // Exclude the content
            });

            return processedContent;
        }

        private string ProcessFileNamePlaceholders(string fileName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(fileName) || parameters == null)
                return fileName;

            var result = fileName;

            // First, process template placeholders
            foreach (var parameter in parameters)
            {
                // For file names, we use simple word replacement without curly braces
                // since curly braces are not valid in Windows file names
                result = result.Replace(parameter.Key, parameter.Value?.ToString() ?? string.Empty);
            }

            // Then, restore file extensions by removing trailing underscores
            result = RestoreFileExtensions(result);

            return result;
        }

        /// <summary>
        /// Restores original file extensions by removing trailing underscores from filenames.
        /// This allows template files to be stored with underscore suffixes to avoid compilation issues.
        /// Examples: MainLayout.razor.css_ → MainLayout.razor.css, appsettings.json_ → appsettings.json
        /// </summary>
        private string RestoreFileExtensions(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;

            // Check if the filename (not the full path) ends with an underscore
            var fileName = Path.GetFileName(filePath);
            if (fileName.EndsWith("_"))
            {
                // Remove the trailing underscore from the filename
                var restoredFileName = fileName.Substring(0, fileName.Length - 1);

                // Reconstruct the full path with the restored filename
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory))
                {
                    return restoredFileName;
                }
                else
                {
                    return Path.Combine(directory, restoredFileName);
                }
            }

            return filePath;
        }

        private FileType DetermineFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".cs" => FileType.CSharpCode,
                ".razor" => FileType.RazorComponent,
                ".csproj" => FileType.ProjectFile,
                ".sln" => FileType.SolutionFile,
                ".json" => FileType.JsonConfig,
                _ => FileType.Other
            };
        }

        public void Dispose()
        {
            // Nothing to dispose in the simple file-based approach
        }
    }
}
