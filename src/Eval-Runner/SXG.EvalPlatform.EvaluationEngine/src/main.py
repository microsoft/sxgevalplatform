"""
Main application entry point for the evaluation runner.
"""

import asyncio
import logging
import signal
import sys
from typing import Any
from http.server import HTTPServer, BaseHTTPRequestHandler
import threading
import json

from eval_runner.config.settings import app_settings
from eval_runner.core.evaluation_engine import evaluation_engine
from eval_runner.services.azure_storage import get_queue_service
from eval_runner.models.eval_models import QueueMessage

logger = logging.getLogger(__name__)

class HealthCheckHandler(BaseHTTPRequestHandler):
    """HTTP handler for health check endpoints."""
    
    def __init__(self, app_instance, *args, **kwargs):
        self.app_instance = app_instance
        super().__init__(*args, **kwargs)
    
    def do_GET(self):
        """Handle GET requests for health checks."""
        try:
            if self.path == '/health':
                self._handle_health_check()
            elif self.path == '/ready':
                self._handle_readiness_check()
            else:
                self._send_response(404, {'error': 'Not found'})
        except Exception as e:
            logger.error(f"Error in health check handler: {e}")
            self._send_response(500, {'error': 'Internal server error'})
    
    def _handle_health_check(self):
        """Handle liveness probe."""
        # Basic health check - service is alive
        health_status = {
            'status': 'healthy',
            'timestamp': str(asyncio.get_event_loop().time()),
            'version': '1.0.0'
        }
        self._send_response(200, health_status)
    
    def _handle_readiness_check(self):
        """Handle readiness probe."""
        # Check if app is ready to process messages
        is_ready = (
            hasattr(self.app_instance, 'running') and 
            self.app_instance.running
        )
        
        if is_ready:
            readiness_status = {
                'status': 'ready',
                'timestamp': str(asyncio.get_event_loop().time()),
                'services': {
                    'queue_service': 'connected',
                    'evaluation_engine': 'ready'
                }
            }
            self._send_response(200, readiness_status)
        else:
            readiness_status = {
                'status': 'not_ready',
                'timestamp': str(asyncio.get_event_loop().time()),
                'reason': 'Application is starting up'
            }
            self._send_response(503, readiness_status)
    
    def _send_response(self, status_code: int, data: dict):
        """Send JSON response."""
        self.send_response(status_code)
        self.send_header('Content-type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(data).encode())
    
    def log_message(self, format, *args):
        """Override to reduce logging noise."""
        pass

class EvaluationApp:
    """Main application class that coordinates queue listening and evaluation processing."""
    
    def __init__(self):
        """Initialize the application."""
        self.queue_service = get_queue_service()
        self.evaluation_engine = evaluation_engine
        self.running = False
        self._shutdown_event = asyncio.Event()
        self.health_server = None
        self.health_thread = None
    
    async def start(self) -> None:
        """Start the application."""
        logger.info("Starting Evaluation Runner Application...")
        
        # Setup signal handlers for graceful shutdown
        self._setup_signal_handlers()
        
        # Start health check server
        self._start_health_server()
        
        # Initialize services
        await self.queue_service.initialize()
        
        self.running = True
        logger.info("Application started successfully. Listening for messages...")
        
        # Start listening for queue messages
        try:
            async def message_wrapper(message):
                # Return the result from _handle_queue_message
                return await self._handle_queue_message(message)
            
            await self.queue_service.listen_for_messages(message_wrapper)
        except Exception as e:
            logger.error(f"Error in message listening loop: {e}")
            raise
        finally:
            await self.stop()
    
    async def _handle_queue_message(self, message: QueueMessage) -> bool:
        """
        Handle incoming queue message.
        
        Args:
            message: The queue message containing eval run ID
            
        Returns:
            bool: True if processing completed successfully, False otherwise
        """
        from eval_runner.utils.logging_helper import log_operation_start, log_operation_success, log_operation_error
        
        try:
            # Log operation start with structured data
            log_operation_start(
                logger, 
                "process_evaluation_message",
                eval_run_id=message.eval_run_id,
                agent_id=message.agent_id,
                metrics_configuration_id=message.metrics_configuration_id,
                enriched_dataset_id=message.enriched_dataset_id,
                priority=message.priority
            )
            
            # Process the evaluation
            processing_successful = await self.evaluation_engine.process_queue_message(message)
            
            if processing_successful:
                log_operation_success(
                    logger,
                    "process_evaluation_message", 
                    eval_run_id=message.eval_run_id,
                    result="all_steps_completed"
                )
                return True
            else:
                log_operation_error(
                    logger,
                    "process_evaluation_message",
                    Exception("One or more processing steps failed"),
                    eval_run_id=message.eval_run_id,
                    result="partial_failure"
                )
                return False
                
        except Exception as e:
            log_operation_error(
                logger,
                "process_evaluation_message",
                e,
                eval_run_id=message.eval_run_id,
                agent_id=message.agent_id,
                error_details=str(e)
            )
            return False  # Return False to indicate processing failed
    
    def _start_health_server(self) -> None:
        """Start the health check HTTP server."""
        try:
            port = 8080
            
            def handler(*args, **kwargs):
                return HealthCheckHandler(self, *args, **kwargs)
            
            self.health_server = HTTPServer(('0.0.0.0', port), handler)
            
            def run_server():
                logger.info(f"Health check server starting on port {port}")
                if self.health_server:
                    self.health_server.serve_forever()
                else:
                    logger.error("Health server is None, cannot serve")
            
            self.health_thread = threading.Thread(target=run_server, daemon=True)
            self.health_thread.start()
            logger.info("Health check server started successfully")
            
        except Exception as e:
            logger.error(f"Failed to start health check server: {e}")
            self.health_server = None
    
    def _stop_health_server(self) -> None:
        """Stop the health check HTTP server."""
        try:
            if self.health_server:
                self.health_server.shutdown()
                self.health_server.server_close()
                logger.info("Health check server stopped")
        except Exception as e:
            logger.error(f"Error stopping health check server: {e}")
    
    def _setup_signal_handlers(self) -> None:
        """Setup signal handlers for graceful shutdown."""
        def signal_handler(signum: int, frame: Any) -> None:
            logger.info(f"Received signal {signum}. Initiating graceful shutdown...")
            asyncio.create_task(self._signal_shutdown())
        
        # Handle SIGINT (Ctrl+C) and SIGTERM
        signal.signal(signal.SIGINT, signal_handler)
        signal.signal(signal.SIGTERM, signal_handler)
    
    async def _signal_shutdown(self) -> None:
        """Handle shutdown signal."""
        self._shutdown_event.set()
        await self.stop()
    
    async def stop(self) -> None:
        """Stop the application gracefully."""
        if not self.running:
            return
        
        logger.info("Stopping Evaluation Runner Application...")
        
        self.running = False
        
        # Close services
        try:
            # Stop health server
            self._stop_health_server()
            
            # Close HTTP client connections
            from eval_runner.services.http_client import api_client
            await api_client.close()
            
            # Cleanup handled by individual services
            await self.queue_service.close()
            logger.info("Application stopped successfully.")
        except Exception as e:
            logger.error(f"Error during shutdown: {e}")

async def main() -> int:
    """
    Main entry point.
    
    Returns:
        Exit code (0 for success, 1 for failure)
    """
    try:
        # Setup logging
        app_settings.setup_logging()
        
        logger.info("=" * 50)
        logger.info("SXG Evaluation Platform - Evaluation Runner")
        logger.info("=" * 50)
        
        # Validate configuration
        if not _validate_configuration():
            logger.error("Configuration validation failed. Exiting.")
            return 1
        
        # Create and start application
        app = EvaluationApp()
        await app.start()
        
        return 0
        
    except KeyboardInterrupt:
        logger.info("Application interrupted by user.")
        return 0
    except Exception as e:
        logger.error(f"Application failed with error: {e}")
        return 1

def _validate_configuration() -> bool:
    """
    Validate application configuration.
    
    Returns:
        True if configuration is valid, False otherwise
    """
    logger.info("Validating configuration...")
    
    errors = []
    
    # Check Azure Storage configuration
    if app_settings.azure_storage.use_managed_identity:
        if not app_settings.azure_storage.account_name:
            errors.append("Azure Storage account name is required for Managed Identity")
    else:
        if not app_settings.azure_storage.connection_string:
            errors.append("Azure Storage connection string is required when Managed Identity is disabled")
    
    if not app_settings.azure_storage.queue_name:
        errors.append("Azure Queue name is not configured")
    
    # Check API endpoints
    if not app_settings.api_endpoints.base_url:
        errors.append("API base URL is not configured")
    
    if not app_settings.api_endpoints.enriched_dataset_endpoint:
        errors.append("Enriched dataset API endpoint is not configured")
    
    if not app_settings.api_endpoints.metrics_configuration_endpoint:
        errors.append("Metrics configuration API endpoint is not configured")
    
    if errors:
        logger.error("Configuration errors found:")
        for error in errors:
            logger.error(f"  - {error}")
        return False
    
    logger.info("Configuration validation successful.")
    return True

def run_cli() -> None:
    """CLI entry point for running the application."""
    try:
        exit_code = asyncio.run(main())
        sys.exit(exit_code)
    except KeyboardInterrupt:
        logger.info("Application interrupted.")
        sys.exit(0)
    except Exception as e:
        logger.error(f"Fatal error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    run_cli()