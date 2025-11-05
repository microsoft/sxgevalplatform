# SXG Evaluation Platform - Evaluation Engine

## 📋 Project Overview

The **SXG Evaluation Platform Evaluation Engine** is a sophisticated, cloud-native Python application designed to run AI agent evaluations at scale. It processes evaluation requests through Azure Storage Queues, executes comprehensive metrics using the Azure AI Evaluation SDK, and provides detailed results for AI system assessment.

### 🎯 Purpose
- **Automated AI Evaluation**: Evaluate AI agents and models using industry-standard metrics
- **Scalable Processing**: Handle multiple evaluation requests concurrently
- **Azure-Native**: Built for Azure cloud deployment with Managed Identity integration
- **Extensible Framework**: Modular architecture supporting custom evaluation metrics

## 🏗️ Architecture Overview

### High-Level Architecture
```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Queue Message │───▶│  Evaluation      │───▶│   Results       │
│   (Eval Request)│    │  Engine          │    │   (Blob Storage)│
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌──────────────────┐
                       │ Azure AI         │
                       │ Evaluation SDK   │
                       │ • Agentic        │
                       │ • RAG            │
                       │ • Safety         │
                       │ • Similarity     │
                       └──────────────────┘
```

### Core Components

1. **🎮 Main Application (`main.py`)**
   - Entry point and orchestration
   - Signal handling for graceful shutdown
   - Queue message processing loop

2. **⚙️ Evaluation Engine (`core/evaluation_engine.py`)**
   - Main business logic coordinator
   - Dataset processing
   - Metrics execution management
   - Results aggregation and storage

3. **📊 Metrics System (`metrics/`)**
   - Azure AI Evaluation SDK integration
   - 20+ pre-configured evaluators across 4 categories
   - Base classes for consistent implementation  
   - Results standardization with enhanced error handling

4. **☁️ Azure Services (`services/`)**
   - Queue message handling with Managed Identity and connection pooling
   - Blob storage for results with Managed Identity
   - HTTP client with connection pooling and lifecycle management
   - Success/failure logging to separate queues

5. **⚙️ Configuration (`config/settings.py`)**
   - Environment-aware configuration management
   - Managed Identity support
   - Validation and error handling

## 📁 Project Structure

```
src/eval_runner/
├── 📄 main.py                          # Application entry point
├── 📁 config/
│   └── 📄 settings.py                  # Configuration management
├── 📁 core/
│   └── 📄 evaluation_engine.py         # Main evaluation logic
├── 📁 services/
│   ├── 📄 azure_storage.py             # Azure Storage services
│   ├── 📄 evaluation_api_client.py     # External API client
│   └── 📄 http_client.py               # HTTP utilities
├── 📁 models/
│   └── 📄 eval_models.py               # Data models and DTOs
├── 📁 metrics/
│   ├── 📄 azure_ai_interface.py        # Azure AI SDK interface
│   ├── 📄 base_evaluators.py           # Abstract base classes
│   ├── 📄 evaluation_result.py         # Result models
│   ├── 📁 agentic/                     # Intent, Task, Tool evaluators
│   ├── 📁 rag/                         # Groundedness, Relevance, etc.
│   ├── 📁 risk_and_safety/             # Safety evaluators
│   └── 📁 text_similarity/             # F1, ROUGE, Similarity
├── 📄 azure_ai_config.py               # Azure AI SDK configuration
└── 📄 exceptions.py                    # Custom exceptions
```

## 🔄 Application Workflow

### 1. **Startup Sequence**
```python
# main.py → EvaluationApp.start()
1. Setup signal handlers (SIGINT, SIGTERM)
2. Initialize queue service with Managed Identity
3. Start listening for queue messages
4. Process messages asynchronously
```

### 2. **Message Processing Flow**
```python
# EvaluationApp._handle_queue_message()
Queue Message → Parse → Validation → Evaluation Engine → Results Storage
```

### 3. **Evaluation Engine Workflow**
```python
# evaluation_engine.py → process_queue_message()
1. Fetch enriched dataset from API
2. Load metrics configuration
3. Initialize Azure AI evaluators
4. Process dataset items in parallel
5. Aggregate results and calculate scores
6. Upload results to blob storage
7. Update status via API
```

## 📊 Metrics System Details

### Azure AI Evaluation Categories

#### 1. **🤖 Agentic Evaluators** (`metrics/agentic/`)
- **IntentResolutionEvaluator**: Measures intent understanding accuracy
- **ToolCallAccuracyEvaluator**: Validates tool usage correctness
- **TaskAdherenceEvaluator**: Checks task completion quality

#### 2. **📚 RAG (Retrieval-Augmented Generation)** (`metrics/rag/`)
- **GroundednessEvaluator**: Assesses factual grounding in context
- **RelevanceEvaluator**: Measures response relevance to query
- **CoherenceEvaluator**: Evaluates logical consistency
- **FluencyEvaluator**: Assesses naturalness and readability

#### 3. **🛡️ Risk & Safety** (`metrics/risk_and_safety/`)
- **ViolenceEvaluator**: Detects violent content
- **SexualEvaluator**: Identifies inappropriate sexual content
- **SelfHarmEvaluator**: Flags self-harm references
- **HateUnfairnessEvaluator**: Detects hate speech and bias
- **IndirectAttackEvaluator**: Identifies prompt injection attempts

#### 4. **📈 Text Similarity** (`metrics/text_similarity/`)
- **SimilarityEvaluator**: Semantic similarity assessment
- **F1ScoreEvaluator**: Precision/recall harmonic mean
- **RougeScoreEvaluator**: Summary evaluation metrics

### Base Evaluator Classes

```python
# base_evaluators.py
BaseAzureEvaluator       # Common interface and lifecycle
├── ModelBasedEvaluator  # LLM-judge evaluators (GPT-based)
├── SafetyEvaluator      # Azure AI Foundry safety evaluators
└── StatisticalEvaluator # Statistical/computational metrics
```

## 🔧 Configuration System

### Configuration Classes

```python
# settings.py
AppSettings
├── AzureStorageConfig      # Storage account, queues, blobs
├── ApiEndpointsConfig      # External API endpoints
├── ApiKeysConfig           # Authentication keys
├── EvaluationConfig        # Execution parameters
├── AzureOpenAIConfig       # Azure OpenAI for LLM judges
├── AzureAIConfig           # Azure AI Foundry project
└── LoggingConfig           # Logging levels and formats
```

### Environment Variables Support
```bash
# Azure Storage
AZURE_STORAGE_ACCOUNT_NAME=your-storage-account
AZURE_USE_MANAGED_IDENTITY=true

# API Configuration  
EVAL_API_KEY=your-api-key
EVAL_CONFIG_ENDPOINT=https://your-api.com/config

# Azure AI Services
AZURE_OPENAI_ENDPOINT=https://your-openai.azure.com
AZURE_AI_PROJECT_NAME=your-project
```

## 🚀 Deployment Options

### 1. **Azure Container Apps** (Recommended)
```dockerfile
# Uses provided Dockerfile
# Automatic scaling based on queue length
# Managed Identity integration
```

### 2. **Azure Container Instances**
```bash
# Single instance deployment
# Manual scaling
# Cost-effective for low volume
```

### 3. **Local Development**
```bash
# Docker Compose setup
# Connection string fallback
# Hot reload support
```

## 🔒 Security Features

### Managed Identity Integration
- **Storage Access**: Queue and Blob operations without secrets
- **Azure AI Services**: Automatic credential management
- **API Authentication**: Environment-based key management

### Required Azure Permissions
```bash
# Storage Account
- Storage Queue Data Contributor
- Storage Blob Data Contributor

# Azure AI Foundry
- Cognitive Services User
- Azure AI Developer
```

## 📈 Performance Characteristics

### Concurrency Settings
```json
{
  "Evaluation": {
    "MaxParallelPrompts": 10,     // Concurrent dataset items
    "MaxParallelMetrics": 5,      // Concurrent metric evaluations
    "TimeoutSeconds": 300,        // Per-evaluation timeout
    "RetryAttempts": 3            // Failure retry count
  }
}
```

### Scaling Behavior
- **Queue Processing**: Single message at a time per instance
- **Dataset Evaluation**: Parallel processing within evaluation
- **Metric Execution**: Concurrent evaluation across metrics
- **Auto-scaling**: Based on queue length in Azure Container Apps

## 🛠️ Development Workflow

### Adding New Metrics
1. Create evaluator class inheriting from appropriate base class
2. Implement required abstract methods
3. Register in `azure_ai_interface.py`
4. Add configuration in `azure_ai_config.py`
5. Update tests and documentation

### Testing Strategy
```bash
# Unit Tests
pytest tests/test_refactored_evaluators.py -v

# Integration Tests
pytest tests/test_azure_storage.py -v

# End-to-end Tests  
python src/test_metrics.py
```

## 📚 Key Files to Understand

### **Start Here** 📍
1. **`README.md`** - This comprehensive overview
2. **`main.py`** - Application entry point and flow
3. **`core/evaluation_engine.py`** - Main business logic

### **Core Architecture**
4. **`config/settings.py`** - Configuration management
5. **`services/azure_storage.py`** - Azure integration
6. **`metrics/azure_ai_interface.py`** - Metrics system

### **Implementation Details**
7. **`models/eval_models.py`** - Data structures
8. **`metrics/base_evaluators.py`** - Evaluation framework
9. **`azure_ai_config.py`** - Azure AI SDK setup

### **Deployment & Operations**
10. **`DEPLOYMENT.md`** - Deployment guide
11. **`AZURE_STORAGE_MANAGED_IDENTITY.md`** - Security setup
12. **`docker-compose.yml`** - Local development

## 🔍 Troubleshooting

### Common Issues
- **Authentication**: Verify Managed Identity permissions
- **Configuration**: Check environment variable formatting
- **Network**: Ensure Azure service connectivity
- **Performance**: Monitor queue depth and processing times

### Logging and Monitoring
```python
# Structured logging with levels
logger.info("Processing evaluation", extra={
    "eval_run_id": message.eval_run_id,
    "metrics_count": len(metrics)
})
```

---

**Next Steps**: Start with `main.py` to understand the application flow, then explore `core/evaluation_engine.py` for the main business logic. The metrics system in `metrics/` demonstrates the Azure AI SDK integration patterns.