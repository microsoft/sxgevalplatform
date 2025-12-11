# SXG Evaluation Platform API

This is the main Web API for the SXG Evaluation Platform, built with ASP.NET Core 8.0.

## ?? Documentation

All comprehensive documentation has been moved to the central `docs` folder:

### For API Consumers
- **[API Consumer Guide](../../docs/API-Consumer-Guide.md)** - Complete guide for developers consuming the API
- **[Quick Deploy Dev Guide](../../docs/Quick-Deploy-Dev-Guide.md)** - Quick reference for deploying to development

### For Developers
- **[Developer Onboarding Guide](../../docs/Developer-Onboarding-Guide.md)** - Complete onboarding for new team members
- **[API Project README](../../docs/API-Project-README.md)** - Project-specific information and structure

### Architecture & Setup
- **[Cache Management Guide](../../docs/Cache-Management-Guide.md)** - Redis and Memory cache usage
- **[Redis Managed Identity Setup](../../docs/Redis-Managed-Identity-Setup.md)** - Azure AD authentication setup
- **[Redis Shared Cache Architecture](../../docs/Redis-Shared-Cache-Architecture.md)** - Shared cache architecture

### Deployment
- **[Development Deployment Review](../../docs/Development-Deployment-Review-Summary.md)** - Complete deployment review
- **[Feature Flags Configuration](../../docs/FeatureFlags-Deployment-Configuration.md)** - Feature flag setup

### All Documentation
- **[Documentation Index](../../docs/README-Documentation-Index.md)** - Complete navigation guide

## ?? Quick Start

### Prerequisites
- .NET 8.0 SDK
- Azure Storage Account
- Azure Active Directory tenant

### Running Locally

```bash
# Navigate to project directory
cd src/APIs/SXG.EvalPlatform.API

# Restore dependencies
dotnet restore

# Run the application
dotnet run
```

### Access Points
- **API**: http://localhost:5000
- **HTTPS**: https://localhost:5001
- **Swagger UI**: https://localhost:5001/swagger
- **Health Check**: https://localhost:5001/api/v1/health

## ?? Project Structure

```
Controllers/     # API controllers
RequestHandlers/# Business logic and request processing
Models/     # Data models and DTOs
Services/        # OpenTelemetry and telemetry services
Middleware/  # Custom middleware (telemetry)
Extensions/         # Service registration extensions
deploy/        # Deployment scripts
```

## ?? Key Features

- RESTful API endpoints for evaluation management
- Swagger/OpenAPI documentation
- Comprehensive health checks with dependency monitoring
- Azure AD authentication
- Azure Storage integration (Tables, Blobs, Queues)
- Redis distributed caching
- OpenTelemetry and Application Insights integration
- Feature flags for runtime configuration

## ?? More Information

For detailed documentation on architecture, development, deployment, and usage, please refer to the comprehensive guides in the `docs` folder.

---

**Quick Links:**
- [Get Started with API](../../docs/API-Consumer-Guide.md#getting-started)
- [Developer Setup](../../docs/Developer-Onboarding-Guide.md#development-environment-setup)
- [Deploy to Azure](../../docs/Quick-Deploy-Dev-Guide.md)
