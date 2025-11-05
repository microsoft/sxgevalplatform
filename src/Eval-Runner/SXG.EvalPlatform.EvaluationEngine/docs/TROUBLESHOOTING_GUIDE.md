# Troubleshooting Guide

## 🚨 Overview

This guide provides comprehensive troubleshooting information for the SXG Evaluation Platform Evaluation Engine, covering common issues, error codes, debugging steps, and resolution procedures.

## 📋 Quick Diagnostic Checklist

### Before You Start
- [ ] Check application logs for error messages
- [ ] Verify configuration settings in `appsettings.json`
- [ ] Confirm Azure resources are accessible
- [ ] Test network connectivity to APIs
- [ ] Check resource limits (memory, CPU, connections)

## 🔍 Common Issues & Solutions

### 1. Application Startup Issues

#### Issue: "No running event loop" Error
```
RuntimeError: no running event loop
```

**Cause**: HTTP client initialization before asyncio event loop starts

**Solution**:
```python
# Fixed in optimized version with lazy loading
# If you see this error, ensure you're using the latest optimized code
```

**Verification**:
```bash
python -c "from eval_runner.models.eval_models import DatasetItem; print('Import successful!')"
```

#### Issue: Configuration Loading Errors
```
ConfigurationError: Missing required configuration
```

**Diagnosis**:
```bash
# Check configuration file
cat appsettings.json | jq .

# Verify environment variables
env | grep -E "AZURE_|EVAL_|API_"
```

**Solutions**:
1. Verify all required configuration values are set
2. Check environment variable names match exactly
3. Ensure JSON syntax is valid
4. Validate Azure resource names

### 2. Queue Processing Issues

#### Issue: Messages Not Being Processed
```
No messages received from queue after 30 seconds
```

**Diagnosis**:
```bash
# Check queue connectivity
az storage queue list --account-name <account> --auth-mode login

# Verify queue messages
az storage message peek --queue-name <queue> --account-name <account>
```

**Solutions**:
1. **Authentication Issues**:
   ```json
   {
     "AzureStorage": {
       "UseManagedIdentity": true,
       "AccountName": "correct-storage-account"
     }
   }
   ```

2. **Queue Configuration**:
   ```json
   {
     "AzureStorage": {
       "QueueName": "eval-requests",
       "PollingIntervalSeconds": 5
     }
   }
   ```

3. **Network Connectivity**:
   ```bash
   # Test connectivity
   nslookup <storage-account>.queue.core.windows.net
   telnet <storage-account>.queue.core.windows.net 443
   ```

#### Issue: Poison Messages
```
Message failed processing 3 times, moving to poison queue
```

**Diagnosis**:
```bash
# Check poison queue
az storage message peek --queue-name eval-requests-poison
```

**Solutions**:
1. **Fix Message Format**:
   ```json
   {
     "eval_run_id": "required-field",
     "agent_id": "required-field",
     "metrics_configuration_id": "required-field"
   }
   ```

2. **Handle Corrupted Messages**:
   ```bash
   # Clear poison queue
   az storage message clear --queue-name eval-requests-poison
   ```

### 3. Evaluation Processing Issues

#### Issue: Metric Evaluation Timeouts
```
Metric evaluation timed out after 30 seconds
```

**Diagnosis**:
```bash
# Check performance logs
grep "metric_timeout_error" logs/ | tail -10

# Monitor concurrent operations
grep "concurrent.*metrics" logs/ | tail -5
```

**Solutions**:
1. **Increase Timeout**:
   ```json
   {
     "Evaluation": {
       "MetricTimeoutSeconds": 60
     }
   }
   ```

2. **Reduce Concurrency**:
   ```json
   {
     "Evaluation": {
       "MetricConcurrency": 5
     }
   }
   ```

3. **Check External Dependencies**:
   ```bash
   # Test Azure AI service connectivity
   curl -I https://<region>.api.cognitive.microsoft.com/
   ```

#### Issue: All Metrics Failed
```
All 5 metrics failed: [groundedness, relevance, coherence, fluency, similarity]
```

**Diagnosis**:
```bash
# Check Azure AI configuration
grep -r "azure_ai_project" appsettings.json

# Verify credentials
az account show
```

**Solutions**:
1. **Correct Azure AI Configuration**:
   ```json
   {
     "AzureAI": {
       "ProjectName": "your-ai-project",
       "ResourceGroupName": "your-rg",
       "SubscriptionId": "your-subscription-id"
     }
   }
   ```

2. **Check Managed Identity**:
   ```bash
   # Test managed identity token
   curl -H Metadata:true "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://cognitiveservices.azure.com/"
   ```

### 4. Performance Issues

#### Issue: Slow Processing (> 30 seconds)
```
Processing time: 45.2s (expected < 10s)
```

**Diagnosis**:
```bash
# Check performance logs
grep "performance_category.*slow" logs/ | wc -l

# Monitor resource usage
docker stats sxg-eval-runner
```

**Solutions**:
1. **Optimize Concurrency**:
   ```json
   {
     "Evaluation": {
       "DatasetConcurrency": 3,
       "MetricConcurrency": 8
     }
   }
   ```

2. **Enable Connection Pooling**:
   ```json
   {
     "HttpClient": {
       "EnableConnectionPooling": true,
       "MaxConnections": 20
     }
   }
   ```

3. **Check External API Response Times**:
   ```bash
   # Monitor API response times
   grep "api_response_time" logs/ | awk '{print $NF}' | sort -n
   ```

#### Issue: Memory Usage High (> 500MB)
```
Memory usage: 750MB (limit: 512MB)
```

**Solutions**:
1. **Reduce Concurrency**:
   ```json
   {
     "Evaluation": {
       "DatasetConcurrency": 2,
       "MetricConcurrency": 5
     }
   }
   ```

2. **Increase Container Limits**:
   ```yaml
   # docker-compose.yml
   services:
     sxg-eval-runner:
       deploy:
         resources:
           limits:
             memory: 1G
   ```

### 5. API Integration Issues

#### Issue: Configuration API Errors
```
Failed to fetch dataset. Status: 404, Error: Not Found
```

**Diagnosis**:
```bash
# Test API endpoints manually
curl -H "Authorization: Bearer $API_KEY" \
     "https://api.example.com/eval/test-123/dataset"
```

**Solutions**:
1. **Verify API Endpoints**:
   ```json
   {
     "ApiEndpoints": {
       "BaseUrl": "https://api.example.com",
       "EnrichedDatasetEndpoint": "/eval/{EvalRunId}/dataset",
       "MetricsConfigurationEndpoint": "/metrics-config/{MetricsConfigurationId}"
     }
   }
   ```

2. **Check API Authentication**:
   ```json
   {
     "ApiKeys": {
       "EvalApiKey": "valid-api-key"
     }
   }
   ```

#### Issue: Status Update Failures
```
Failed to update status. Status: 400, Error: Invalid status transition
```

**Solutions**:
1. **Check Status Transition Logic**:
   - Ensure status updates follow valid transitions
   - Handle "already in terminal state" responses as success

2. **Implement Retry Logic**:
   ```python
   # Retry with exponential backoff (already implemented)
   for attempt in range(3):
       try:
           result = await update_status(eval_run_id, status)
           break
       except Exception as e:
           await asyncio.sleep(2 ** attempt)
   ```

## 🔧 Debugging Tools & Commands

### Application Logs Analysis

#### View Recent Errors
```bash
# Last 50 error entries
grep -i error logs/app.log | tail -50

# Errors by category
grep -c "ConfigurationError\|EvaluationError\|TimeoutError" logs/app.log
```

#### Performance Analysis
```bash
# Processing times
grep "duration_ms" logs/app.log | awk '{print $NF}' | sort -n | tail -10

# Success rates
grep "success_rate" logs/app.log | awk '{print $NF}' | sort -n
```

#### Connection Pool Monitoring
```bash
# Connection lifecycle
grep -E "Created new HTTP session|Closed HTTP session" logs/app.log

# Pool utilization
netstat -an | grep :443 | wc -l
```

### Health Check Commands

#### Application Health
```bash
# Health endpoint
curl http://localhost:8080/health

# Readiness check
curl http://localhost:8080/ready
```

#### Azure Resource Health
```bash
# Storage account connectivity
az storage account show --name <account> --resource-group <rg>

# Queue service status
az storage queue stats --account-name <account>
```

### Performance Testing

#### Load Test
```bash
# Simulate concurrent requests
python scripts/load_test.py --concurrent=10 --items=25
```

#### Memory Profiling
```python
# Add to application for memory debugging
import tracemalloc
tracemalloc.start()

# ... application code ...

current, peak = tracemalloc.get_traced_memory()
print(f"Current memory usage: {current / 1024 / 1024:.1f} MB")
print(f"Peak memory usage: {peak / 1024 / 1024:.1f} MB")
```

## 📊 Error Code Reference

### Application Error Codes

| Code | Description | Severity | Action |
|------|-------------|----------|---------|
| `CONFIG_001` | Missing configuration value | Critical | Check appsettings.json |
| `CONFIG_002` | Invalid configuration format | Critical | Validate JSON syntax |
| `QUEUE_001` | Queue connection failed | Critical | Check Azure Storage |
| `QUEUE_002` | Message parsing error | Warning | Validate message format |
| `EVAL_001` | Dataset fetch failed | Error | Check API connectivity |
| `EVAL_002` | Metrics config not found | Error | Verify metrics config ID |
| `EVAL_003` | All metrics failed | Critical | Check Azure AI setup |
| `PERF_001` | Processing timeout | Warning | Increase timeout/reduce load |
| `PERF_002` | Memory limit exceeded | Critical | Reduce concurrency |
| `API_001` | Authentication failed | Critical | Check API keys |
| `API_002` | Rate limit exceeded | Warning | Implement backoff |

### HTTP Status Codes

| Status | Meaning | Common Causes | Solutions |
|--------|---------|---------------|-----------|
| 400 | Bad Request | Invalid message format | Validate JSON structure |
| 401 | Unauthorized | Invalid API key | Check authentication |
| 403 | Forbidden | Insufficient permissions | Verify access rights |
| 404 | Not Found | Resource doesn't exist | Check resource IDs |
| 408 | Request Timeout | Network/processing delay | Increase timeouts |
| 429 | Too Many Requests | Rate limiting | Implement backoff |
| 500 | Internal Server Error | Application error | Check logs |
| 502 | Bad Gateway | External service down | Check dependencies |
| 503 | Service Unavailable | Resource exhaustion | Scale resources |

## 🛠️ Advanced Debugging

### Enable Debug Logging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "eval_runner": "Debug"
    }
  }
}
```

### Performance Profiling
```python
# Add performance profiling
import cProfile
import pstats

profiler = cProfile.Profile()
profiler.enable()

# ... application code ...

profiler.disable()
stats = pstats.Stats(profiler)
stats.sort_stats('cumulative')
stats.print_stats(20)
```

### Memory Leak Detection
```python
# Monitor memory growth over time
import psutil
import time

def monitor_memory():
    process = psutil.Process()
    while True:
        memory_mb = process.memory_info().rss / 1024 / 1024
        print(f"Memory usage: {memory_mb:.1f} MB")
        time.sleep(60)
```

## 🚨 Emergency Procedures

### Critical System Issues

#### High Memory Usage (> 90%)
1. **Immediate**: Reduce concurrency to minimum
2. **Check**: Memory leak indicators in logs
3. **Scale**: Increase container memory limits
4. **Monitor**: Memory usage trends

#### Queue Backup (> 100 messages)
1. **Scale**: Increase container instances
2. **Optimize**: Reduce processing time per message
3. **Prioritize**: Process high-priority messages first
4. **Alert**: Notify operations team

#### Complete System Failure
1. **Restart**: Application containers
2. **Check**: Azure resource health
3. **Validate**: Network connectivity
4. **Escalate**: If issues persist

### Recovery Procedures

#### Message Recovery
```bash
# Move messages from poison queue back to main queue
az storage message put \
  --queue-name eval-requests \
  --content "$(az storage message get --queue-name eval-requests-poison --output tsv --query content)"
```

#### Configuration Reset
```bash
# Reset to known good configuration
cp appsettings.production.json appsettings.json
docker-compose restart sxg-eval-runner
```

## 📞 Support & Escalation

### Internal Support
- **Application Logs**: Check structured logs first
- **Performance Metrics**: Review performance dashboards
- **Health Checks**: Verify all endpoints responding

### External Dependencies
- **Azure Support**: For Azure service issues
- **API Provider**: For external API problems
- **Network Team**: For connectivity issues

### Escalation Criteria
- **Critical**: Complete system failure, data loss
- **High**: Performance degradation > 50%, multiple failures
- **Medium**: Single component failure, minor performance issues
- **Low**: Documentation, enhancement requests

This troubleshooting guide provides comprehensive coverage of common issues and their resolutions. Regular review and updates based on production experience will improve system reliability.