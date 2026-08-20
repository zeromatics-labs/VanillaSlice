using ZKnow.VanillaStudio.Models;

namespace ZKnow.VanillaStudio.Services
{
    public class TemplateBasedServerDataGenerator
    {
        private readonly TemplateEngineService _templateEngine;
        private readonly ILogger<TemplateBasedServerDataGenerator> _logger;

        public TemplateBasedServerDataGenerator(TemplateEngineService templateEngine, ILogger<TemplateBasedServerDataGenerator> logger)
        {
            _templateEngine = templateEngine;
            _logger = logger;
        }

        public async Task<List<GeneratedFile>> GenerateServerDataProjectAsync(ProjectConfiguration config)
        {
            try
            {
                _logger.LogInformation("🏗️ Generating Server.Data project using templates...");

                var parameters = new Dictionary<string, object>
                {
                    ["RootNamespace"] = $"{config.ProjectName}.Server.Data",
                    ["ProjectName"] = config.ProjectName,
                    ["TargetFramework"] = config.TargetFramework,
                    ["AspNetCoreVersion"] = config.AspNetCoreVersion,
                    ["NpgsqlVersion"] = config.NpgsqlVersion,
                    ["IncludeAuthentication"] = config.IncludeAuthentication,
                    ["IncludeSampleData"] = config.IncludeSampleData,
                    ["DatabaseProvider"] = config.DatabaseProvider.ToString(),
                    ["EmailFromAddress"] = config.EmailFromAddress
                };

                var generatedFiles = await _templateEngine.GenerateFromTemplateAsync(
                    "ServerData", 
                    parameters, 
                    $"{config.ProjectName}.Common/{config.ProjectName}.Server.Data");

                // Filter files based on configuration
                var filteredFiles = FilterFilesByConfiguration(generatedFiles, config);

                _logger.LogInformation("✅ Generated {Count} Server.Data files using templates", filteredFiles.Count);
                return filteredFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to generate Server.Data project using templates");
                throw;
            }
        }

        private List<GeneratedFile> FilterFilesByConfiguration(List<GeneratedFile> files, ProjectConfiguration config)
        {
            var filteredFiles = new List<GeneratedFile>();

            foreach (var file in files)
            {
                // Always include project file and services
                if (file.RelativePath.EndsWith(".csproj") ||
                    file.RelativePath.Contains("Services/"))
                {
                    filteredFiles.Add(file);
                    continue;
                }

                // The checked-in Migrations/ snapshot is hand-authored against SQL Server
                // column types (nvarchar, etc.) and calls SqlServerModelBuilderExtensions /
                // SqlServerPropertyBuilderExtensions, which only exist when the SQL Server
                // EF provider package is referenced. Shipping it for any other provider is
                // a compile error, so only include it when SqlServer was selected.
                if (file.RelativePath.Contains("Migrations/") || file.RelativePath.Contains("Migrations\\"))
                {
                    if (config.DatabaseProvider == DatabaseProvider.SqlServer)
                    {
                        filteredFiles.Add(file);
                    }
                    continue;
                }

                // Include ApplicationUser only if authentication is enabled
                if (file.RelativePath.Contains("ApplicationUser.cs"))
                {
                    if (config.IncludeAuthentication)
                    {
                        filteredFiles.Add(file);
                    }
                    continue;
                }

                // Include Product entity only if sample data is enabled
                if (file.RelativePath.Contains("Product.cs"))
                {
                    if (config.IncludeSampleData)
                    {
                        filteredFiles.Add(file);
                    }
                    continue;
                }

                // Include AppDbContext with conditional content
                if (file.RelativePath.Contains("AppDbContext.cs"))
                {
                    // Modify the content based on configuration
                    file.Content = ModifyAppDbContextContent(file.Content, config);
                    filteredFiles.Add(file);
                    continue;
                }

                // Include all other files
                filteredFiles.Add(file);
            }

            return filteredFiles;
        }

        private string ModifyAppDbContextContent(string content, ProjectConfiguration config)
        {
            // This is a simple approach - in a more sophisticated system, 
            // you might use conditional compilation or multiple template variants
            
            if (!config.IncludeAuthentication)
            {
                // Replace IdentityDbContext with DbContext
                content = content.Replace("using Microsoft.AspNetCore.Identity.EntityFrameworkCore;", "");
                content = content.Replace("IdentityDbContext<ApplicationUser>", "DbContext");
            }

            if (!config.IncludeSampleData)
            {
                // Remove Product DbSet and configuration
                content = content.Replace("// Sample DbSet - replace with your actual entities\n        public DbSet<Product> Products { get; set; }", "");
                content = content.Replace(@"            // Configure your entities here
            modelBuilder.Entity<Product>(entity =>
            { 
                entity.Property(e => e.Price).HasColumnType(""decimal(18,2)"");
            });", "");
            }

            return content;
        }
    }
}
