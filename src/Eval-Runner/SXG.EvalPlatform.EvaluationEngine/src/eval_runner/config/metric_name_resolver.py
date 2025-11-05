"""
Centralized metric name mapping and resolution service.
"""

import json
import logging
from pathlib import Path
from typing import Dict, List, Optional, Set, Union
from difflib import SequenceMatcher

logger = logging.getLogger(__name__)


class MetricNameResolver:
    """Centralized service for resolving metric names from API to registry format."""
    
    def __init__(self, config_path: Optional[Union[str, Path]] = None):
        """
        Initialize the metric name resolver.
        
        Args:
            config_path: Path to metric mappings configuration file
        """
        if config_path is None:
            config_path = Path(__file__).parent / "metric_mappings.json"
        
        self.config_path = Path(config_path)
        self._mappings = {}
        self._alternative_mappings = {}
        self._fuzzy_rules = {}
        self._registry_info = {}
        self._load_configuration()
    
    def _load_configuration(self) -> None:
        """Load metric mappings from configuration file."""
        try:
            with open(self.config_path, 'r') as f:
                config = json.load(f)
            
            self._mappings = config.get("api_to_registry_mappings", {}).get("mappings", {})
            self._alternative_mappings = config.get("alternative_names", {}).get("mappings", {})
            self._fuzzy_rules = config.get("fuzzy_match_rules", {})
            self._registry_info = config.get("registry_info", {})
            
            logger.info(f"Loaded {len(self._mappings)} primary mappings and {len(self._alternative_mappings)} alternative mappings")
            
        except Exception as e:
            logger.error(f"Failed to load metric mappings from {self.config_path}: {e}")
            # Use minimal fallback mappings
            self._mappings = {
                "Relevance": "relevance",
                "Coherence": "coherence", 
                "Groundedness": "groundedness"
            }
    
    def resolve_metric_name(self, api_name: str, available_metrics: Optional[Set[str]] = None) -> str:
        """
        Resolve API metric name to registry metric name.
        
        Args:
            api_name: Metric name from API
            available_metrics: Set of available metric names in registry (for validation)
            
        Returns:
            Resolved metric name that should exist in registry
        """
        original_name = api_name
        
        # Step 1: Try exact mapping
        if api_name in self._mappings:
            resolved = self._mappings[api_name]
            logger.debug(f"Exact mapping: '{api_name}' -> '{resolved}'")
            return resolved
        
        # Step 2: Try alternative names
        if api_name in self._alternative_mappings:
            resolved = self._alternative_mappings[api_name]
            logger.debug(f"Alternative mapping: '{api_name}' -> '{resolved}'")
            return resolved
        
        # Step 3: Try fuzzy matching if enabled and available_metrics provided
        if self._fuzzy_rules.get("enabled", False) and available_metrics:
            fuzzy_match = self._find_fuzzy_match(api_name, available_metrics)
            if fuzzy_match:
                logger.info(f"Fuzzy match: '{api_name}' -> '{fuzzy_match}'")
                return fuzzy_match
        
        # Step 4: Apply basic normalization as fallback
        normalized = self._apply_basic_normalization(api_name)
        
        # Step 5: Validate against available metrics if provided
        if available_metrics and normalized not in available_metrics:
            logger.warning(f"Metric '{api_name}' could not be resolved to any available metric")
            logger.debug(f"Available metrics: {sorted(available_metrics)}")
            # Return original name to make the issue obvious
            return original_name
        
        logger.debug(f"Basic normalization: '{api_name}' -> '{normalized}'")
        return normalized
    
    def _find_fuzzy_match(self, api_name: str, available_metrics: Set[str]) -> Optional[str]:
        """
        Find fuzzy match using similarity scoring.
        
        Args:
            api_name: API metric name to match
            available_metrics: Available registry metric names
            
        Returns:
            Best matching metric name or None
        """
        threshold = self._fuzzy_rules.get("similarity_threshold", 0.8)
        
        # Normalize the input name for comparison
        normalized_input = self._apply_basic_normalization(api_name)
        
        best_match = None
        best_score = 0.0
        
        for metric_name in available_metrics:
            # Calculate similarity between normalized names
            similarity = SequenceMatcher(None, normalized_input, metric_name).ratio()
            
            if similarity > best_score and similarity >= threshold:
                best_score = similarity
                best_match = metric_name
        
        if best_match:
            logger.debug(f"Fuzzy match score {best_score:.2f}: '{api_name}' -> '{best_match}'")
        
        return best_match
    
    def _apply_basic_normalization(self, name: str) -> str:
        """
        Apply basic normalization transformations.
        
        Args:
            name: Input metric name
            
        Returns:
            Normalized metric name
        """
        transformations = self._fuzzy_rules.get("transformations", ["lowercase", "snake_case"])
        
        result = name
        
        if "lowercase" in transformations:
            result = result.lower()
        
        if "remove_spaces" in transformations:
            result = result.replace(" ", "")
        
        if "remove_special_chars" in transformations:
            # Remove parentheses and other special chars
            result = result.replace("(", "").replace(")", "").replace("-", "_")
        
        if "snake_case" in transformations:
            # Convert to snake_case
            result = result.replace(" ", "_").replace("-", "_")
            # Clean up multiple underscores
            while "__" in result:
                result = result.replace("__", "_")
            result = result.strip("_")
        
        return result
    
    def get_all_mappings(self) -> Dict[str, str]:
        """Get all configured mappings for debugging/validation."""
        all_mappings = {}
        all_mappings.update(self._mappings)
        all_mappings.update(self._alternative_mappings)
        return all_mappings
    
    def validate_mappings(self, available_metrics: Set[str]) -> List[str]:
        """
        Validate that all configured mappings point to available metrics.
        
        Args:
            available_metrics: Set of metrics available in registry
            
        Returns:
            List of validation issues
        """
        issues = []
        
        all_mappings = self.get_all_mappings()
        for api_name, registry_name in all_mappings.items():
            if registry_name not in available_metrics:
                issues.append(f"Mapping '{api_name}' -> '{registry_name}' points to unavailable metric")
        
        return issues
    
    def add_dynamic_mapping(self, api_name: str, registry_name: str) -> None:
        """
        Add a dynamic mapping at runtime.
        
        Args:
            api_name: API metric name
            registry_name: Registry metric name
        """
        self._mappings[api_name] = registry_name
        logger.info(f"Added dynamic mapping: '{api_name}' -> '{registry_name}'")


# Global resolver instance
_resolver_instance = None

def get_metric_resolver() -> MetricNameResolver:
    """Get the global metric resolver instance."""
    global _resolver_instance
    if _resolver_instance is None:
        _resolver_instance = MetricNameResolver()
    return _resolver_instance