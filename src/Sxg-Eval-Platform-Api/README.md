# SXG Evaluation Platform API

This is the main Web API for the SXG Evaluation Platform, built with ASP.NET Core 8.0.

## Features

- RESTful API endpoints for evaluation management
- Swagger/OpenAPI documentation
- Health check endpoints
- CORS support
- Structured logging
- Error handling and validation

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code

### Running the Application

1. Navigate to the project directory:
   ```bash
   cd "D:\Projects\sxg-eval-platform\src\Sxg-Eval-Platform-Api"
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
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
├── BaseController.cs
├── EvaluationController.cs
└── HealthController.cs

Models/            # Data models and DTOs
└── EvaluationModels.cs

Services/          # Business logic services
├── IEvaluationService.cs
└── EvaluationService.cs

Properties/        # Project configuration
└── launchSettings.json
```

## Development Notes

- The current implementation uses in-memory storage for simplicity
- Entity Framework integration can be added for persistent storage
- Authentication and authorization can be implemented using JWT tokens
- Additional validation and business rules can be added as needed