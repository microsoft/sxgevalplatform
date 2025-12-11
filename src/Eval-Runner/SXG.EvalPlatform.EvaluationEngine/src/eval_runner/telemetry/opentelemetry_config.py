"""
OpenTelemetry configuration for the evaluation runner.
Provides structured logging, tracing, and metrics using OpenTelemetry standard.
"""

import logging
import os
from typing import Optional, Dict, Any
from opentelemetry import trace, metrics
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.resources import Resource
from opentelemetry._logs import set_logger_provider
from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
from opentelemetry.sdk._logs.export import BatchLogRecordProcessor


class OpenTelemetryConfig:
    """OpenTelemetry configuration and setup."""
    
    def __init__(self, service_name: str = "sxg-evaluation-engine", service_version: str = "1.0.0"):
        self.service_name = service_name
        self.service_version = service_version
        self.resource = Resource.create({
            "service.name": service_name,
            "service.version": service_version,
            "service.instance.id": os.environ.get("HOSTNAME", "unknown"),
            "deployment.environment": os.environ.get("RUNTIME_ENVIRONMENT", "Local")
        })
        
        self.tracer_provider: Optional[TracerProvider] = None
        self.meter_provider: Optional[MeterProvider] = None
        self.logger_provider: Optional[LoggerProvider] = None
        
    def setup_telemetry(self, connection_string: str, enable_console: bool = True) -> None:
        """
        Set up OpenTelemetry with Azure Monitor export.
        
        Args:
            connection_string: Application Insights connection string
            enable_console: Whether to also log to console
        """
        try:
            from azure.monitor.opentelemetry.exporter import AzureMonitorTraceExporter, AzureMonitorLogExporter, AzureMonitorMetricExporter
            
            # Set up tracing
            self._setup_tracing(connection_string)
            
            # Set up metrics
            self._setup_metrics(connection_string)
            
            # Set up logging
            self._setup_logging(connection_string, enable_console)
            
            # Set up HTTP instrumentation for dependency tracking
            self._setup_http_instrumentation()
            
            print(f"[SUCCESS] OpenTelemetry configured with Azure Monitor")
            
        except ImportError as e:
            print(f"[WARNING]  Azure Monitor OpenTelemetry packages not installed: {e}")
            print("   Run: pip install azure-monitor-opentelemetry-exporter")
            # Fall back to console-only setup
            self._setup_console_only()
            
        except Exception as e:
            print(f"[WARNING]  Failed to configure OpenTelemetry: {e}")
            self._setup_console_only()
    
    def _setup_tracing(self, connection_string: str) -> None:
        """Set up distributed tracing."""
        from azure.monitor.opentelemetry.exporter import AzureMonitorTraceExporter
        
        # Create tracer provider
        self.tracer_provider = TracerProvider(resource=self.resource)
        trace.set_tracer_provider(self.tracer_provider)
        
        # Add Azure Monitor exporter
        azure_exporter = AzureMonitorTraceExporter(connection_string=connection_string)
        span_processor = BatchSpanProcessor(azure_exporter)
        self.tracer_provider.add_span_processor(span_processor)
    
    def _setup_metrics(self, connection_string: str) -> None:
        """Set up custom metrics."""
        from azure.monitor.opentelemetry.exporter import AzureMonitorMetricExporter
        
        # Create metric exporter
        azure_metric_exporter = AzureMonitorMetricExporter(connection_string=connection_string)
        metric_reader = PeriodicExportingMetricReader(
            exporter=azure_metric_exporter,
            export_interval_millis=60000  # Export every 60 seconds
        )
        
        # Create meter provider
        self.meter_provider = MeterProvider(
            resource=self.resource,
            metric_readers=[metric_reader]
        )
        metrics.set_meter_provider(self.meter_provider)
    
    def _setup_logging(self, connection_string: str, enable_console: bool) -> None:
        """Set up structured logging with OpenTelemetry."""
        from azure.monitor.opentelemetry.exporter import AzureMonitorLogExporter
        
        # Create logger provider
        self.logger_provider = LoggerProvider(resource=self.resource)
        set_logger_provider(self.logger_provider)
        
        # Add Azure Monitor log exporter
        azure_log_exporter = AzureMonitorLogExporter(connection_string=connection_string)
        log_processor = BatchLogRecordProcessor(azure_log_exporter)
        self.logger_provider.add_log_record_processor(log_processor)
        
        # Configure Python logging to use OpenTelemetry
        handler = LoggingHandler(level=logging.NOTSET, logger_provider=self.logger_provider)
        
        # Get root logger and configure
        root_logger = logging.getLogger()
        root_logger.addHandler(handler)
        
        # Add console handler if requested
        if enable_console:
            console_handler = logging.StreamHandler()
            console_handler.setLevel(logging.INFO)
            # Use simple formatter to avoid missing otelTraceID/otelSpanID fields
            formatter = logging.Formatter(
                '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
            )
            console_handler.setFormatter(formatter)
            root_logger.addHandler(console_handler)
    
    def _setup_console_only(self) -> None:
        """Fallback to console-only logging when Azure Monitor is not available."""
        print("[WARNING]  Setting up console-only logging (Azure Monitor unavailable)")
        
        root_logger = logging.getLogger()
        console_handler = logging.StreamHandler()
        console_handler.setLevel(logging.INFO)
        formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
        console_handler.setFormatter(formatter)
        root_logger.addHandler(console_handler)
    
    def get_tracer(self, name: str) -> trace.Tracer:
        """Get a tracer instance."""
        return trace.get_tracer(name, self.service_version)
    
    def get_meter(self, name: str) -> metrics.Meter:
        """Get a meter instance for custom metrics."""
        return metrics.get_meter(name, self.service_version)
    
    def shutdown(self) -> None:
        """Shutdown OpenTelemetry providers."""
        if self.tracer_provider:
            self.tracer_provider.shutdown()
        if self.meter_provider:
            self.meter_provider.shutdown()
        if self.logger_provider:
            self.logger_provider.shutdown()
    
    def _setup_http_instrumentation(self) -> None:
        """
        Set up HTTP client instrumentation for dependency tracking.
        This captures Azure OpenAI API calls as dependencies in Application Insights.
        """
        try:
            # Import HTTP instrumentors
            from opentelemetry.instrumentation.aiohttp_client import AioHttpClientInstrumentor
            from opentelemetry.instrumentation.requests import RequestsInstrumentor
            from opentelemetry.instrumentation.urllib3 import URLLib3Instrumentor
            from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
            
            # Instrument HTTP clients
            AioHttpClientInstrumentor().instrument()
            RequestsInstrumentor().instrument()
            URLLib3Instrumentor().instrument()
            HTTPXClientInstrumentor().instrument()
            
            print(f"[SUCCESS] HTTP client instrumentation enabled for dependency tracking")
            print(f"   - aiohttp: Azure AI SDK internal HTTP calls will be tracked")
            print(f"   - requests: Standard HTTP library calls will be tracked")
            print(f"   - urllib3: Low-level HTTP calls will be tracked")
            print(f"   - httpx: Modern async HTTP calls will be tracked")
            
        except ImportError as e:
            print(f"[WARNING] HTTP instrumentation packages not available: {e}")
            print("   Azure OpenAI dependencies may not appear in Application Insights")
            print("   Install with: pip install -r requirements.txt")
        except Exception as e:
            print(f"[WARNING] Failed to set up HTTP instrumentation: {e}")


# Global OpenTelemetry instance
otel_config = OpenTelemetryConfig()