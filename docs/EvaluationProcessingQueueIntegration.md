# Evaluation Processing Queue Integration

This document describes the integration of Azure Storage Queue service for evaluation processing requests in the SXG Evaluation Platform API.

## Overview

When an enriched dataset is successfully stored via the `POST /api/v1/eval/artifacts/enriched-dataset` endpoint, the system automatically sends a message to the `eval-processing-requests` Azure Storage Queue to trigger evaluation processing.

## Architecture

### Flow Diagram
```
Client Stores Enriched Dataset ? EvalArtifactsController ? EvalArtifactsRequestHandler
                                                                      ?
                                           Azure Blob Storage ? Store Enriched Dataset
                                                                      ?
                                      Azure Storage Queue ? (eval-processing-requests)
```

### Components

1. **EvalArtifactsController**: Handles the REST API request for storing enriched datasets
2. **EvalArtifactsRequestHandler**: Business logic, blob storage, and queue operations
3. **AzureQueueStorageService**: Queue operations abstraction
4. **EvalProcessingController**: Queue monitoring and management

## Queue Message Format

### Queue Name
- **Queue**: `eval-processing-requests` (configurable via `EvalProcessingRequestsQueueName` in appsettings)
- **Message Format**: JSON

### Message Schema
```json
{
  "evalRunId": "uuid",
  "metricsConfigurationId": "string",
  "enrichedDatasetId": "string",
  "datasetId": "string",
  "agentId": "string",
  "requestedAt": "datetime",
  "priority": "string",
  "enrichedDatasetBlobPath": "string",
  "metadata": {
    "key": "value"
  }
}
```

### Example Message
```json
{
  "evalRunId": "123e4567-e89b-12d3-a456-426614174000",
  "metricsConfigurationId": "456e7890-e89b-12d3-a456-426614174000",
  "enrichedDatasetId": "123e4567-e89b-12d3-a456-426614174000",
  "datasetId": "789f0123-e89b-12d3-a456-426614174000",
  "agentId": "agent-123",
  "requestedAt": "2025-01-20T10:30:00Z",
  "priority": "Normal",
  "enrichedDatasetBlobPath": "enriched-datasets/123e4567-e89b-12d3-a456-426614174000.json"
}
```

## API Endpoints

### Enriched Dataset Storage (Triggers Queue Message)
**POST** `/api/v1/eval/artifacts/enriched-dataset?evalRunId={guid}`

When called, this endpoint:
1. Stores the enriched dataset in Azure Blob Storage
2. Automatically sends processing request to `eval-processing-requests` queue
3. Returns the storage response with blob path

### Queue Monitoring Endpoints

#### Get Queue Status
**GET** `/api/v1/eval-processing/queue/status`

Returns:
- Queue existence status
- Approximate message count
- Last checked timestamp

#### Peek Queue Messages
**GET** `/api/v1/eval-processing/queue/peek?maxMessages=5`

Returns:
- Preview of pending processing requests
- Message metadata (insertion time, dequeue count, etc.)
- Does not consume messages from the queue

#### Send Test Message
**POST** `/api/v1/eval-processing/queue/test-message`

Allows manual testing of queue integration by sending test processing requests.

## Configuration

### Required Settings
```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account-name",
    "EvalProcessingRequestsQueueName": "eval-processing-requests"
  }
}
```

### Authentication
- Uses Azure Managed Identity via `DefaultAzureCredential`
- Automatically handles authentication in Azure environments
- Falls back to Azure CLI credentials for local development

## Integration Details

### EvalArtifactsRequestHandler Enhancement
The `StoreEnrichedDatasetAsync()` method now:

1. Stores the enriched dataset in Azure Blob Storage
2. Calls `SendEvalProcessingRequestAsync()` to queue the processing request
3. Uses try-catch to ensure queue errors don't affect blob storage success
4. Logs all operations for debugging and monitoring

### Message Payload Details
- **EvalRunId**: The evaluation run that needs processing
- **MetricsConfigurationId**: Configuration to use for evaluation
- **EnrichedDatasetId**: Identifier for the enriched dataset (currently uses evalRunId)
- **DatasetId**: Original dataset ID used for the evaluation
- **AgentId**: Owner of the evaluation run
- **EnrichedDatasetBlobPath**: Full path to the stored enriched dataset

## Error Handling

### Queue Message Sending
- **Non-blocking**: Queue sending errors do not fail enriched dataset storage
- **Logged**: All queue operations are comprehensively logged
- **Retryable**: Failed queue messages can be retried independently

### Message Processing
- Messages include `DequeueCount` for retry logic
- Automatic message expiration after configurable TTL
- Poison message handling via Azure Storage Queue features

## Usage Examples

### Storing an Enriched Dataset (Triggers Queue Message)
```bash
curl -X POST "https://your-api.azurewebsites.net/api/v1/eval/artifacts/enriched-dataset?evalRunId=123e4567-e89b-12d3-a456-426614174000" \
  -H "Content-Type: application/json" \
  -d '{
    "enrichedDataset": {
      "questions": [
        {"id": 1, "question": "What is AI?", "enriched_context": "..."}
      ]
    }
  }'
```

### Monitoring Queue Status
```bash
curl -X GET "https://your-api.azurewebsites.net/api/v1/eval-processing/queue/status"
```

### Peeking at Queue Messages
```bash
curl -X GET "https://your-api.azurewebsites.net/api/v1/eval-processing/queue/peek?maxMessages=10"
```

## Message Processing (Consumer Side)

### Recommended Consumer Pattern
```csharp
// Receive messages
var messages = await _queueService.ReceiveMessagesAsync("eval-processing-requests", 10);

foreach (var message in messages)
{
    try
    {
        // Parse message
        var request = JsonSerializer.Deserialize<EvalProcessingRequest>(message.MessageText);
        
        // Process evaluation using both original dataset ID and enriched dataset
        await ProcessEvaluation(request);
        
        // Delete message after successful processing
        await _queueService.DeleteMessageAsync("eval-processing-requests", 
            message.MessageId, message.PopReceipt);
    }
    catch (Exception ex)
    {
        // Log error - message will become visible again after visibility timeout
        _logger.LogError(ex, "Failed to process evaluation request: {MessageId}", message.MessageId);
    }
}
```

## Monitoring and Observability

### Key Metrics to Monitor
- Queue depth (approximate message count)
- Message processing rate
- Failed message count (high dequeue count)
- Queue operation success rates

### Logging
All queue operations are logged with:
- **Information**: Successful operations
- **Warning**: Non-critical issues (queue send failures)
- **Error**: Critical failures requiring attention

### Sample Log Messages
```
Successfully sent evaluation processing request to queue eval-processing-requests for EvalRunId: 123e4567-e89b-12d3-a456-426614174000
Failed to send evaluation processing request to queue eval-processing-requests for EvalRunId: 123e4567-e89b-12d3-a456-426614174000
```

## Testing

### Integration Testing
1. Store enriched dataset via API
2. Verify message appears in `eval-processing-requests` queue
3. Verify message content matches expected schema (including dataset ID)
4. Test queue monitoring endpoints

### Local Development
1. Use Azure Storage Emulator or Azurite for local testing
2. Azure CLI authentication for local queue access
3. Monitor logs for queue operation confirmation

## Complete Workflow

### Full Evaluation Pipeline
```
1. Create Evaluation Run
   ? (Queue: dataset-enrichment-requests)
2. Enrich Dataset
   ? (Store enriched dataset)
3. POST /api/v1/eval/artifacts/enriched-dataset
   ? (Queue: eval-processing-requests with dataset ID)
4. Process Evaluation
   ? (Store results)
5. Update Evaluation Run Status
```

## Dependencies

### Required Services
- `IAzureQueueStorageService` - Queue operations
- `IConfigHelper` - Configuration access
- `IAzureBlobStorageService` - Blob storage operations
- `IEvalRunRequestHandler` - Evaluation run data access

### Required Configurations
- `AzureStorage:EvalProcessingRequestsQueueName`
- `AzureStorage:AccountName`
- Azure Storage permissions for queue operations

## Files Created/Modified

### New Files
- `src\Sxg-Eval-Platform-Api\Models\EvalProcessingModels.cs` - Message models
- `src\Sxg-Eval-Platform-Api\Controllers\EvalProcessingController.cs` - Queue monitoring

### Modified Files
- `src\Sxg-Eval-Platform-Api\RequestHandlers\EvalArtifactsRequestHandler.cs` - Queue integration

## Future Enhancements

### Planned Features
1. **Priority Processing**: High-priority evaluation requests
2. **Batch Processing**: Process multiple evaluations together
3. **Result Callbacks**: Webhook notifications when processing completes
4. **Retry Policies**: Automatic retry with exponential backoff
5. **Dead Letter Queue**: Handle poison messages

### Integration Points
- **Status Updates**: Queue for evaluation completion notifications
- **Result Storage**: Automatic result blob creation
- **Metrics Collection**: Performance and accuracy metrics
- **Event Grid**: Publish evaluation events for broader system integration

## Troubleshooting

### Common Issues

#### Queue Messages Not Appearing
- Check Azure Storage account permissions
- Verify queue name configuration: `EvalProcessingRequestsQueueName`
- Review application logs for send failures

#### Blob Storage but No Queue Message
- Check queue service dependency injection
- Verify ConfigHelper queue name resolution
- Review try-catch block - queue errors are non-blocking

#### Authentication Errors
- Ensure Managed Identity is enabled in Azure App Service
- Verify storage account permissions for the identity
- For local development, ensure Azure CLI is authenticated

### Debug Endpoints
- `GET /api/v1/eval-processing/queue/status` - Queue health
- `GET /api/v1/eval-processing/queue/peek` - Message preview
- Application logs contain detailed operation information

This integration ensures that enriched datasets automatically trigger evaluation processing, creating a seamless pipeline from dataset enrichment to evaluation completion. The inclusion of the original dataset ID in the message payload provides consumers with access to both the original and enriched datasets for comprehensive evaluation processing.