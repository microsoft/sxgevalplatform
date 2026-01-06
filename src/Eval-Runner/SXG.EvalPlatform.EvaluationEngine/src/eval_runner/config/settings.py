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
        """Load configuration from appsettings file and override with environment variables."""
        logging.info(f"Loading configuration from: {self.config_path}")
        
        if os.path.exists(self.config_path):
            try:
                with open(self.config_path, 'r') as f:
                    config_data = json.load(f)
                logging.info(f"Successfully loaded configuration from {self.config_path}")
                
                # Override with environment variables
                config_data = self._override_with_env_vars(config_data)
                
                return config_data
            except Exception as e:
                raise ConfigurationError(f"Failed to parse configuration file {self.config_path}: {e}")
        else:
            raise ConfigurationError(f"Configuration file not found: {self.config_path}")
    
    def _override_with_env_vars(self, config_data: Dict[str, Any]) -> Dict[str, Any]:
        """
        Override configuration with environment variables.
        
        Environment variables use double underscore (__) as hierarchy separator.
        Supports up to 3 levels of nesting. Matching is case-insensitive.
        
        Examples:
            AzureStorage__AccountName -> config_data['AzureStorage']['AccountName']
            AzureAI__SubscriptionId -> config_data['AzureAI']['SubscriptionId']
            ApiEndpoints__BaseUrl -> config_data['ApiEndpoints']['BaseUrl']
            Evaluation__MaxParallelPrompts -> config_data['Evaluation']['MaxParallelPrompts']
            Logging__LogLevel__Default -> config_data['Logging']['LogLevel']['Default']
        """
        overrides_applied = []
        sensitive_keys = {'ConnectionString', 'ApiKey', 'Secret', 'Password', 'Token'}
        
        for env_key, env_value in os.environ.items():
            # Split by double underscore to get hierarchy
            if '__' in env_key:
                parts = env_key.split('__')
                
                # Support 2 or 3 level hierarchies
                if len(parts) == 2:
                    env_section, env_key_name = parts
                    
                    # Find matching section (case-insensitive)
                    section = self._find_matching_key(config_data, env_section)
                    if not section:
                        # Section doesn't exist, create it with original casing from env var
                        section = env_section
                        config_data[section] = {}
                    
                    # Find matching key within section (case-insensitive)
                    if section in config_data:
                        key = self._find_matching_key(config_data[section], env_key_name)
                        if not key:
                            # Key doesn't exist, use original casing from env var
                            key = env_key_name
                        
                        # Ensure we're not overwriting a dict with a scalar value
                        if isinstance(config_data[section].get(key), dict):
                            logging.warning(f"Skipping override for {env_key}: target is a nested object. Use 3-level syntax (e.g., {env_section}__{env_key_name}__SubKey)")
                            continue
                        
                        # Convert string values to appropriate types
                        typed_value = self._convert_env_value(env_value)
                        
                        # Apply override
                        config_data[section][key] = typed_value
                        
                        # Log override (mask sensitive values)
                        display_value = '***' if any(sens.lower() in key.lower() for sens in sensitive_keys) else typed_value
                        overrides_applied.append(f"{section}.{key} = {display_value}")
                    
                elif len(parts) == 3:
                    env_section, env_subsection, env_key_name = parts
                    
                    # Find matching section (case-insensitive)
                    section = self._find_matching_key(config_data, env_section)
                    if not section:
                        section = env_section
                        config_data[section] = {}
                    
                    # Find matching subsection (case-insensitive)
                    subsection = self._find_matching_key(config_data[section], env_subsection)
                    if not subsection:
                        subsection = env_subsection
                        config_data[section][subsection] = {}
                    
                    # Find matching key (case-insensitive)
                    if subsection in config_data[section]:
                        key = self._find_matching_key(config_data[section][subsection], env_key_name)
                        if not key:
                            key = env_key_name
                        
                        # Convert string values to appropriate types
                        typed_value = self._convert_env_value(env_value)
                        
                        # Apply override
                        config_data[section][subsection][key] = typed_value
                        
                        # Log override (mask sensitive values)
                        display_value = '***' if any(sens.lower() in key.lower() for sens in sensitive_keys) else typed_value
                        overrides_applied.append(f"{section}.{subsection}.{key} = {display_value}")
        
        if overrides_applied:
            logging.info(f"Applied {len(overrides_applied)} environment variable overrides:")
            for override in overrides_applied:
                logging.info(f"  - {override}")
        else:
            logging.info("No environment variable overrides found")
        
        return config_data
    
    def _find_matching_key(self, dictionary: Dict[str, Any], search_key: str) -> Optional[str]:
        """Find a key in dictionary matching search_key (case-insensitive). Returns the actual key from dict."""
        search_key_lower = search_key.lower()
        for key in dictionary.keys():
            if key.lower() == search_key_lower:
                return key
        return None
    
    def _convert_env_value(self, value: str) -> Any:
        """Convert environment variable string to appropriate Python type."""
        # Handle boolean values
        if value.lower() in ('true', 'yes', '1'):
            return True
        if value.lower() in ('false', 'no', '0'):
            return False
        
        # Handle numeric values
        try:
            if '.' in value:
                return float(value)
            return int(value)
        except ValueError:
            pass
        
        # Return as string
        return value
    
    def _load_azure_storage_config(self) -> AzureStorageConfig:
        """Load Azure Storage configuration."""
        azure_config = self._config_data.get('AzureStorage', {})
        
        # Helper to get value with case-insensitive key lookup
        def get_config_value(key: str, default: Any) -> Any:
            """Get configuration value with case-insensitive key matching."""
            # Try exact match first
            if key in azure_config:
                return azure_config[key]
            # Try case-insensitive match
            key_lower = key.lower()
            for k, v in azure_config.items():
                if k.lower() == key_lower:
                    return v
            return default
        
        # Check if we should use Managed Identity (default) or Connection String (fallback)
        use_managed_identity = get_config_value('UseManagedIdentity', True)
        
        return AzureStorageConfig(
            account_name=get_config_value('AccountName', ''),
            queue_name=get_config_value('QueueName', 'eval-processing-requests'),
            success_queue_name=get_config_value('SuccessQueueName', 'eval-processing-requests-completed'),
            failure_queue_name=get_config_value('FailureQueueName', 'eval-processing-requests-failed'),
            blob_container_prefix=get_config_value('BlobContainerPrefix', 'agent-'),
            use_managed_identity=use_managed_identity,
            connection_string=get_config_value('ConnectionString', '') if not use_managed_identity else None
        )
    
    def _load_api_endpoints_config(self) -> ApiEndpointsConfig:
        """Load API endpoints configuration."""
        api_config = self._config_data.get('ApiEndpoints', {})
        return ApiEndpointsConfig(
            base_url=api_config.get('BaseUrl', ''),
            enriched_dataset_endpoint=api_config.get('EnrichedDatasetEndpoint', ''),
            metrics_configuration_endpoint=api_config.get('MetricsConfigurationEndpoint', ''),
            update_status=api_config.get('UpdateStatusEndpoint', ''),  # Fixed: using correct key from JSON config
            post_results_endpoint=api_config.get('PostResultsEndpoint', '')
        )
    
    def _load_api_authentication_config(self) -> ApiAuthenticationConfig:
        """Load API authentication configuration."""
        auth_config = self._config_data.get('ApiAuthentication', {})
        return ApiAuthenticationConfig(
            client_id=auth_config.get('ClientId', '17bf598d-3033-4395-ae51-4799394c84c7'),
            tenant_id=auth_config.get('TenantId', '72f988bf-86f1-41af-91ab-2d7cd011db47'),
            resource_app_id=auth_config.get('ResourceAppId', 'ac2b08ba-4232-438f-b333-0300df1de14d'),
            scope=auth_config.get('Scope', 'api://ac2b08ba-4232-438f-b333-0300df1de14d/.default'),
            use_managed_identity=auth_config.get('UseManagedIdentity', True),
            enable_authentication=auth_config.get('EnableAuthentication', True),
            enable_token_caching=auth_config.get('EnableTokenCaching', True),
            token_refresh_buffer_seconds=auth_config.get('TokenRefreshBufferSeconds', 300)
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
            microsoft_level=log_levels.get('Microsoft', 'Warning'),
            azure_core_level=log_levels.get('azure.core.pipeline.policies.http_logging_policy', 'Warning'),
            azure_monitor_level=log_levels.get('azure.monitor.opentelemetry.exporter', 'Warning'),
            azure_identity_level=log_levels.get('azure.identity', 'Warning')
        )
    
    def _load_azure_ai_config(self) -> AzureAIConfig:
        """Load consolidated Azure AI configuration from both AzureOpenAI and AzureAI sections."""
        # Load from both sections to support backward compatibility
        openai_config = self._config_data.get('AzureOpenAI', {})
        ai_config = self._config_data.get('AzureAI', {})
        
        # Use simplified structure matching Azure AI SDK samples
        return AzureAIConfig(
            # Core Azure AI Foundry project configuration
            subscription_id=ai_config.get('SubscriptionId', openai_config.get('SubscriptionId', 'your-azure-subscription-id')),
            resource_group_name=ai_config.get('ResourceGroupName', openai_config.get('ResourceGroupName', 'your-resource-group-name')),
            resource_name=ai_config.get('ResourceName', openai_config.get('ResourceName', 'your-azure-resource-name')),
            project_name=ai_config.get('ProjectName', 'your-project-name'),
            
            # Optional OpenAI configuration for backward compatibility
            endpoint=openai_config.get('Endpoint', ''),
            deployment_name=openai_config.get('DeploymentName', 'gpt-4.1'),
            api_version=openai_config.get('ApiVersion', '2025-01-01-preview'),
            tenant_id=ai_config.get('TenantId', openai_config.get('TenantId')),
            
            # Authentication (check both sections, prefer AzureAI)
            use_managed_identity=ai_config.get('UseDefaultAzureCredential', openai_config.get('UseManagedIdentity', True)),
            api_key=openai_config.get('ApiKey')  # Only from OpenAI config
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