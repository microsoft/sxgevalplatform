"""
Azure AI Evaluation metrics package.
This module re-exports the new Azure AI evaluation interface.
"""

# Re-export the new Azure AI evaluation interface
from .simple_interface import registry

__all__ = ['registry']