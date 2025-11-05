"""
Configuration management for the evaluation runner.
"""

import json
import os
from dataclasses import dataclass, field
from typing import Dict, Any, Optional
import logging

from ..exceptions import ConfigurationError

@dataclass
class AzureStorageConfig:
    """Configuration for Azure Storage services."""
    account_name: str
    queue_name: str
    success_queue_name: str = "eval-processing-requests-completed"  # Queue for successfully processed messages
    failure_queue_name: str = "eval-processing-requests-failed"     # Queue for failed processed messages
    blob_container_prefix: Optional[str] = None  # No longer used - containers use agent_id directly
    use_managed_identity: bool = True
    connection_string: Optional[str] = None  # Fallback for local development
    
    def validate(self) -> None:
        """Validate Azure storage configuration."""
        if self.use_managed_identity:
            if not self.account_name or self.account_name == "your-storage-account-name":
                raise ConfigurationError("Azure storage account name must be configured for Managed Identity")
        else:
            if not self.connection_string or self.connection_string == "your-azure-storage-connection-string":
                raise ConfigurationError("Azure storage connection string must be configured")
        if not self.queue_name:
            raise ConfigurationError("Azure queue name must be configured")

@dataclass
class ApiEndpointsConfig:
    """Configuration for API endpoints."""
    base_url: str
    enriched_dataset_endpoint: str
    metrics_configuration_endpoint: str
    update_status: str

@dataclass
class EvaluationConfig:
    """Configuration for evaluation execution."""
    max_parallel_prompts: int = 10
    max_parallel_metrics: int = 5
    timeout_seconds: int = 300
    retry_attempts: int = 2
    queue_polling_interval_seconds: int = 30
    queue_visibility_timeout_seconds: int = 300
    max_message_retries: int = 2  # Maximum retries for failed queue messages
    
    def validate(self) -> None:
        """Validate evaluation configuration."""
        if self.max_parallel_metrics <= 0:
            raise ConfigurationError("max_parallel_metrics must be positive")
        if self.timeout_seconds <= 0:
            raise ConfigurationError("timeout_seconds must be positive")
        if self.retry_attempts < 0:
            raise ConfigurationError("retry_attempts must be non-negative")

@dataclass
class AzureOpenAIConfig:
    """Configuration for Azure OpenAI service."""
    endpoint: str
    api_key: Optional[str]
    deployment_name: str
    api_version: str
    use_managed_identity: bool = True
    subscription_id: Optional[str] = None
    resource_group_name: Optional[str] = None
    resource_name: Optional[str] = None
    tenant_id: Optional[str] = None
    
    def validate(self) -> None:
        """Validate Azure OpenAI configuration."""
        if not self.endpoint or self.endpoint == "https://your-openai-endpoint.openai.azure.com/":
            raise ConfigurationError("Azure OpenAI endpoint must be configured")
        
        if self.use_managed_identity:
            # For managed identity, we need tenant ID and resource details
            if self.tenant_id and self.tenant_id == "your-tenant-id":
                raise ConfigurationError("Azure tenant ID must be configured for managed identity")
            if self.subscription_id and self.subscription_id == "your-subscription-id":
                raise ConfigurationError("Azure subscription ID should be configured for managed identity")
        else:
            # For API key authentication
            if not self.api_key or self.api_key == "your-azure-openai-api-key":
                raise ConfigurationError("Azure OpenAI API key must be configured when not using managed identity")
        
        if not self.deployment_name or self.deployment_name == "your-gpt-deployment-name":
            raise ConfigurationError("Azure OpenAI deployment name must be configured")

@dataclass
class ApplicationInsightsConfig:
    """Configuration for Application Insights telemetry."""
    connection_string: str
    enable_telemetry: bool = True
    enable_console_logging: bool = True
    log_level: Dict[str, str] = field(default_factory=dict)
    
    def validate(self) -> None:
        """Validate Application Insights configuration."""
        if self.enable_telemetry and not self.connection_string:
            # Don't raise error, just disable telemetry
            self.enable_telemetry = False

@dataclass
class AzureAIConfig:
    """Configuration for Azure AI Foundry project."""
    project_id: str
    subscription_id: str
    resource_group_name: str
    project_name: str
    tenant_id: Optional[str] = None
    
    def validate(self) -> None:
        """Validate Azure AI configuration."""
        if not self.subscription_id or self.subscription_id == "your-azure-subscription-id":
            raise ConfigurationError("Azure subscription ID must be configured")
        if not self.resource_group_name or self.resource_group_name == "your-resource-group-name":
            raise ConfigurationError("Azure resource group name must be configured")
        if not self.project_name or self.project_name == "your-project-name":
            raise ConfigurationError("Azure AI project name must be configured")
        if self.tenant_id and self.tenant_id == "your-tenant-id":
            raise ConfigurationError("Azure tenant ID must be configured")

@dataclass
class LoggingConfig:
    """Configuration for logging."""
    default_level: str = "Information"
    system_level: str = "Warning"
    microsoft_level: str = "Warning"

class AppSettings:
    """Application settings manager."""
    
    def __init__(self, config_path: Optional[str] = None):
        """
        Initialize application settings.
        
        Args:
            config_path: Path to the configuration file. If None, uses appsettings.json
        """
        self.config_path = config_path or "appsettings.json"
        self._config_data = self._load_config()
        
        # Load configurations
        self.azure_storage = self._load_azure_storage_config()
        self.api_endpoints = self._load_api_endpoints_config()
        self.evaluation = self._load_evaluation_config()
        self.application_insights = self._load_application_insights_config()
        self.logging = self._load_logging_config()
        self.azure_openai = self._load_azure_openai_config()
        self.azure_ai = self._load_azure_ai_config()
        
    
    def validate_configuration(self) -> None:
        """Validate all configuration sections."""
        # Validation can be called manually when needed
        self.azure_storage.validate()
        self.evaluation.validate()
        # Azure AI configurations are optional for some evaluators
        # Only validate if they need to be used
        
    def _load_config(self) -> Dict[str, Any]:
        """Load configuration from file or environment variables."""
        config_data = {}
        
        # Try to load from file first
        if os.path.exists(self.config_path):
            with open(self.config_path, 'r') as f:
                config_data = json.load(f)
        
        # Override with environment variables if available
        config_data = self._override_with_env_vars(config_data)
        
        return config_data
    
    def _override_with_env_vars(self, config_data: Dict[str, Any]) -> Dict[str, Any]:
        """Override configuration with environment variables."""
        # Azure Storage
        if 'AzureStorage' not in config_data:
            config_data['AzureStorage'] = {}
        
        config_data['AzureStorage']['AccountName'] = os.getenv(
            'AZURE_STORAGE_ACCOUNT_NAME', 
            config_data['AzureStorage'].get('AccountName', '')
        )
        config_data['AzureStorage']['QueueName'] = os.getenv(
            'AZURE_QUEUE_NAME', 
            config_data['AzureStorage'].get('QueueName', 'eval-processing-requests')
        )
        config_data['AzureStorage']['SuccessQueueName'] = os.getenv(
            'AZURE_SUCCESS_QUEUE_NAME', 
            config_data['AzureStorage'].get('SuccessQueueName', 'eval-processing-requests-completed')
        )
        config_data['AzureStorage']['FailureQueueName'] = os.getenv(
            'AZURE_FAILURE_QUEUE_NAME', 
            config_data['AzureStorage'].get('FailureQueueName', 'eval-processing-requests-failed')
        )
        config_data['AzureStorage']['BlobContainerPrefix'] = os.getenv(
            'AZURE_BLOB_CONTAINER_PREFIX', 
            config_data['AzureStorage'].get('BlobContainerPrefix', 'agent-')
        )
        config_data['AzureStorage']['UseManagedIdentity'] = os.getenv(
            'AZURE_USE_MANAGED_IDENTITY', 
            str(config_data['AzureStorage'].get('UseManagedIdentity', True))
        ).lower() == 'true'
        config_data['AzureStorage']['ConnectionString'] = os.getenv(
            'AZURE_STORAGE_CONNECTION_STRING', 
            config_data['AzureStorage'].get('ConnectionString', '')
        )
        
        # API Endpoints
        if 'ApiEndpoints' not in config_data:
            config_data['ApiEndpoints'] = {}
        
        config_data['ApiEndpoints']['BaseUrl'] = os.getenv(
            'EVALUATION_API_BASE_URL', 
            config_data['ApiEndpoints'].get('BaseUrl', '')
        )
        config_data['ApiEndpoints']['EvalConfigEndpoint'] = os.getenv(
            'EVAL_CONFIG_ENDPOINT', 
            config_data['ApiEndpoints'].get('EvalConfigEndpoint', '')
        )
        config_data['ApiEndpoints']['StatusUpdateEndpoint'] = os.getenv(
            'STATUS_UPDATE_ENDPOINT', 
            config_data['ApiEndpoints'].get('StatusUpdateEndpoint', '')
        )
        
        # Azure OpenAI
        if 'AzureOpenAI' not in config_data:
            config_data['AzureOpenAI'] = {}
        
        config_data['AzureOpenAI']['Endpoint'] = os.getenv(
            'AZURE_OPENAI_ENDPOINT', 
            config_data['AzureOpenAI'].get('Endpoint', '')
        )
        config_data['AzureOpenAI']['DeploymentName'] = os.getenv(
            'AZURE_OPENAI_DEPLOYMENT_NAME',
            config_data['AzureOpenAI'].get('DeploymentName', 'gpt-4.1')
        )
        config_data['AzureOpenAI']['ApiVersion'] = os.getenv(
            'AZURE_OPENAI_API_VERSION',
            config_data['AzureOpenAI'].get('ApiVersion', '2025-01-01-preview')
        )
        config_data['AzureOpenAI']['UseManagedIdentity'] = os.getenv(
            'AZURE_OPENAI_USE_MANAGED_IDENTITY',
            str(config_data['AzureOpenAI'].get('UseManagedIdentity', True))
        ).lower() == 'true'
        config_data['AzureOpenAI']['TenantId'] = os.getenv(
            'AZURE_TENANT_ID',
            config_data['AzureOpenAI'].get('TenantId', '')
        )
        config_data['AzureOpenAI']['SubscriptionId'] = os.getenv(
            'AZURE_SUBSCRIPTION_ID',
            config_data['AzureOpenAI'].get('SubscriptionId', '')
        )
        
        # Azure AI Foundry
        if 'AzureAI' not in config_data:
            config_data['AzureAI'] = {}
        
        config_data['AzureAI']['SubscriptionId'] = os.getenv(
            'AZURE_SUBSCRIPTION_ID',
            config_data['AzureAI'].get('SubscriptionId', '')
        )
        config_data['AzureAI']['ResourceGroupName'] = os.getenv(
            'AZURE_RESOURCE_GROUP_NAME',
            config_data['AzureAI'].get('ResourceGroupName', '')
        )
        config_data['AzureAI']['ProjectName'] = os.getenv(
            'AZURE_AI_PROJECT_NAME',
            config_data['AzureAI'].get('ProjectName', '')
        )
        config_data['AzureAI']['TenantId'] = os.getenv(
            'AZURE_TENANT_ID',
            config_data['AzureAI'].get('TenantId', '')
        )
        
        # Application Insights
        if 'ApplicationInsights' not in config_data:
            config_data['ApplicationInsights'] = {}
        
        config_data['ApplicationInsights']['ConnectionString'] = os.getenv(
            'APPLICATIONINSIGHTS_CONNECTION_STRING',
            config_data['ApplicationInsights'].get('ConnectionString', '')
        )
        
        return config_data
    
    def _load_azure_storage_config(self) -> AzureStorageConfig:
        """Load Azure Storage configuration."""
        azure_config = self._config_data.get('AzureStorage', {})
        
        # Check if we should use Managed Identity (default) or Connection String (fallback)
        use_managed_identity = azure_config.get('UseManagedIdentity', True)
        
        return AzureStorageConfig(
            account_name=azure_config.get('AccountName', ''),
            queue_name=azure_config.get('QueueName', 'eval-processing-requests'),
            success_queue_name=azure_config.get('SuccessQueueName', 'eval-processing-requests-completed'),
            failure_queue_name=azure_config.get('FailureQueueName', 'eval-processing-requests-failed'),
            blob_container_prefix=azure_config.get('BlobContainerPrefix', 'agent-'),
            use_managed_identity=use_managed_identity,
            connection_string=azure_config.get('ConnectionString', '') if not use_managed_identity else None
        )
    
    def _load_api_endpoints_config(self) -> ApiEndpointsConfig:
        """Load API endpoints configuration."""
        api_config = self._config_data.get('ApiEndpoints', {})
        return ApiEndpointsConfig(
            base_url=api_config.get('BaseUrl', ''),
            enriched_dataset_endpoint=api_config.get('EnrichedDatasetEndpoint', ''),
            metrics_configuration_endpoint=api_config.get('MetricsConfigurationEndpoint', ''),
            update_status=api_config.get('UpdateStatus', '')
        )
    
    def _load_evaluation_config(self) -> EvaluationConfig:
        """Load evaluation configuration."""
        eval_config = self._config_data.get('Evaluation', {})
        return EvaluationConfig(
            max_parallel_prompts=eval_config.get('MaxParallelPrompts', 10),
            max_parallel_metrics=eval_config.get('MaxParallelMetrics', 5),
            timeout_seconds=eval_config.get('TimeoutSeconds', 300),
            retry_attempts=eval_config.get('RetryAttempts', 2),
            queue_polling_interval_seconds=eval_config.get('QueuePollingIntervalSeconds', 30),
            queue_visibility_timeout_seconds=eval_config.get('QueueVisibilityTimeoutSeconds', 300)
        )
    
    def _load_application_insights_config(self) -> ApplicationInsightsConfig:
        """Load Application Insights configuration."""
        ai_config = self._config_data.get('ApplicationInsights', {})
        return ApplicationInsightsConfig(
            connection_string=ai_config.get('ConnectionString', ''),
            enable_telemetry=ai_config.get('EnableTelemetry', True),
            enable_console_logging=ai_config.get('EnableConsoleLogging', True),
            log_level=ai_config.get('LogLevel', {})
        )
    
    def _load_logging_config(self) -> LoggingConfig:
        """Load logging configuration."""
        logging_config = self._config_data.get('Logging', {})
        log_levels = logging_config.get('LogLevel', {})
        return LoggingConfig(
            default_level=log_levels.get('Default', 'Information'),
            system_level=log_levels.get('System', 'Warning'),
            microsoft_level=log_levels.get('Microsoft', 'Warning')
        )
    
    def _load_azure_openai_config(self) -> AzureOpenAIConfig:
        """Load Azure OpenAI configuration."""
        openai_config = self._config_data.get('AzureOpenAI', {})
        return AzureOpenAIConfig(
            endpoint=openai_config.get('Endpoint', 'https://your-openai-endpoint.openai.azure.com/'),
            api_key=openai_config.get('ApiKey'),  # Can be None for managed identity
            deployment_name=openai_config.get('DeploymentName', 'your-gpt-deployment-name'),
            api_version=openai_config.get('ApiVersion', '2024-10-21'),
            use_managed_identity=openai_config.get('UseManagedIdentity', True),
            subscription_id=openai_config.get('SubscriptionId'),
            resource_group_name=openai_config.get('ResourceGroupName'),
            resource_name=openai_config.get('ResourceName'),
            tenant_id=openai_config.get('TenantId')
        )
    
    def _load_azure_ai_config(self) -> AzureAIConfig:
        """Load Azure AI Foundry configuration."""
        ai_config = self._config_data.get('AzureAI', {})
        return AzureAIConfig(
            project_id=ai_config.get('ProjectId', 'your-azure-ai-project-id'),
            subscription_id=ai_config.get('SubscriptionId', 'your-azure-subscription-id'),
            resource_group_name=ai_config.get('ResourceGroupName', 'your-resource-group-name'),
            project_name=ai_config.get('ProjectName', 'your-project-name'),
            tenant_id=ai_config.get('TenantId')
        )
    
    def setup_logging(self) -> None:
        """Configure logging based on settings.""" 
        # Map level strings to logging constants
        level_map = {
            'CRITICAL': logging.CRITICAL,
            'ERROR': logging.ERROR, 
            'WARNING': logging.WARNING,
            'INFO': logging.INFO,
            'DEBUG': logging.DEBUG,
            'INFORMATION': logging.INFO,  # .NET style
            'WARN': logging.WARNING      # .NET style
        }
        
        default_level = level_map.get(self.logging.default_level.upper(), logging.INFO)
        
        # Configure root logger
        logging.basicConfig(
            level=default_level,
            format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            handlers=[]
        )
        
        logger = logging.getLogger()
        handlers = []
        
        # Add console handler if enabled
        if self.application_insights.enable_console_logging:
            console_handler = logging.StreamHandler()
            console_handler.setLevel(default_level)
            formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
            console_handler.setFormatter(formatter)
            handlers.append(console_handler)
        
        # Configure OpenTelemetry if enabled
        if self.application_insights.enable_telemetry:
            try:
                from ..telemetry.opentelemetry_config import otel_config
                
                if self.application_insights.connection_string:
                    # Set up OpenTelemetry with Azure Monitor
                    otel_config.setup_telemetry(
                        connection_string=self.application_insights.connection_string,
                        enable_console=self.application_insights.enable_console_logging
                    )
                else:
                    print("⚠️  Application Insights connection string not configured")
                    if self.application_insights.enable_console_logging:
                        print("   Falling back to console-only logging")
                
            except ImportError:
                print("⚠️  OpenTelemetry packages not installed. Run: pip install opentelemetry-api opentelemetry-sdk azure-monitor-opentelemetry-exporter")
                # Add basic console handler as fallback
                if self.application_insights.enable_console_logging:
                    console_handler = logging.StreamHandler()
                    console_handler.setLevel(default_level)
                    formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
                    console_handler.setFormatter(formatter)
                    handlers.append(console_handler)
            except Exception as e:
                print(f"⚠️  Failed to configure OpenTelemetry: {e}")
                # Add basic console handler as fallback
                if self.application_insights.enable_console_logging:
                    console_handler = logging.StreamHandler()
                    console_handler.setLevel(default_level)
                    formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
                    console_handler.setFormatter(formatter)
                    handlers.append(console_handler)
        
        # Add any remaining handlers to logger
        for handler in handlers:
            logger.addHandler(handler)
    
    def shutdown_telemetry(self):
        """Shutdown telemetry providers gracefully."""
        try:
            from ..telemetry.opentelemetry_config import otel_config
            otel_config.shutdown()
        except ImportError:
            pass


# Global app settings instance
app_settings = AppSettings()