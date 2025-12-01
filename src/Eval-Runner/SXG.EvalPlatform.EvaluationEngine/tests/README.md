# Test Scripts

This directory contains test scripts for verifying the evaluation platform functionality.

## Available Tests

- **test_auth.py**: Test authentication token acquisition
- **test_auth_feature_flag.py**: Test authentication with feature flags
- **test_api_calls.py**: Test full API call functionality

## Running Tests

```bash
# Test authentication first
cd tests
python test_auth.py

# Test API calls (requires valid eval_run_id)
python test_api_calls.py

# Test authentication with feature flags
python test_auth_feature_flag.py
```

## Prerequisites

- Python environment configured with all dependencies
- Valid Azure credentials (managed identity or service principal)
- Proper appsettings configuration for the target environment