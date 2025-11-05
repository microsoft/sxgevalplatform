# Contributing Guide

## 🤝 Welcome Contributors!

Thank you for your interest in contributing to the SXG Evaluation Platform Evaluation Engine! This guide provides everything you need to know about contributing to this project, including development setup, coding standards, testing requirements, and the contribution process.

## 📋 Table of Contents

- [Development Setup](#development-setup)
- [Project Architecture](#project-architecture)
- [Coding Standards](#coding-standards)
- [Testing Requirements](#testing-requirements)
- [Performance Guidelines](#performance-guidelines)
- [Pull Request Process](#pull-request-process)
- [Code Review Guidelines](#code-review-guidelines)
- [Release Process](#release-process)

## 🛠️ Development Setup

### Prerequisites

- **Python 3.11+** (required for async/await optimizations)
- **Git** for version control
- **Docker** for containerization testing
- **Azure CLI** for Azure resource testing
- **VS Code** (recommended) with Python extension

### Environment Setup

1. **Clone the repository**:
   ```bash
   git clone https://github.com/microsoft/sxgevalplatform.git
   cd sxgevalplatform/src/Sxg-Eval-Platform.EvaluationEngine
   ```

2. **Set up virtual environment**:
   ```bash
   python -m venv .venv
   
   # Windows
   .\.venv\Scripts\Activate.ps1
   
   # Linux/Mac
   source .venv/bin/activate
   ```

3. **Install dependencies**:
   ```bash
   # Production dependencies
   pip install -r requirements.txt
   
   # Development dependencies
   pip install -r requirements-dev.txt
   ```

4. **Configure development settings**:
   ```bash
   cp appsettings.example.json appsettings.json
   # Edit appsettings.json with your development configuration
   ```

5. **Set up pre-commit hooks**:
   ```bash
   pre-commit install
   ```

### IDE Configuration

#### VS Code Settings
```json
{
  "python.defaultInterpreterPath": "./.venv/Scripts/python.exe",
  "python.linting.enabled": true,
  "python.linting.flake8Enabled": true,
  "python.formatting.provider": "black",
  "python.testing.pytestEnabled": true,
  "python.testing.pytestArgs": ["tests/"],
  "editor.formatOnSave": true
}
```

#### Recommended Extensions
- Python (ms-python.python)
- Black Formatter (ms-python.black-formatter)
- Pylance (ms-python.vscode-pylance)
- GitLens (eamodio.gitlens)
- Azure Tools (ms-vscode.vscode-node-azure-pack)

## 🏗️ Project Architecture

### Core Components Overview

```
src/eval_runner/
├── core/                    # Main business logic
│   └── evaluation_engine.py # Orchestrates evaluation processing
├── services/               # External integrations
│   ├── azure_storage.py    # Azure Storage Queue/Blob services
│   ├── http_client.py      # HTTP client with connection pooling
│   └── evaluation_api_client.py # External API integration
├── models/                 # Data models and DTOs
│   └── eval_models.py      # Core data structures
├── metrics/                # Evaluation metrics implementation
│   ├── base_evaluators.py  # Abstract base classes
│   ├── agentic/            # Agentic evaluation metrics
│   ├── rag/                # RAG evaluation metrics
│   ├── risk_and_safety/    # Safety evaluation metrics
│   └── text_similarity/    # Text similarity metrics
└── utils/                  # Utility modules
    └── logging_helper.py   # Structured logging and performance monitoring
```

### Key Design Principles

1. **Concurrent Processing**: All I/O operations use async/await with controlled concurrency
2. **Resource Management**: Proper lifecycle management for connections and resources
3. **Error Resilience**: Comprehensive error handling with partial success logic
4. **Performance Optimization**: Connection pooling, timeout management, concurrent execution
5. **Observability**: Structured logging, performance metrics, distributed tracing

## 📏 Coding Standards

### Python Style Guide

We follow [PEP 8](https://pep8.org/) with some customizations:

#### General Rules
- **Line Length**: 120 characters maximum
- **Indentation**: 4 spaces (no tabs)
- **Imports**: Organized using isort
- **Formatting**: Black formatter with line-length=120
- **Type Hints**: Required for all public functions and methods
- **Docstrings**: Google-style docstrings for all public APIs

#### Example Code Style

```python
from typing import List, Optional, Dict, Any
import asyncio
import logging

logger = logging.getLogger(__name__)


class EvaluationService:
    """Service for processing evaluation requests with concurrent optimization.
    
    This service handles evaluation requests from Azure Storage Queue,
    processes them concurrently, and stores results in Azure Blob Storage.
    
    Attributes:
        concurrency_limit: Maximum concurrent evaluations
        timeout_seconds: Timeout for individual metric evaluations
    """
    
    def __init__(
        self, 
        concurrency_limit: int = 3,
        timeout_seconds: int = 30
    ) -> None:
        """Initialize the evaluation service.
        
        Args:
            concurrency_limit: Maximum number of concurrent evaluations
            timeout_seconds: Timeout for metric evaluations
        """
        self.concurrency_limit = concurrency_limit
        self.timeout_seconds = timeout_seconds
        self._semaphore = asyncio.Semaphore(concurrency_limit)
    
    async def process_evaluation(
        self,
        eval_request: EvaluationRequest
    ) -> EvaluationResult:
        """Process a single evaluation request.
        
        Args:
            eval_request: The evaluation request to process
            
        Returns:
            EvaluationResult: Results of the evaluation
            
        Raises:
            EvaluationError: If evaluation processing fails
            TimeoutError: If evaluation exceeds timeout
        """
        async with self._semaphore:
            try:
                # Implementation here
                result = await self._execute_evaluation(eval_request)
                logger.info(
                    "Evaluation completed successfully",
                    extra={
                        "eval_run_id": eval_request.eval_run_id,
                        "duration_ms": result.duration_ms
                    }
                )
                return result
            except Exception as e:
                logger.error(
                    "Evaluation failed",
                    extra={
                        "eval_run_id": eval_request.eval_run_id,
                        "error": str(e)
                    },
                    exc_info=True
                )
                raise
```

### Async/Await Guidelines

1. **Use async/await for I/O operations**:
   ```python
   # Good
   async def fetch_data(url: str) -> dict:
       async with aiohttp.ClientSession() as session:
           async with session.get(url) as response:
               return await response.json()
   
   # Avoid
   def fetch_data_sync(url: str) -> dict:
       response = requests.get(url)
       return response.json()
   ```

2. **Implement proper concurrency control**:
   ```python
   # Good - controlled concurrency
   semaphore = asyncio.Semaphore(5)
   async def process_item(item):
       async with semaphore:
           return await expensive_operation(item)
   
   # Avoid - unlimited concurrency
   async def process_items(items):
       return await asyncio.gather(*[expensive_operation(item) for item in items])
   ```

3. **Handle timeouts appropriately**:
   ```python
   # Good
   try:
       result = await asyncio.wait_for(operation(), timeout=30.0)
   except asyncio.TimeoutError:
       logger.warning("Operation timed out after 30 seconds")
       return default_result()
   ```

### Error Handling Standards

1. **Use specific exception types**:
   ```python
   class EvaluationError(Exception):
       """Base exception for evaluation errors."""
       pass
   
   class ConfigurationError(EvaluationError):
       """Configuration-related errors."""
       pass
   
   class MetricExecutionError(EvaluationError):
       """Metric execution errors."""
       pass
   ```

2. **Always log errors with context**:
   ```python
   try:
       result = await process_evaluation(eval_request)
   except EvaluationError as e:
       logger.error(
           "Evaluation processing failed",
           extra={
               "eval_run_id": eval_request.eval_run_id,
               "error_type": type(e).__name__,
               "error_message": str(e)
           },
           exc_info=True
       )
       raise
   ```

### Performance Guidelines

1. **Use connection pooling**:
   ```python
   # Good - reuse connections
   class APIClient:
       def __init__(self):
           self.session = aiohttp.ClientSession(
               connector=aiohttp.TCPConnector(limit=20)
           )
   ```

2. **Implement proper resource cleanup**:
   ```python
   # Good - proper cleanup
   async def __aenter__(self):
       return self
   
   async def __aexit__(self, exc_type, exc_val, exc_tb):
       await self.close()
   ```

## 🧪 Testing Requirements

### Test Structure

```
tests/
├── unit/                   # Unit tests for individual components
├── integration/            # Integration tests for service interactions
├── performance/            # Performance and load tests
└── fixtures/               # Test data and fixtures
```

### Testing Standards

1. **Minimum Test Coverage**: 80% overall, 90% for critical paths
2. **Test Naming**: `test_<functionality>_<condition>_<expected_result>`
3. **Async Testing**: Use pytest-asyncio for async test functions
4. **Mocking**: Mock external dependencies (Azure services, APIs)

### Example Test Structure

```python
import pytest
import asyncio
from unittest.mock import AsyncMock, patch
from eval_runner.core.evaluation_engine import EvaluationEngine
from eval_runner.models.eval_models import QueueMessage


class TestEvaluationEngine:
    """Test suite for EvaluationEngine."""
    
    @pytest.fixture
    async def evaluation_engine(self):
        """Create evaluation engine instance for testing."""
        engine = EvaluationEngine()
        yield engine
        await engine.cleanup()
    
    @pytest.fixture
    def sample_queue_message(self):
        """Sample queue message for testing."""
        return QueueMessage(
            eval_run_id="test-eval-123",
            agent_id="test-agent-456",
            dataset_id="test-dataset-789",
            enriched_dataset_id="test-enriched-abc",
            metrics_configuration_id="test-metrics-def"
        )
    
    @pytest.mark.asyncio
    async def test_process_queue_message_success(
        self, 
        evaluation_engine, 
        sample_queue_message
    ):
        """Test successful queue message processing."""
        # Arrange
        with patch.object(evaluation_engine, '_fetch_configuration') as mock_fetch:
            mock_fetch.return_value = (mock_dataset, mock_metrics)
            
        # Act
        result = await evaluation_engine.process_queue_message(sample_queue_message)
        
        # Assert
        assert result is True
        mock_fetch.assert_called_once()
    
    @pytest.mark.asyncio
    async def test_process_queue_message_timeout(
        self, 
        evaluation_engine, 
        sample_queue_message
    ):
        """Test queue message processing timeout handling."""
        # Arrange
        with patch.object(evaluation_engine, '_run_evaluations') as mock_eval:
            mock_eval.side_effect = asyncio.TimeoutError("Evaluation timed out")
            
        # Act & Assert
        result = await evaluation_engine.process_queue_message(sample_queue_message)
        assert result is False
```

### Running Tests

```bash
# Run all tests
pytest

# Run with coverage
pytest --cov=eval_runner --cov-report=html

# Run specific test file
pytest tests/test_evaluation_engine.py

# Run tests with specific marker
pytest -m "not slow"

# Run async tests only
pytest -k "asyncio"
```

### Performance Testing

```python
@pytest.mark.performance
async def test_concurrent_processing_performance():
    """Test concurrent processing performance."""
    start_time = time.time()
    
    # Simulate concurrent evaluations
    tasks = [simulate_evaluation() for _ in range(10)]
    results = await asyncio.gather(*tasks)
    
    duration = time.time() - start_time
    
    # Assert performance improvements
    assert duration < 15.0  # Should complete in under 15 seconds
    assert all(result.success for result in results)
```

## 🚀 Performance Guidelines

### Optimization Principles

1. **Concurrent Processing**: Use async/await for I/O-bound operations
2. **Connection Pooling**: Reuse HTTP connections and database connections
3. **Resource Management**: Proper cleanup and lifecycle management
4. **Timeout Management**: Set appropriate timeouts for all operations
5. **Memory Efficiency**: Avoid memory leaks and excessive allocations

### Performance Monitoring

Add performance monitoring to new features:

```python
from eval_runner.utils.logging_helper import performance_timer

async def new_feature_function():
    """New feature with performance monitoring."""
    with performance_timer(logger, "new_feature_operation"):
        # Implementation here
        result = await expensive_operation()
        return result
```

### Benchmarking

Always benchmark performance-critical changes:

```python
# Before optimization
baseline_time = await benchmark_function()

# After optimization  
optimized_time = await benchmark_function()

improvement = (baseline_time - optimized_time) / baseline_time * 100
print(f"Performance improvement: {improvement:.1f}%")
```

## 🔄 Pull Request Process

### Before Submitting

1. **Code Quality Checks**:
   ```bash
   # Format code
   black src/ --line-length=120
   
   # Sort imports
   isort src/
   
   # Lint code
   flake8 src/ --max-line-length=120
   
   # Type checking
   mypy src/
   
   # Run tests
   pytest --cov=eval_runner
   ```

2. **Performance Validation**:
   ```bash
   # Run performance tests
   pytest tests/performance/ -v
   
   # Validate no performance regression
   python scripts/performance_benchmark.py
   ```

### Pull Request Template

```markdown
## Description
Brief description of changes and motivation.

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Performance improvement
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated  
- [ ] Performance tests added/updated
- [ ] All tests pass

## Performance Impact
- [ ] No performance impact
- [ ] Performance improvement (include benchmark results)
- [ ] Performance regression acceptable (provide justification)

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Documentation updated
- [ ] Breaking changes documented
```

### Review Process

1. **Automated Checks**: All CI/CD checks must pass
2. **Code Review**: At least one approval from core team
3. **Performance Review**: Performance-critical changes need performance team review
4. **Documentation Review**: Documentation updates reviewed for accuracy

## 👀 Code Review Guidelines

### As a Reviewer

1. **Focus Areas**:
   - Correctness and logic
   - Performance implications
   - Error handling
   - Code style and consistency
   - Test coverage

2. **Review Checklist**:
   - [ ] Code follows established patterns
   - [ ] Async/await used correctly
   - [ ] Proper error handling
   - [ ] Performance considerations addressed
   - [ ] Tests provide adequate coverage
   - [ ] Documentation updated if needed

3. **Feedback Guidelines**:
   - Be constructive and specific
   - Explain reasoning behind suggestions
   - Distinguish between "must fix" and "nice to have"
   - Approve when requirements are met

### As a Contributor

1. **Prepare for Review**:
   - Keep PRs focused and reasonably sized
   - Provide clear description and context
   - Include relevant tests
   - Update documentation

2. **Respond to Feedback**:
   - Address all feedback comments
   - Ask for clarification if needed
   - Make requested changes promptly
   - Re-request review after changes

## 🚢 Release Process

### Versioning

We use [Semantic Versioning](https://semver.org/):
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Release Checklist

1. **Pre-Release**:
   - [ ] All tests pass
   - [ ] Performance benchmarks validated
   - [ ] Documentation updated
   - [ ] CHANGELOG.md updated
   - [ ] Version bumped in relevant files

2. **Release**:
   - [ ] Tag created with version number
   - [ ] Docker image built and tagged
   - [ ] Release notes published
   - [ ] Deployment validated

3. **Post-Release**:
   - [ ] Monitor performance metrics
   - [ ] Check error rates
   - [ ] Validate functionality in production

## 🆘 Getting Help

### Resources
- **Documentation**: Check existing documentation first
- **Issues**: Search existing issues for similar problems
- **Discussions**: Use GitHub Discussions for questions
- **Teams**: Contact the development team directly

### Contact Information
- **Team Lead**: [Team Lead Contact]
- **Architecture Questions**: [Architecture Team]
- **Performance Questions**: [Performance Team]
- **Azure/Infrastructure**: [Infrastructure Team]

## 📝 Additional Resources

- [Python Async Programming Guide](https://docs.python.org/3/library/asyncio.html)
- [Azure SDK for Python](https://docs.microsoft.com/en-us/azure/developer/python/)
- [Performance Testing Best Practices](https://docs.python.org/3/library/profile.html)
- [VS Code Python Development](https://code.visualstudio.com/docs/python/python-tutorial)

Thank you for contributing to the SXG Evaluation Platform! Your contributions help make this project better for everyone. 🎉