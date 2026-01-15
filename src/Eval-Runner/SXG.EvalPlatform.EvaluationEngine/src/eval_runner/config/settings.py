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
    post_results_endpoint: str

@dataclass
class ApiAuthenticationConfig:
    """Configuration for API authentication."""
    # Client app registration details
    client_id: str = "17bf598d-3033-4395-ae51-4799394c84c7"  # SXG-EvalPlatform-EvalRunnerApp
    tenant_id: str = "72f988bf-86f1-41af-91ab-2d7cd011db47"
    
    # API resource details
    resource_app_id: str = "ac2b08ba-4232-438f-b333-0300df1de14d"  # SXG-EvalPlatform-API-PPE
    scope: str = "api://ac2b08ba-4232-438f-b333-0300df1de14d/.default"  # Use .default for app permissions
    
    # Authentication options
    use_managed_identity: bool = True
    enable_authentication: bool = True  # Feature flag to enable/disable authentication
    enable_token_caching: bool = True
    token_refresh_buffer_seconds: int = 300  # Refresh token 5 minutes before expiry
    
    def validate(self) -> None:
        """Validate authentication configuration."""
        if not self.client_id:
            raise ConfigurationError("API authentication client_id must be configured")
        if not self.tenant_id:
            raise ConfigurationError("API authentication tenant_id must be configured")
        if not self.scope:
            raise ConfigurationError("API authentication scope must be configured")

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
class AzureAIConfig:
    """Configuration for Azure AI services (both OpenAI and AI Foundry)."""
    # Required parameters (no defaults)
    subscription_id: str
    resource_group_name: str
    resource_name: str
    project_name: str
    
    # Optional parameters with defaults must come last
    endpoint: str = ""  # Optional for backward compatibility
    deployment_name: str = "gpt-4.1"
    api_version: str = "2025-01-01-preview"
    tenant_id: Optional[str] = None
    
    # Authentication configuration
    use_default_credentials: bool = False
    use_managed_identity: bool = True
    api_key: Optional[str] = None
    
    def validate(self) -> None:
        """Validate Azure AI configuration."""        
        # Authentication validation
        if self.use_managed_identity:
            if self.tenant_id and self.tenant_id == "your-tenant-id":
                raise ConfigurationError("Azure tenant ID must be configured for managed identity")
            if self.subscription_id and self.subscription_id == "your-subscription-id":
                raise ConfigurationError("Azure subscription ID should be configured for managed identity")
        else:
            if not self.api_key or self.api_key == "your-azure-openai-api-key":
                raise ConfigurationError("Azure OpenAI API key must be configured when not using managed identity")
        
        # Required fields validation
        if not self.deployment_name or self.deployment_name == "your-gpt-deployment-name":
            raise ConfigurationError("Azure OpenAI deployment name must be configured")
        if not self.subscription_id or self.subscription_id == "your-azure-subscription-id":
            raise ConfigurationError("Azure subscription ID must be configured")
        if not self.resource_group_name or self.resource_group_name == "your-resource-group-name":
            raise ConfigurationError("Azure resource group name must be configured")
        if not self.project_name or self.project_name == "your-project-name":
            raise ConfigurationError("Azure AI project name must be configured")

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
class LoggingConfig:
    """Configuration for logging."""
    default_level: str = "Information"
    system_level: str = "Warning"
    microsoft_level: str = "Warning"
    azure_core_level: str = "Warning"
    azure_monitor_level: str = "Warning"
    azure_identity_level: str = "Warning"

class AppSettings:
    """Application settings manager."""
    
    def __init__(self, config_path: Optional[str] = None):
        """
        Initialize application settings.
        
        Args:
            config_path: Path to the configuration file. If None, determines file based on RUNTIME_ENVIRONMENT
        """
        if config_path:
            self.config_path = config_path
        else:
            # Determine config file based on RUNTIME_ENVIRONMENT
            environment = os.getenv('RUNTIME_ENVIRONMENT', 'Local')
            self.config_path = f"appsettings.{environment}.json"
        self._config_data = self._load_config()
        
        # Load configurations
        self.azure_storage = self._load_azure_storage_config()
        self.api_endpoints = self._load_api_endpoints_config()
        self.api_authentication = self._load_api_authentication_config()
        self.evaluation = self._load_evaluation_config()
        self.application_insights = self._load_application_insights_config()
        self.logging = self._load_logging_config()
        self.azure_ai = self._load_azure_ai_config()
        
        # Deprecated properties (for backward compatibility)
        self.azure_openai = self.azure_ai  # Point to consolidated config
        
    
    def validate_configuration(self) -> None:
        """Validate all configuration sections."""
        # Validation can be called manually when needed
        self.azure_storage.validate()
        self.evaluation.validate()
        # Azure AI configurations are optional for some evaluators
        # Only validate if they need to be used
        
    def _load_config(self) -> Dict[str, Any]:
        """Load configuration from appsettings file only."""
        logging.info(f"Loading configuration from: {self.config_path}")
        
        if os.path.exists(self.config_path):
            try:
                with open(self.config_path, 'r') as f:
                    config_data = json.load(f)
                logging.info(f"Successfully loaded configuration from {self.config_path}")
                return config_data
            except Exception as e:
                raise ConfigurationError(f"Failed to parse configuration file {self.config_path}: {e}")
        else:
            raise ConfigurationError(f"Configuration file not found: {self.config_path}")
    
    def _get_config_value(self, json_value: Any, env_var_name: str, default: Any = None) -> Any:
        """
        Get configuration value with environment variable taking precedence.
        
        Priority:
        1. Environment variable (highest priority - allows quick overrides)
        2. Value from appsettings JSON file
        3. Default value
        
        Args:
            json_value: Value from JSON config file
            env_var_name: Name of environment variable to check first
            default: Default value if neither env var nor JSON is set
            
        Returns:
            Configuration value
        """
        # First check environment variable - it takes precedence
        env_value = os.getenv(env_var_name)
        if env_value:
            return env_value
        
        # Then check JSON value if no env var is set
        if json_value:
            return json_value
        
        # Fall back to default
        return default
    
    def _load_azure_storage_config(self) -> AzureStorageConfig:
        """Load Azure Storage configuration with environment variable fallback."""
        azure_config = self._config_data.get('AzureStorage', {})
        
        # Check if we should use Managed Identity (default) or Connection String (fallback)
        use_managed_identity_str = self._get_config_value(
            azure_config.get('UseManagedIdentity'), 
            'AzureStorage__UseManagedIdentity', 
            'True'
        )
        use_managed_identity = str(use_managed_identity_str).lower() in ('true', '1', 'yes')
        
        return AzureStorageConfig(
            account_name=self._get_config_value(
                azure_config.get('AccountName'),
                'AzureStorage__AccountName',
                ''
            ),
            queue_name=self._get_config_value(
                azure_config.get('QueueName'),
                'AzureStorage__QueueName',
                'eval-processing-requests'
            ),
            success_queue_name=self._get_config_value(
                azure_config.get('SuccessQueueName'),
                'AzureStorage__SuccessQueueName',
                'eval-processing-requests-completed'
            ),
            failure_queue_name=self._get_config_value(
                azure_config.get('FailureQueueName'),
                'AzureStorage__FailureQueueName',
                'eval-processing-requests-failed'
            ),
            blob_container_prefix=self._get_config_value(
                azure_config.get('BlobContainerPrefix'),
                'AzureStorage__BlobContainerPrefix',
                'agent-'
            ),
            use_managed_identity=use_managed_identity,
            connection_string=self._get_config_value(
                azure_config.get('ConnectionString'),
                'AzureStorage__ConnectionString',
                ''
            ) if not use_managed_identity else None
        )
    
    def _load_api_endpoints_config(self) -> ApiEndpointsConfig:
        """Load API endpoints configuration with environment variable fallback."""
        api_config = self._config_data.get('ApiEndpoints', {})
        return ApiEndpointsConfig(
            base_url=self._get_config_value(
                api_config.get('BaseUrl'),
                'ApiEndpoints__BaseUrl',
                ''
            ),
            enriched_dataset_endpoint=self._get_config_value(
                api_config.get('EnrichedDatasetEndpoint'),
                'ApiEndpoints__EnrichedDatasetEndpoint',
                ''
            ),
            metrics_configuration_endpoint=self._get_config_value(
                api_config.get('MetricsConfigurationEndpoint'),
                'ApiEndpoints__MetricsConfigurationEndpoint',
                ''
            ),
            update_status=self._get_config_value(
                api_config.get('UpdateStatusEndpoint'),
                'ApiEndpoints__UpdateStatusEndpoint',
                ''
            ),
            post_results_endpoint=self._get_config_value(
                api_config.get('PostResultsEndpoint'),
                'ApiEndpoints__PostResultsEndpoint',
                ''
            )
        )
    
    def _load_api_authentication_config(self) -> ApiAuthenticationConfig:
        """Load API authentication configuration with environment variable fallback."""
        auth_config = self._config_data.get('ApiAuthentication', {})
        return ApiAuthenticationConfig(
            client_id=self._get_config_value(
                auth_config.get('ClientId'),
                'ApiAuthentication__ClientId',
                '17bf598d-3033-4395-ae51-4799394c84c7'
            ),
            tenant_id=self._get_config_value(
                auth_config.get('TenantId'),
                'ApiAuthentication__TenantId',
                '72f988bf-86f1-41af-91ab-2d7cd011db47'
            ),
            resource_app_id=self._get_config_value(
                auth_config.get('ResourceAppId'),
                'ApiAuthentication__ResourceAppId',
                'ac2b08ba-4232-438f-b333-0300df1de14d'
            ),
            scope=self._get_config_value(
                auth_config.get('Scope'),
                'ApiAuthentication__Scope',
                'api://ac2b08ba-4232-438f-b333-0300df1de14d/.default'
            ),
            use_managed_identity=str(self._get_config_value(
                auth_config.get('UseManagedIdentity'),
                'ApiAuthentication__UseManagedIdentity',
                'True'
            )).lower() in ('true', '1', 'yes'),
            enable_authentication=str(self._get_config_value(
                auth_config.get('EnableAuthentication'),
                'ApiAuthentication__EnableAuthentication',
                'True'
            )).lower() in ('true', '1', 'yes'),
            enable_token_caching=str(self._get_config_value(
                auth_config.get('EnableTokenCaching'),
                'ApiAuthentication__EnableTokenCaching',
                'True'
            )).lower() in ('true', '1', 'yes'),
            token_refresh_buffer_seconds=int(self._get_config_value(
                auth_config.get('TokenRefreshBufferSeconds'),
                'ApiAuthentication__TokenRefreshBufferSeconds',
                300
            ))
        )
    
    def _load_evaluation_config(self) -> EvaluationConfig:
        """Load evaluation configuration with environment variable fallback."""
        eval_config = self._config_data.get('Evaluation', {})
        return EvaluationConfig(
            max_parallel_prompts=int(self._get_config_value(
                eval_config.get('MaxParallelPrompts'),
                'Evaluation__MaxParallelPrompts',
                10
            )),
            max_parallel_metrics=int(self._get_config_value(
                eval_config.get('MaxParallelMetrics'),
                'Evaluation__MaxParallelMetrics',
                5
            )),
            timeout_seconds=int(self._get_config_value(
                eval_config.get('TimeoutSeconds'),
                'Evaluation__TimeoutSeconds',
                300
            )),
            retry_attempts=int(self._get_config_value(
                eval_config.get('RetryAttempts'),
                'Evaluation__RetryAttempts',
                2
            )),
            queue_polling_interval_seconds=int(self._get_config_value(
                eval_config.get('QueuePollingIntervalSeconds'),
                'Evaluation__QueuePollingIntervalSeconds',
                30
            )),
            queue_visibility_timeout_seconds=int(self._get_config_value(
                eval_config.get('QueueVisibilityTimeoutSeconds'),
                'Evaluation__QueueVisibilityTimeoutSeconds',
                300
            ))
        )
    
    def _load_application_insights_config(self) -> ApplicationInsightsConfig:
        """Load Application Insights configuration with environment variable fallback."""
        ai_config = self._config_data.get('ApplicationInsights', {})
        return ApplicationInsightsConfig(
            connection_string=self._get_config_value(
                ai_config.get('ConnectionString'),
                'ApplicationInsights__ConnectionString',
                ''
            ),
            enable_telemetry=str(self._get_config_value(
                ai_config.get('EnableTelemetry'),
                'ApplicationInsights__EnableTelemetry',
                'True'
            )).lower() in ('true', '1', 'yes'),
            enable_console_logging=str(self._get_config_value(
                ai_config.get('EnableConsoleLogging'),
                'ApplicationInsights__EnableConsoleLogging',
                'True'
            )).lower() in ('true', '1', 'yes'),
            log_level=ai_config.get('LogLevel', {})
        )
    
    def _load_logging_config(self) -> LoggingConfig:
        """Load logging configuration."""
        logging_config = self._config_data.get('Logging', {})
        log_levels = logging_config.get('LogLevel', {})
        return LoggingConfig(
            default_level=log_levels.get('Default', 'Information'),
            system_level=log_levels.get('System', 'Warning'),
            microsoft_level=log_levels.get('Microsoft', 'Warning'),
            azure_core_level=log_levels.get('azure.core.pipeline.policies.http_logging_policy', 'Warning'),
            azure_monitor_level=log_levels.get('azure.monitor.opentelemetry.exporter', 'Warning'),
            azure_identity_level=log_levels.get('azure.identity', 'Warning')
        )
    
    def _load_azure_ai_config(self) -> AzureAIConfig:
        """Load consolidated Azure AI configuration with environment variable fallback."""
        # Load from both sections to support backward compatibility
        openai_config = self._config_data.get('AzureOpenAI', {})
        ai_config = self._config_data.get('AzureAI', {})
        
        # Use simplified structure matching Azure AI SDK samples
        return AzureAIConfig(
            # Core Azure AI Foundry project configuration
            subscription_id=self._get_config_value(
                ai_config.get('SubscriptionId') or openai_config.get('SubscriptionId'),
                'AzureAI__SubscriptionId',
                'your-azure-subscription-id'
            ),
            resource_group_name=self._get_config_value(
                ai_config.get('ResourceGroupName') or openai_config.get('ResourceGroupName'),
                'AzureAI__ResourceGroupName',
                'your-resource-group-name'
            ),
            resource_name=self._get_config_value(
                ai_config.get('ResourceName') or openai_config.get('ResourceName'),
                'AzureAI__ResourceName',
                'your-azure-resource-name'
            ),
            project_name=self._get_config_value(
                ai_config.get('ProjectName'),
                'AzureAI__ProjectName',
                'your-project-name'
            ),
            
            # Optional OpenAI configuration for backward compatibility
            endpoint=self._get_config_value(
                openai_config.get('Endpoint'),
                'AzureOpenAI__Endpoint',
                ''
            ),
            deployment_name=self._get_config_value(
                openai_config.get('DeploymentName'),
                'AzureOpenAI__DeploymentName',
                'gpt-4.1'
            ),
            api_version=self._get_config_value(
                openai_config.get('ApiVersion'),
                'AzureOpenAI__ApiVersion',
                '2025-01-01-preview'
            ),
            tenant_id=self._get_config_value(
                ai_config.get('TenantId') or openai_config.get('TenantId'),
                'AzureAI__TenantId',
                None
            ),
            
            # Authentication (check both sections, prefer AzureAI)
            use_default_credentials=str(self._get_config_value(
                ai_config.get('UseDefaultAzureCredential') or openai_config.get('UseDefaultAzureCredential'),
                'AzureAI__UseDefaultAzureCredential',
                'False'
            )).lower() in ('true', '1', 'yes'),
            use_managed_identity=str(self._get_config_value(
                ai_config.get('UseManagedIdentity') or openai_config.get('UseManagedIdentity'),
                'AzureAI__UseManagedIdentity',
                'True'
            )).lower() in ('true', '1', 'yes'),
            api_key=self._get_config_value(
                openai_config.get('ApiKey'),
                'AzureOpenAI__ApiKey',
                None
            )
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
        
        # Clear any existing handlers to avoid duplicates
        logger = logging.getLogger()
        for handler in logger.handlers[:]:
            logger.removeHandler(handler)
        
        # Configure root logger with empty handlers (we'll add specific ones)
        logging.basicConfig(
            level=default_level,
            format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            handlers=[],
            force=True
        )
        
        handlers = []
        
        # ALWAYS add console handler if console logging is enabled
        if self.application_insights.enable_console_logging:
            console_handler = logging.StreamHandler()
            console_handler.setLevel(default_level)
            formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
            console_handler.setFormatter(formatter)
            handlers.append(console_handler)
            print(f"Console logging enabled with level: {self.logging.default_level}")
        
        # Configure OpenTelemetry if enabled
        if self.application_insights.enable_telemetry:
            print(f"Setting up OpenTelemetry with Application Insights...")
            try:
                from ..telemetry.opentelemetry_config import otel_config
                
                if self.application_insights.connection_string:
                    # Set up OpenTelemetry with Azure Monitor
                    otel_config.setup_telemetry(
                        connection_string=self.application_insights.connection_string,
                        enable_console=False  # Console handler already added above
                    )
                    print(f"OpenTelemetry configured with Application Insights")
                else:
                    print("WARNING: Application Insights connection string not configured")
                
            except ImportError:
                print("WARNING: OpenTelemetry packages not installed. Run: pip install opentelemetry-api opentelemetry-sdk azure-monitor-opentelemetry-exporter")
            except Exception as e:
                print(f"WARNING: Failed to configure OpenTelemetry: {e}")
        else:
            print(f"WARNING: Application Insights telemetry disabled for faster local development")
        
        # Add all handlers to logger
        for handler in handlers:
            logger.addHandler(handler)
            
        print(f"Logging configured with {len(handlers)} handler(s): {[type(h).__name__ for h in handlers]}")
        
        # Set specific logger levels
        logging.getLogger('System').setLevel(level_map.get(self.logging.system_level.upper(), logging.WARNING))
        logging.getLogger('Microsoft').setLevel(level_map.get(self.logging.microsoft_level.upper(), logging.WARNING))
        
        # Set Azure SDK logger levels from configuration
        logging.getLogger('azure.core.pipeline.policies.http_logging_policy').setLevel(
            level_map.get(self.logging.azure_core_level.upper(), logging.WARNING))
        logging.getLogger('azure.monitor.opentelemetry.exporter').setLevel(
            level_map.get(self.logging.azure_monitor_level.upper(), logging.WARNING))
        logging.getLogger('azure.identity').setLevel(
            level_map.get(self.logging.azure_identity_level.upper(), logging.WARNING))
    
    def shutdown_telemetry(self):
        """Shutdown telemetry providers gracefully."""
        try:
            from ..telemetry.opentelemetry_config import otel_config
            otel_config.shutdown()
        except ImportError:
            pass


# Global app settings instance
app_settings = AppSettings()