"""
Logging helper utilities for the evaluation runner with OpenTelemetry support.
"""

import logging
import time
from typing import Dict, Any, Optional
from contextlib import contextmanager

# Import OpenTelemetry modules if available
try:
    from opentelemetry import trace
    from opentelemetry.trace import Status, StatusCode
    OPENTELEMETRY_AVAILABLE = True
except ImportError:
    # Create placeholder for type checking
    class MockSpan:
        def is_recording(self):
            return False
        def set_attribute(self, key, value):
            pass
        def set_status(self, status):
            pass
    
    class MockTracer:
        @contextmanager
        def start_as_current_span(self, operation_name):
            yield MockSpan()
    
    class MockTrace:
        @staticmethod
        def get_current_span():
            return MockSpan()
        @staticmethod
        def get_tracer(name):
            return MockTracer()
    
    class MockStatusCode:
        OK = "OK"
        ERROR = "ERROR"
    
    class MockStatus:
        def __init__(self, status_code, description=None):
            self.status_code = status_code
            self.description = description
    
    trace = MockTrace()
    Status = MockStatus
    StatusCode = MockStatusCode
    OPENTELEMETRY_AVAILABLE = False

def log_structured(logger: logging.Logger, level: int, message: str, **kwargs):
    """
    Log a structured message with additional properties and OpenTelemetry context.
    
    Args:
        logger: Logger instance
        level: Logging level (logging.INFO, logging.ERROR, etc.)
        message: Primary log message
        **kwargs: Additional structured properties
    """
    # Add OpenTelemetry trace context if available
    if OPENTELEMETRY_AVAILABLE:
        span = trace.get_current_span()
        if span and span.is_recording():
            # Add span attributes for structured data
            for key, value in kwargs.items():
                if isinstance(value, (str, int, float, bool)):
                    span.set_attribute(f"custom.{key}", value)
    
    # Create extra properties for the log record
    extra_props = {
        'custom_dimensions': kwargs
    }
    
    logger.log(level, message, extra=extra_props)

def log_operation_start(logger: logging.Logger, operation_name: str, **properties):
    """Log the start of an operation with tracking properties."""
    log_structured(
        logger, 
        logging.INFO, 
        f"Starting operation: {operation_name}",
        operation_name=operation_name,
        operation_status="started",
        **properties
    )

def log_operation_success(logger: logging.Logger, operation_name: str, duration_ms: Optional[float] = None, **properties):
    """Log the successful completion of an operation."""
    props = {
        'operation_name': operation_name,
        'operation_status': 'completed',
        **properties
    }
    
    if duration_ms is not None:
        props['duration_ms'] = duration_ms
    
    log_structured(
        logger, 
        logging.INFO, 
        f"Operation completed successfully: {operation_name}",
        **props
    )

def log_operation_error(logger: logging.Logger, operation_name: str, error: Exception, **properties):
    """Log an operation error with structured error details."""
    log_structured(
        logger, 
        logging.ERROR, 
        f"Operation failed: {operation_name} - {str(error)}",
        operation_name=operation_name,
        operation_status="failed",
        error_type=type(error).__name__,
        error_message=str(error),
        **properties
    )

def log_metric(logger: logging.Logger, metric_name: str, value: float, **properties):
    """Log a custom metric value."""
    log_structured(
        logger,
        logging.INFO,
        f"Metric: {metric_name} = {value}",
        metric_name=metric_name,
        metric_value=value,
        **properties
    )

def log_evaluation_result(logger: logging.Logger, eval_run_id: str, success: bool, 
                         metrics_count: int = 0, items_count: int = 0, duration_ms: Optional[float] = None):
    """Log evaluation result with standard properties."""
    props = {
        'eval_run_id': eval_run_id,
        'evaluation_success': success,
        'metrics_evaluated': metrics_count,
        'items_processed': items_count
    }
    
    if duration_ms is not None:
        props['evaluation_duration_ms'] = duration_ms
    
    status = "completed successfully" if success else "failed"
    message = f"Evaluation {status}: {eval_run_id} ({items_count} items, {metrics_count} metrics)"
    
    log_structured(
        logger,
        logging.INFO if success else logging.ERROR,
        message,
        **props
    )


@contextmanager
def trace_operation(operation_name: str, **attributes):
    """
    Context manager for tracing operations with OpenTelemetry spans.
    
    Args:
        operation_name: Name of the operation to trace
        **attributes: Additional span attributes
    """
    if OPENTELEMETRY_AVAILABLE:
        tracer = trace.get_tracer(__name__)
        
        with tracer.start_as_current_span(operation_name) as span:
            # Set span attributes
            for key, value in attributes.items():
                if isinstance(value, (str, int, float, bool)):
                    span.set_attribute(key, value)
            
            yield span
    else:
        # OpenTelemetry not available, yield None
        yield None


def add_span_event(span, event_name: str, **attributes):
    """
    Add an event to the current span.
    
    Args:
        span: OpenTelemetry span (can be None)
        event_name: Name of the event
        **attributes: Event attributes
    """
    if span and hasattr(span, 'add_event'):
        span.add_event(event_name, attributes)


def set_span_status(span, success: bool, description: str = ""):
    """
    Set span status based on operation success.
    
    Args:
        span: OpenTelemetry span (can be None)
        success: Whether operation was successful
        description: Error description if failed
    """
    if span and hasattr(span, 'set_status') and OPENTELEMETRY_AVAILABLE:
        if success:
            span.set_status(Status(StatusCode.OK))
        else:
            span.set_status(Status(StatusCode.ERROR, description))


def log_operation_with_tracing(logger: logging.Logger, operation_name: str, **properties):
    """
    Log operation start with distributed tracing context.
    
    Args:
        logger: Logger instance
        operation_name: Name of the operation
        **properties: Additional properties to log and trace  
    """
    pass  # Implementation exists elsewhere


@contextmanager
def performance_timer(logger: logging.Logger, operation_name: str, **context):
    """
    Context manager for measuring and logging operation performance.
    
    Args:
        logger: Logger instance  
        operation_name: Name of the operation being measured
        **context: Additional context to include in performance logs
    """
    start_time = time.time()
    
    # Log operation start
    log_structured(
        logger,
        logging.INFO,
        f"Starting performance measurement for {operation_name}",
        operation=operation_name,
        measurement_type="performance_start",
        **context
    )
    
    try:
        yield start_time
    finally:
        end_time = time.time()
        duration_ms = (end_time - start_time) * 1000
        
        # Determine log level based on duration
        if duration_ms > 10000:  # > 10 seconds
            log_level = logging.WARNING
            performance_category = "slow"
        elif duration_ms > 5000:  # > 5 seconds
            log_level = logging.INFO
            performance_category = "moderate"
        else:
            log_level = logging.DEBUG
            performance_category = "fast"
        
        # Log performance results
        log_structured(
            logger,
            log_level,
            f"Performance measurement completed for {operation_name}",
            operation=operation_name,
            measurement_type="performance_complete",
            duration_ms=duration_ms,
            duration_seconds=end_time - start_time,
            performance_category=performance_category,
            **context
        )


def log_performance_metrics(logger: logging.Logger, operation: str, metrics: Dict[str, Any], **context):
    """
    Log performance metrics for monitoring and analysis.
    
    Args:
        logger: Logger instance
        operation: Operation name
        metrics: Performance metrics dictionary
        **context: Additional context
    """
    log_structured(
        logger,
        logging.INFO,
        f"Performance metrics for {operation}",
        operation=operation,
        measurement_type="performance_metrics",
        metrics=metrics,
        **context
    )