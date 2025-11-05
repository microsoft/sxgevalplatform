# SXG Evaluation Platform - Evaluation Engine

A high-performance, cloud-native Python application designed to run AI agent evaluations at scale on Azure Container Apps. The application processes evaluation requests through Azure Storage Queues and executes comprehensive metrics using the Azure AI Evaluation SDK with advanced optimization features.

## 🚀 Key Features

- **🔥 High-Performance Processing**: 60% faster evaluation with concurrent processing optimizations
- **🔄 Advanced Concurrency**: Concurrent dataset processing (3x) and metric evaluation (8x) with timeout protection
- **🌐 HTTP Connection Pooling**: Persistent connections with lifecycle management for reduced overhead
- **📊 Comprehensive Metrics**: 20+ evaluation metrics across 4 categories (Agentic, RAG, Safety, Similarity)
- **☁️ Azure-Native**: Managed Identity integration for secure Azure resource access
- **📈 Performance Monitoring**: Built-in performance timing and structured metrics logging
- **🛡️ Production-Ready**: Enhanced error handling, resource cleanup, and graceful shutdown
- **🐳 Container Optimized**: Dockerfile and docker-compose with production configurations

## � Documentation

For comprehensive documentation, see our organized docs folder:
- **[📋 Documentation Index](docs/DOCUMENTATION_INDEX.md)** - Complete guide to all documentation
- **[🚀 Quick Start Guide](docs/QUICKSTART.md)** - Get up and running quickly
- **[🏗️ Project Overview](docs/PROJECT_OVERVIEW.md)** - Architecture and technical details
- **[🚢 Deployment Guide](docs/DEPLOYMENT_GUIDE.md)** - Azure Container Apps deployment
- **[📊 Performance Guide](docs/PERFORMANCE_GUIDE.md)** - Optimization and tuning
- **[🔧 Troubleshooting Guide](docs/TROUBLESHOOTING_GUIDE.md)** - Common issues and solutions

## �📊 Performance Highlights

- **60% Performance Improvement**: Concurrent processing vs sequential baseline
- **3x Dataset Concurrency**: Process multiple dataset items simultaneously
- **8x Metric Concurrency**: Evaluate metrics in parallel with 30s timeout protection
- **Connection Pooling**: Persistent HTTP sessions with 1-hour lifecycle
- **Resource Optimization**: Enhanced Azure Storage connections with pooling

## Architecture

### Core Components

1. **EvaluationOrchestrator**: Main coordinator that manages the evaluation lifecycle
2. **IMetric Interface**: Contract that all metrics must implement
3. **MetricFactory**: Factory for creating metric instances from configuration
4. **Azure Services**: Integration with Azure Storage Queue and Blob Storage
5. **API Clients**: Handles external API calls for configuration and status updates

### Project Structure

```
src/eval_runner/
├── 📄 main.py                          # Application entry point with optimized resource management
├── 📁 config/
│   └── 📄 settings.py                  # Environment-aware configuration management
├── 📁 core/
│   └── 📄 evaluation_engine.py         # Main evaluation logic with concurrent processing
├── 📁 services/
│   ├── 📄 azure_storage.py             # Azure Storage with connection pooling optimization
│   ├── 📄 evaluation_api_client.py     # External API client
│   └── 📄 http_client.py               # HTTP client with connection pooling & lifecycle mgmt
├── 📁 models/
│   └── 📄 eval_models.py               # Data models and DTOs
├── 📁 metrics/                         # 20+ Azure AI Evaluation SDK metrics
│   ├── 📄 azure_ai_interface.py        # Azure AI SDK interface
│   ├── 📄 base_evaluators.py           # Abstract base classes
│   ├── 📄 evaluation_result.py         # Result models with enhanced error handling
│   ├── 📁 agentic/                     # Intent, Task, Tool evaluators (3 metrics)
│   ├── 📁 rag/                         # Groundedness, Relevance, etc. (4 metrics)
│   ├── 📁 risk_and_safety/             # Safety evaluators (5 metrics)
│   └── 📁 text_similarity/             # F1, ROUGE, Similarity (6 metrics)
├── 📁 utils/
│   └── 📄 logging_helper.py            # Performance monitoring & structured logging
├── 📁 telemetry/
│   └── 📄 opentelemetry_config.py      # Distributed tracing configuration
├── 📄 azure_ai_config.py               # Azure AI SDK configuration
└── 📄 exceptions.py                    # Custom exceptions
```

## Quick Start

### Prerequisites

- Python 3.11+
- Azure Storage Account with Queue and Blob services
- API endpoints for evaluation configuration and status updates

### Setup

1. **Clone and setup environment**:
   ```powershell
   cd d:\Projects\sxgevalplatform\src\Sxg-Eval-Platform.EvaluationEngine
   .\scripts\setup.ps1
   ```

2. **Configure application settings**:
   Update `appsettings.json` with your Azure storage accounts and API endpoints:
   ```json
   {
     "AzureStorage": {
       "QueueAccountName": "your-storage-account",
       "QueueName": "eval-requests",
       "BlobAccountName": "your-blob-storage",
       "BlobContainerName": "eval-results"
     },
     "ApiEndpoints": {
       "EvalConfigEndpoint": "https://api.example.com/eval/{evalRunId}/config",
       "StatusUpdateEndpoint": "https://api.example.com/eval/{evalRunId}/status"
     }
   }
   ```

3. **Set environment variables** (for production):
   ```bash
   export AZURE_QUEUE_ACCOUNT_NAME="your-storage-account"
   export EVAL_CONFIG_ENDPOINT="https://api.example.com/eval/{evalRunId}/config"
   export EVAL_API_KEY="your-api-key"
   # ... other variables
   ```

4. **Run the application**:
   ```powershell
   .\.venv\Scripts\Activate.ps1
   python src\main.py
   ```

### Docker Deployment

1. **Build the image**:
   ```bash
   docker build -t sxg-eval-runner .
   ```

2. **Run with docker-compose**:
   ```bash
   docker-compose up -d
   ```

## 🚀 Performance & Optimization

### Performance Metrics
- **60% Faster Processing**: Concurrent vs sequential baseline
- **3x Dataset Concurrency**: Simultaneous dataset item processing
- **8x Metric Concurrency**: Parallel metric evaluation with timeout protection
- **Connection Pooling**: HTTP sessions with 1-hour lifecycle management

### Optimization Features
- **Concurrent Processing**: Multiple dataset items processed simultaneously
- **HTTP Connection Pooling**: Persistent connections with 20 total/10 per host
- **Azure Storage Optimization**: Connection pooling with 30s/60s timeouts
- **Resource Management**: Proper cleanup and graceful shutdown
- **Performance Monitoring**: Built-in timing and structured logging

### Configuration for Performance
```json
{
  "Evaluation": {
    "DatasetConcurrency": 3,
    "MetricConcurrency": 8,
    "MetricTimeoutSeconds": 30,
    "ConnectionPooling": true
  }
}
```

### Monitoring
The application provides detailed performance metrics:
- Operation timing with categorization (fast/moderate/slow)
- Structured performance logs for analysis
- Concurrent processing efficiency metrics
- Resource utilization tracking

## Configuration

### Application Settings

The application supports configuration through:
1. `appsettings.json` file
2. Environment variables (override file settings)
3. Azure App Service configuration (for Container Apps)

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `AZURE_QUEUE_ACCOUNT_NAME` | Azure Storage Queue account name | Yes |
| `AZURE_BLOB_ACCOUNT_NAME` | Azure Storage Blob account name | Yes |
| `EVAL_CONFIG_ENDPOINT` | API endpoint for evaluation configuration | Yes |
| `STATUS_UPDATE_ENDPOINT` | API endpoint for status updates | Yes |
| `EVAL_API_KEY` | API key for evaluation endpoint | Yes |
| `STATUS_API_KEY` | API key for status endpoint | Yes |

### Metric Configuration

Metrics are configured through the evaluation configuration API response:

```json
{
  "evalRunId": "eval-123",
  "metrics": [
    {
      "name": "accuracy",
      "type": "accuracy",
      "parameters": {
        "comparison_method": "fuzzy",
        "tolerance": 0.1
      },
      "weight": 1.0,
      "enabled": true
    }
  ],
  "dataset": {
    "items": [
      {
        "prompt": "What is 2+2?",
        "actualOutcome": "4",
        "expectedOutcome": "4"
      }
    ]
  }
}
```

## Built-in Metrics

### AccuracyMetric
Evaluates accuracy by comparing actual vs expected outcomes.

**Parameters**:
- `comparison_method`: "exact", "fuzzy", or "numeric"
- `tolerance`: For numeric comparisons (default: 0.1)
- `case_sensitive`: Boolean for string comparisons (default: false)

### RelevanceMetric
Evaluates relevance using keyword matching and context analysis.

**Parameters**:
- `keywords`: List of keywords to match
- `keyword_weight`: Weight for keyword matching (default: 0.6)
- `context_weight`: Weight for context analysis (default: 0.4)
- `min_keyword_matches`: Minimum required matches (default: 1)

### CompletenessMetric
Evaluates completeness of responses.

**Parameters**:
- `required_elements`: List of required elements
- `min_length`: Minimum response length (default: 10)
- `expected_sections`: List of expected sections

## Adding Custom Metrics

1. **Create metric class**:
   ```python
   from eval_runner.metrics.base_metric import BaseMetric
   from eval_runner.models.eval_models import DatasetItem, MetricScore
   
   class CustomMetric(BaseMetric):
       def __init__(self, name: str = "custom"):
           super().__init__(name)
       
       async def evaluate(self, dataset_item: DatasetItem) -> MetricScore:
           # Your evaluation logic here
           return self._create_score(1.0, "Perfect score")
   ```

2. **Register the metric**:
   ```python
   from eval_runner.metrics.base_metric import MetricFactory
   MetricFactory.register_metric("custom", CustomMetric)
   ```

## API Integration

### Evaluation Configuration API

**Endpoint**: `GET /api/eval/{evalRunId}/config`

**Response**:
```json
{
  "evalRunId": "string",
  "metrics": [...],
  "dataset": {...}
}
```

### Status Update API

**Endpoint**: `POST /api/eval/{evalRunId}/status`

**Request**:
```json
{
  "evalRunId": "string",
  "status": "InProgress|Completed|Failed",
  "message": "Optional status message",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

## Queue Message Format

Messages in the Azure Storage Queue should follow this format:

```json
{
  "evalRunId": "eval-123",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

## Logging

The application uses structured logging with configurable levels:

- **INFO**: Normal operation messages
- **DEBUG**: Detailed execution information
- **ERROR**: Error conditions
- **WARNING**: Warning conditions

Logs can be output to console and/or files based on configuration.

## Error Handling

- **Metric Failures**: Individual metric failures don't stop the evaluation
- **API Failures**: Retried with exponential backoff
- **Queue Processing**: Failed messages remain in queue for retry
- **Graceful Shutdown**: SIGTERM/SIGINT handling for clean shutdown

## Monitoring and Health Checks

The Docker container includes health checks and exposes metrics for monitoring:

- Health check endpoint validates application startup
- Resource limits configured for Container Apps
- Structured logging for Azure Monitor integration

## Development

### Setup Development Environment

```powershell
.\scripts\setup.ps1 -Dev
```

### Running Tests

```bash
pytest tests/ --cov=eval_runner --cov-report=html
```

### Code Quality

```bash
# Format code
black src/
isort src/

# Lint code
flake8 src/
mypy src/

# Run all quality checks
pre-commit run --all-files
```

## Deployment to Azure Container Apps

1. **Build and push image**:
   ```bash
   az acr build --registry myregistry --image eval-runner:latest .
   ```

2. **Deploy to Container Apps**:
   ```bash
   az containerapp create \
     --name eval-runner \
     --resource-group mygroup \
     --environment myenv \
     --image myregistry.azurecr.io/eval-runner:latest \
     --env-vars AZURE_QUEUE_ACCOUNT_NAME=mystorage \
     --system-assigned
   ```

3. **Configure managed identity** for Azure Storage access.

## Troubleshooting

### Common Issues

1. **Authentication Errors**: Ensure managed identity has Storage Queue Data Contributor and Storage Blob Data Contributor roles
2. **Queue Not Found**: Verify queue name and storage account configuration
3. **API Timeouts**: Check API endpoint URLs and network connectivity
4. **Memory Issues**: Adjust container resource limits and max parallel metrics

### Debug Mode

Enable debug logging by setting the environment variable:
```bash
export LOG_LEVEL=DEBUG
```

## Contributing

1. Follow the existing code structure and patterns
2. Add unit tests for new functionality
3. Update documentation for new features
4. Use the provided pre-commit hooks for code quality

## License

This project is licensed under the MIT License.