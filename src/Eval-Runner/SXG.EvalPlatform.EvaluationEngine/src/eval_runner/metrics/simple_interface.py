"""
Evaluation interface using Azure AI Evaluation metrics.
This module provides backwards compatibility by re-exporting the new Azure AI interface.
"""

from typing import Dict

# Re-export the new Azure AI interface
from .azure_ai_interface import MetricAdapter, AzureAIMetricsRegistry, registry as _azure_registry

# For backwards compatibility, alias the old class names to new ones
IEvaluationMetric = MetricAdapter
BaseMetric = MetricAdapter
MetricRegistry = AzureAIMetricsRegistry


class BackCompatRegistry:
    """Backwards compatible registry wrapper."""
    
    def __init__(self, azure_registry):
        self._azure_registry = azure_registry
    
    def get_all_metrics(self) -> Dict[str, Dict[str, MetricAdapter]]:
        """Get metrics organized by category (new interface)."""
        return self._azure_registry.get_all_metrics()
    
    def get_all_metrics_flat(self) -> Dict[str, MetricAdapter]:
        """Get flat dictionary of all metrics (backwards compatibility)."""
        return self._azure_registry.get_all_metrics_flat()
    
    def __getattr__(self, name):
        """Proxy all other attributes to the Azure registry."""
        return getattr(self._azure_registry, name)


# Global registry instance with backwards compatibility
registry = BackCompatRegistry(_azure_registry)