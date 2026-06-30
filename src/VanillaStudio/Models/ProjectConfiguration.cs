using System.ComponentModel.DataAnnotations;

namespace ZKnow.VanillaStudio.Models
{
    public class ProjectConfiguration
    {
        [Required]
        [Display(Name = "Project Name")]
        public string ProjectName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Root Namespace")]
        public string RootNamespace { get; set; } = string.Empty;

        [Display(Name = "Output Directory")]
        public string OutputDirectory { get; set; } = string.Empty;

        [Display(Name = "Project Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Author Name")]
        public string AuthorName { get; set; } = string.Empty;

        // Platform Selection
        public PlatformType PlatformType { get; set; } = PlatformType.WebAndMaui;

        // Individual Platform Flags (new checkbox approach)
        public bool IncludeWebProject { get; set; } = true;
        public bool IncludeHybridMaui { get; set; } = true;
        public bool IncludeMauiNative { get; set; } = false;

        // MAUI Navigation Configuration
        public MauiNavigationType MauiNavigationType { get; set; } = MauiNavigationType.Tabs;

        // Razor Component Strategy
        public ComponentStrategy ComponentStrategy { get; set; } = ComponentStrategy.CommonLibrary;

        // Rendering Mode
        public RenderingMode RenderingMode { get; set; } = RenderingMode.Auto;

        // Additional Features
        public bool IncludeAuthentication { get; set; } = true;
        public bool IncludeDatabase { get; set; } = true;
        public bool IncludeApiControllers { get; set; } = true;
        public bool IncludeSampleComponents { get; set; } = true;
        public bool IncludeSampleData { get; set; } = true;

        // Database Configuration
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
        public string ConnectionStringName { get; set; } = "DefaultConnection";

        // UI Framework Selection
        public UIFramework UIFramework { get; set; } = UIFramework.Bootstrap;

        // .NET Version
        public DotNetVersion DotNetVersion { get; set; } = DotNetVersion.Net10;

        // Advanced Options
        public bool UseAspireOrchestration { get; set; } = true;
        public bool IncludeDockerSupport { get; set; } = false;
        public bool IncludeTestProjects { get; set; } = false;

        // Computed version strings derived from DotNetVersion
        public string TargetFramework       => DotNetVersion == DotNetVersion.Net9 ? "net9.0"  : "net10.0";
        public string AspNetCoreVersion     => DotNetVersion == DotNetVersion.Net9 ? "9.0.8"   : "10.0.0";
        public string MauiVersion           => DotNetVersion == DotNetVersion.Net9 ? "9.0.10"  : "10.0.0";
        public string MauiTargetFrameworks  => DotNetVersion == DotNetVersion.Net9
            ? "net9.0-android;net9.0-ios;net9.0-maccatalyst"
            : "net10.0-android;net10.0-ios;net10.0-maccatalyst";
        public string MauiWindowsTarget     => DotNetVersion == DotNetVersion.Net9
            ? "net9.0-windows10.0.19041.0"
            : "net10.0-windows10.0.19041.0";

        // Aspire versioning: 9.x targets .NET 9, 13.x targets .NET 10 (independent versioning scheme)
        public string AspireVersion => DotNetVersion == DotNetVersion.Net9 ? "9.3.1" : "13.0.0";

        // ServiceDefaults package versions — Microsoft.Extensions follows .NET versioning; OTel is independent
        public string ResiliencePackageVersion      => DotNetVersion == DotNetVersion.Net9 ? "9.4.0"  : "10.0.0";
        public string ServiceDiscoveryVersion       => DotNetVersion == DotNetVersion.Net9 ? "9.3.1"  : "10.0.0";
        public string OpenTelemetryVersion          => "1.12.0";

        // AppHost csproj SDK section differs between Aspire 9.x and 13.x.
        // Net9: two-element format (Microsoft.NET.Sdk base + Aspire SDK element).
        // Net10/Aspire13: single SDK attribute on <Project> tag; hosting package auto-included by SDK.
        public string AspireProjectOpenTag => DotNetVersion == DotNetVersion.Net9
            ? "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n\r\n  <Sdk Name=\"Aspire.AppHost.Sdk\" Version=\"9.3.1\" />"
            : "<Project Sdk=\"Aspire.AppHost.Sdk/13.0.0\">";

        public string AspirePackageGroup => DotNetVersion == DotNetVersion.Net9
            ? "  <ItemGroup>\r\n    <PackageReference Include=\"Aspire.Hosting.AppHost\" Version=\"9.3.1\" />\r\n  </ItemGroup>"
            : "";

        public ProjectConfiguration()
        {
#if DEBUG
            ProjectName = "ZKnowledge.Enterprise";
            RootNamespace = "ZKnowledge.Enterprise";
            OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ZKnowledge.Enterprise");
#else
            // Set default output directory to current directory + project name
            if (!string.IsNullOrEmpty(ProjectName))
            {
                OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), ProjectName);
            }
#endif
        }
    }

    public enum DotNetVersion
    {
        [Display(Name = ".NET 9 (STS)")]
        Net9 = 1,

        [Display(Name = ".NET 10 (LTS)")]
        Net10 = 2
    }

    public enum PlatformType
    {
        [Display(Name = "Web Application Only")]
        WebOnly = 1,

        [Display(Name = "Web Application + MAUI Mobile App")]
        WebAndMaui = 2
    }

    public enum ComponentStrategy
    {
        [Display(Name = "Common Razor Library (Reusable across projects)")]
        CommonLibrary = 1,

        [Display(Name = "Embedded Components (Directly in WebPortal project)")]
        Embedded = 2
    }

    public enum RenderingMode
    {
        [Display(Name = "Auto Render (Server + WebAssembly hybrid)")]
        Auto = 1,

        [Display(Name = "Server-Side Interactive Only")]
        ServerOnly = 2,

        [Display(Name = "Static Server-Side Rendering (SSR) Only")]
        StaticSSR = 3
    }

    public enum DatabaseProvider
    {
        [Display(Name = "SQL Server")]
        SqlServer = 1,

        [Display(Name = "SQLite")]
        SQLite = 2,

        [Display(Name = "PostgreSQL")]
        PostgreSQL = 3,

        [Display(Name = "No Database")]
        None = 4
    }

    public enum UIFramework
    {
        [Display(Name = "Bootstrap 5 (Default)")]
        Bootstrap = 1,

        [Display(Name = "Microsoft Fluent UI")]
        FluentUI = 2,

        [Display(Name = "MudBlazor (Material Design)")]
        MudBlazor = 3,

        [Display(Name = "Radzen Blazor Components")]
        Radzen = 4,

        [Display(Name = "Tailwind CSS")]
        TailwindCSS = 5
    }

    public enum MauiNavigationType
    {
        [Display(Name = "Tab Navigation")]
        Tabs = 1,

        [Display(Name = "Flyout Navigation")]
        Flyout = 2
    }

    public class ProjectGenerationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? DownloadUrl { get; set; }
        public string? ProjectPath { get; set; }
        public List<GeneratedFile> GeneratedFiles { get; set; } = new();
        public byte[]? ZipData { get; set; }
        public string? ZipFileName { get; set; }
    }

    public class GeneratedFile
    {
        public string RelativePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public FileType Type { get; set; }
    }

    public enum FileType
    {
        CSharpCode,
        ProjectFile,
        SolutionFile,
        RazorComponent,
        JsonConfig,
        Other
    }
}
