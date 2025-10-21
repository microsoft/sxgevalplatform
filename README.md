# SXG Evaluation Platform

A comprehensive .NET 8 API platform for managing AI model evaluations, datasets, metrics configurations, and results storage using Azure services.

## Overview

The SXG Evaluation Platform provides a RESTful API that enables organizations to:
- Create and manage evaluation runs for AI models
- Store and retrieve evaluation datasets (Golden and Synthetic types)
- Configure metrics for different evaluation scenarios
- Save and analyze evaluation results with flexible JSON structures
- Monitor evaluation progress and maintain audit trails

## Architecture

### Core Components
- **API Layer**: .NET 8 Web API with Swagger documentation
- **Storage Layer**: Azure Table Storage for metadata, Azure Blob Storage for datasets and results
- **Authentication**: OAuth using Azure Active Directory
- **Data Organization**: Agent-based partitioning for multi-tenant scenarios

### Key Features
- ✅ **Case Insensitive Status Updates**: Flexible status handling (e.g., "completed", "COMPLETED", "Completed")
- ✅ **Terminal State Protection**: Immutable states once evaluation reaches "Completed" or "Failed"
- ✅ **Folder-Based Result Storage**: Organized storage structure `evalresults/{evalrunid}/`
- ✅ **RESTful Design**: Clean API following REST principles
- ✅ **Comprehensive Error Handling**: Detailed error responses with actionable messages
- ✅ **Multi-File Support**: Store multiple output files per evaluation run

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Azure Storage Account
- Azure Active Directory tenant

### Run Locally
```bash
cd src/Sxg-Eval-Platform-Api
dotnet restore
dotnet run
```

Access the API at: `http://localhost:5000`  
Swagger UI: `http://localhost:5000/swagger`

### API Health Check
```bash
curl -X GET https://your-api-domain.com/api/v1/health
```

## API Documentation

📖 **[Complete API Documentation](./docs/API_Documentation.md)**

The comprehensive API documentation includes:
- **Quick Start Guide**: Get up and running in minutes
- **Authentication Setup**: Azure AD integration details
- **All Endpoints**: Detailed documentation with examples
- **Data Models**: Complete schema definitions
- **Error Handling**: Common scenarios and solutions
- **Best Practices**: Performance, security, and integration guidance
- **Integration Examples**: Ready-to-use code samples

### Key API Endpoints

| Endpoint | Method | Description |
|----------|---------|-------------|
| `/api/v1/health` | GET | API health status |
| `/api/v1/eval/runs` | POST | Create evaluation run |
| `/api/v1/eval/runs/{id}` | PUT | Update evaluation status |
| `/api/v1/eval/runs/{id}` | GET | Get evaluation details |
| `/api/v1/eval/results` | POST | Save evaluation results |
| `/api/v1/eval/results/{id}` | GET | Get evaluation results |
| `/api/v1/datasets` | GET/POST | Manage datasets |
| `/api/v1/eval/defaultconfiguration` | GET | Get metrics configuration |

## Project Structure

```
sxgevalplatform/
├── src/
│   └── Sxg-Eval-Platform-Api/          # Main API project
│       ├── Controllers/                 # API controllers
│       ├── RequestHandlers/            # Business logic and request processing
│       ├── Models/                     # Data models and DTOs
│       └── archive/                    # Legacy code (Services, unused models)
├── Sxg.EvalPlatform.API.Storage/       # Storage layer
│   ├── Services/                       # Azure storage services
│   ├── Entities/                       # Table entities
│   └── TableEntities/                  # Storage models
├── SXG.EvalPlatform.Common/            # Shared utilities
├── docs/                               # Documentation
│   ├── API_Documentation_Consolidated.md
│   ├── Case_Insensitive_Status_Updates.md
│   └── API_Endpoint_Impact_Analysis.md
└── deploy/                             # Deployment scripts
```

## Configuration

### Azure Storage
```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account"
  }
}
```

### Authentication
```json
{
  "Authentication": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
  }
}
```

## Storage Architecture

### Table Storage Partitioning
- **Partition Key**: `AgentId` for optimal performance
- **Row Key**: `EvalRunId` for unique identification
- **Benefits**: Efficient agent-based queries and load distribution

### Blob Storage Organization
- **Container**: Agent-specific containers (lowercase agent IDs)
- **Structure**: `evalresults/{evalrunid}/{filename}`
- **Support**: Multiple files per evaluation run

## Business Rules

### Status Management
- **Valid States**: `Queued`, `Running`, `Completed`, `Failed`
- **Case Insensitive**: Accept any case variation, normalize to Pascal case
- **Terminal Protection**: `Completed` and `Failed` states cannot be updated
- **State Transitions**: Controlled workflow with validation

### Data Integrity
- Agent-based data isolation
- Immutable evaluation results once saved
- Comprehensive audit trails with timestamps

## Development

### Building
```bash
dotnet build src/Sxg-Eval-Platform-Api/SXG.EvalPlatform.API.csproj
```

### Testing
```bash
dotnet test
```

### Deployment
See deployment scripts in the `deploy/` directory for Azure deployment guidance.

## Monitoring

### Health Endpoints
- `/api/v1/health` - Basic health check
- Structured logging throughout the application
- Azure Application Insights integration ready

### Key Metrics
- Evaluation run completion rates
- API response times
- Authentication success rates
- Storage operation performance

## Contributing

1. Follow .NET coding standards
2. Include comprehensive XML documentation
3. Add unit tests for new functionality
4. Update API documentation for endpoint changes
5. Ensure backward compatibility

## Documentation

- **[API Documentation](./docs/API_Documentation.md)** - Complete API reference
- **[Case Insensitive Updates](./docs/Case_Insensitive_Status_Updates.md)** - Status handling details
- **[Endpoint Impact Analysis](./docs/API_Endpoint_Impact_Analysis.md)** - Change impact documentation

## Support

- **Swagger UI**: Available at `/swagger` endpoint
- **Health Check**: Monitor API status at `/api/v1/health`
- **Logs**: Structured logging for debugging and monitoring

## License

Microsoft Corporation. All rights reserved.

