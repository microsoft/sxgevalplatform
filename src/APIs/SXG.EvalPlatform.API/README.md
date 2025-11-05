# SXG Evaluation Platform API

This is the main Web API for the SXG Evaluation Platform, built with ASP.NET Core 8.0.

## 📚 Complete Documentation

For comprehensive documentation, see:
- **[API Complete Guide](../../docs/SXG_Evaluation_Platform_API_Complete_Guide.md)** - Full API reference
- **[Technical Implementation Guide](../../docs/Technical_Implementation_Guide.md)** - Architecture and development details

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK
- Azure Storage Account
- Azure Active Directory tenant

### Running the Application

```bash
# Navigate to project directory
cd src/Sxg-Eval-Platform-Api

# Restore dependencies
dotnet restore

# Run the application
dotnet run
```

### Access Points
- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/api/v1/health

## 🔧 Features

- RESTful API endpoints for evaluation management
- Swagger/OpenAPI documentation  
- Health check endpoints
- Azure AD authentication
- Azure Storage integration
- Structured logging
- Comprehensive error handling

## 📁 Project Structure

```
Controllers/        # API controllers
├── EvalRunController.cs
├── EvalConfigController.cs
└── ...

RequestHandlers/   # Business logic and request processing
Models/            # Data models and DTOs
archive/           # Legacy code (Services, unused models)
Properties/        # Launch settings
deploy/           # Deployment scripts and guides
```

## 🏗️ Architecture

- **Authentication**: Azure Active Directory OAuth
- **Storage**: Azure Table Storage + Blob Storage
- **Framework**: ASP.NET Core 8.0
- **Documentation**: Swagger/OpenAPI

## 📖 Related Documentation

- **[Project Root Documentation](../../API_Documentation.md)** - Overview and quick start
- **[Setup Guides](../../docs/)** - Configuration and deployment guides

---

*For detailed API usage, authentication setup, and technical implementation details, please refer to the comprehensive documentation linked above.*
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

4. Open your browser and navigate to `https://localhost:7000` to view the Swagger documentation.

## API Endpoints

### Health Checks
- `GET /api/v1/health` - Basic health check
- `GET /api/v1/health/detailed` - Detailed health information

### Evaluations
- `GET /api/v1/evaluation` - Get all evaluations
- `GET /api/v1/evaluation/{id}` - Get evaluation by ID
- `POST /api/v1/evaluation` - Create new evaluation
- `PUT /api/v1/evaluation/{id}` - Update evaluation
- `DELETE /api/v1/evaluation/{id}` - Delete evaluation

### Evaluation Configurations
- `GET /api/v1/eval/configurations` - Get default metric configuration
- `POST /api/v1/eval/configurations` - Create or save evaluation configuration
- `GET /api/v1/eval/configurations/{agentId}` - Get all configurations for an agent
- `GET /api/v1/eval/configurations/details/{configId}` - Get configuration by ID

## Configuration

The application can be configured through:
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development-specific settings
- Environment variables

## Project Structure

```
Controllers/        # API controllers
├── EvalRunController.cs
├── EvalConfigController.cs
└── HealthController.cs

RequestHandlers/   # Business logic and request processing
├── EvalRunRequestHandler.cs
├── EvaluationResultRequestHandler.cs
└── MetricsConfigurationRequestHandler.cs

Models/            # Data models and DTOs
└── (Various DTO models)

archive/           # Legacy code (Services, unused models)
Properties/        # Project configuration
└── launchSettings.json
```

## Development Notes

- Uses Azure Table Storage and Blob Storage for persistent data
- RequestHandlers pattern provides business logic separation from controllers  
- Storage services provide abstraction layer over Azure SDK clients
- Authentication uses Azure Active Directory OAuth tokens
- Additional validation and business rules can be added as needed