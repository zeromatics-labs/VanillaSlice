
# VanillaSlice Framework

VanillaSlice is a .NET-first framework that blends Clean Architecture with Vertical Slices to enforce SOLID by design. It organizes your app into thin, feature-centric slices that encapsulate everything—UI, contracts, domain logic, and data—so each slice owns its responsibilities end-to-end. With VanillaStudio (bootstrapper) and SliceFactory, you can scaffold CRUD features across Blazor Web and MAUI Hybrid using Bootstrap 5 or Fluent UI out of the box. The result is a maintainable codebase with fewer regressions, faster delivery, and the agility to absorb .NET evolution—ready for team scale and AI co-authoring.

# VanillaStudio

🚀 A comprehensive .NET project generator that creates enterprise-ready applications with multiple platform support, modern UI frameworks, and integrated services. Along with projects, a SliceFactory is generated as per selected options to generated feature slice everytime you need a new featrue. [Access online hosted version here](https://vanillaslice.dev/)

# SliceFactory
SliceFactory generates end-to-end feature slices—Listing and Form—each containing Razor/XAML UI, ViewModels, business models, a typed client service (via HttpClient), a server-side controller, and a server-side service with the actual business logic and queries. All code is plain C# and SOLID by default: the factory enforces structure through interfaces and generates safe, empty stubs so you focus on domain logic, not plumbing. Everything comes pre-wired (DI, routing, endpoints). Just add fields/properties to your models and UI, implement the query/commands, and your slice is ready.

## Key Features

### ✅ **Platforms Support**
- **Blazor Web Applications** - Modern web UI with server-side and WebAssembly support
- **MAUI Hybrid Apps** - Hybrid mobile applications using shared razor components
- **MAUI Native Apps** - Native mobile applications using XAML frontend and shared backend (coming soon)
- **Web API Integration** - RESTful API services with OpenAPI documentation
- **Entity Framework Core** - Database integration with multiple provider support
- **Aspire Orchestration Support** - Modern cloud-native application orchestration
- **Docker Support** - Containerization ready configurations (coming soon)


### ✅ **UI Framework Support**
- **Bootstrap 5** - ✅ **Fully Implemented** - Default responsive framework
- **Microsoft Fluent UI** - ✅ **Fully Implemented**  - Microsoft's design system
- **MudBlazor** - 🔄 **In Progress** - Material Design for Blazor
- **Radzen Components** - 🔄 **In Progress** - Rich component library
- **Tailwind CSS** - ✅ **Fully Implemented**  - Utility-first CSS framework

### ✅ **Database Support**
- **SQL Server** - ✅ **Fully Implemented** — EF Core + Identity + migrations
- **SQLite** - ✅ **Fully Implemented** — lightweight dev/test option
- **PostgreSQL** - ✅ **Fully Implemented** — open-source production option
- **No Database Option** - ✅ **Fully Implemented** — API-only or external data sources

### ✅ **Authentication & Security**
- **Identity Integration** - ✅ **Fully Implemented**
- **Authorization Policies** - ✅ **Fully Implemented**
- **JWT Token Support** - ✅ **Fully Implemented**

## Implementation Status Matrix

| Feature | Status | Notes |
|---------|--------|-------|
| **Core Platform** |
| Web Application | ✅ **Complete** | Blazor Server/WebAssembly hybrid |
| Hybrid MAUI App | ✅ **Complete** | Hybrid MAUI from shared code base |
| Web API | ✅ **Complete** | RESTful services with OpenAPI |
| MAUI Native App | ✅ **Fully Implemented**  | Cross-platform mobile support |
| **UI Frameworks** |
| Bootstrap 5 | ✅ **Complete** | Default implementation |
| Microsoft Fluent UI | ✅ **Complete**  | Blazor integration underway |
| MudBlazor | 🔄 **In Progress** | Material Design components |
| Radzen Components | 🔄 **In Progress** | Rich UI component library |
| Tailwind CSS | ✅ **Complete**  | Utility-first CSS integration |
| **Services & Features** |
| Authentication | ✅ **Complete** | Identity with JWT support |
| Database Integration | ✅ **Complete** | EF Core with multiple providers |
| Dialog Services | ✅ **Complete** | DialogService + DialogContainer generated in Framework project |
| Notification Services | 🔄 **In Progress** | Toast and alert systems |
| **Advanced Features** |
| Push Notifications | 📅 **TBD** | Mobile and web push support |
| Offline Data Access | 📅 **TBD** | Local storage and sync |
| Real-time Chat | 📅 **TBD** | SignalR chat implementation |
| File Upload/Management | 📅 **TBD** | Blob storage integration |
| Reporting Services | 📅 **TBD** | PDF and Excel generation |

## Quick Start

### Prerequisites
- .NET 9.0 SDK or .NET 10.0 SDK (select your target version in the wizard)
- Visual Studio 2022 (17.12+) or VS Code with C# Dev Kit
- SQL Server (optional — SQLite available for development)

### Running the VanillaStudio

1. **Clone the repository:**
   ```bash
   git clone https://github.com/zero-know/VanillaSlice.git
   cd src
   ```

2. **Run the application:**
   ```bash
   cd VanillaStudio
   dotnet run
   ```

3. **Access the web interface:**
   - Open your browser to `https://localhost:5001`
   - Use the Project Wizard to configure your application

### Using the Project Wizard

1. **Project Configuration**
   - Enter your project name and root namespace
   - Choose your target platform (Web-only or Web + MAUI)
   - Select component strategy (Common library or Embedded)

2. **Platform Selection**
   - **Web Only**: Creates Blazor web application with API
   - **Web + MAUI**: Adds cross-platform mobile app support

3. **UI Framework Selection**
   - Choose from Bootstrap, Fluent UI, MudBlazor, Radzen, or Tailwind
   - Framework-specific components and styling will be configured

4. **Database Configuration**
   - Select database provider (SQL Server, PostgreSQL, SQLite, or None)
   - Configure connection strings and Entity Framework settings

5. **Additional Features**
   - Authentication and authorization setup
   - Sample components and data
   - Docker and Aspire orchestration support

6. **Generate Project**
   - Click "Generate Project" to create your solution
   - Download the generated ZIP file
   - Extract and open in your preferred IDE

## Generated Project Structure

```
YourProject/
├── src/
│   ├── YourProject.WebAPI/              # REST API services
│   ├── YourProject.WebPortal/           # Blazor web application
│   ├── YourProject.WebPortal.Client/    # Client-side components
│   ├── YourProject.HybridApp/           # MAUI hybrid app
│   ├── YourProject.Server.Data/         # Data access layer
│   ├── YourProject.Server.DataServices/ # Business logic services
│   ├── YourProject.ServiceContracts/    # Shared contracts
│   ├── YourProject.Common/              # Shared utilities
│   ├── YourProject.Client.Shared/       # Client shared components
│   └── YourProject.Framework/           # Core framework
├── YourProject.AppHost/                 # Aspire orchestration
├── YourProject.ServiceDefaults/         # Default configurations
└── YourProject.sln                      # Solution file
```

## Architecture Overview

The generated solutions follow **Clean Architecture** principles:

- **Presentation Layer**: Blazor components, Web API controllers
- **Application Layer**: Services, business logic, contracts
- **Infrastructure Layer**: Data access, external services
- **Domain Layer**: Entities, value objects, domain services

## Database Integration

### Supported Providers
- **SQL Server**: Production-ready with advanced features
- **PostgreSQL**: Open-source alternative with full feature support
- **SQLite**: Development and lightweight deployment scenarios
- **No Database**: In-memory or external data source integrations

> All four providers are fully selectable in the project wizard and generate correct EF Core configuration, migrations support, and connection string setup.

### Entity Framework Features
- Code-first migrations
- Seed data configuration
- Repository pattern implementation
- Unit of work pattern
- CRUD operations with validation

## UI Framework Integration

### Bootstrap 5 (Default)
- Responsive grid system
- Modern components
- Dark/light theme support
- Custom component implementations

### Framework-Specific Features (In Progress)
- **Fluent UI**: Microsoft design system integration
- **MudBlazor**: Material Design components with theming
- **Radzen**: Rich data grids, charts, and form components
- **Tailwind**: Utility-first styling with custom components

## Contributing

We welcome contributions! Please see our contributing guidelines and help us expand the framework support and add new features.

### Priority Areas
- UI framework implementations (MudBlazor, Radzen, Tailwind, Fluent UI)
- MAUI native app templates
- Dialog and notification services
- Advanced features (push notifications, offline sync, real-time chat)

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Support

- **Issues**: Report bugs and request features on GitHub Issues
- **Documentation**: Full documentation available in the `/docs` folder
- **Community**: Join our discussions for help and contributions

---

**Status Legend:**
- ✅ **Complete**: Fully implemented and tested
- 🔄 **In Progress**: Currently under development
- 📅 **TBD**: Planned for future releases
