# Dataset Enrichment Queue Integration

This document describes the integration of Azure Storage Queue service for dataset enrichment requests in the SXG Evaluation Platform API.

## Overview

When a new evaluation run is created via the `POST /api/v1/eval/runs` endpoint, the system automatically sends a message to the `enrich-dataset-request` Azure Storage Queue to trigger dataset enrichment processing.

## Architecture

### Flow Diagram
```
Client Request ? EvalRunController ? EvalRunRequestHandler ? Azure Table Storage
                                                          ?
                                   Azure Storage Queue ? (enrich-dataset-request)
```

### Components

1. **EvalRunController**: Handles the REST API request
2. **EvalRunRequestHandler**: Business logic and orchestration
3. **AzureQueueStorageService**: Queue operations abstraction
4. **DatasetEnrichmentController**: Queue monitoring and management

## Queue Message Format

### Queue Name
- **Queue**: `enrich-dataset-request`
- **Message Format**: JSON

### Message Schema
```json
{
  "evalRunId": "uuid",
  "datasetId": "uuid", 
  "agentId": "string",
  "requestedAt": "datetime",
  "priority": "string",
  "metadata": {
    "key": "value"
  }
}
```

### Example Message
```json
{
  "evalRunId": "123e4567-e89b-12d3-a456-426614174000",
  "datasetId": "987fcdeb-51d2-43e8-9f6a-123456789abc",
  "agentId": "agent-123",
  "requestedAt": "2025-01-20T10:30:00Z",
  "priority": "Normal"
}
```

## API Endpoints

### Evaluation Run Creation
**POST** `/api/v1/eval/runs`

When called, this endpoint:
1. Creates a new evaluation run in Azure Table Storage
2. Automatically sends enrichment request to `enrich-dataset-request` queue
3. Returns the created evaluation run details

### Queue Monitoring Endpoints

#### Get Queue Status
**GET** `/api/v1/dataset-enrichment/queue/status`

Returns:
- Queue existence status
- Approximate message count
- Last checked timestamp

#### Peek Queue Messages
**GET** `/api/v1/dataset-enrichment/queue/peek?maxMessages=5`

Returns:
- Preview of pending enrichment requests
- Message metadata (insertion time, dequeue count, etc.)
- Does not consume messages from the queue

#### Send Test Message
**POST** `/api/v1/dataset-enrichment/queue/test-message`

Allows manual testing of queue integration by sending test enrichment requests.

## Configuration

### Required Settings
```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account-name"
  }
}
```

### Authentication
- Uses Azure Managed Identity via `DefaultAzureCredential`
- Automatically handles authentication in Azure environments
- Falls back to Azure CLI credentials for local development

## Error Handling

### Queue Message Sending
- **Non-blocking**: Queue sending errors do not fail evaluation run creation
- **Logged**: All queue operations are comprehensively logged
- **Retryable**: Failed queue messages can be retried independently

### Message Processing
- Messages include `DequeueCount` for retry logic
- Automatic message expiration after configurable TTL
- Poison message handling via Azure Storage Queue features

## Usage Examples

### Creating an Evaluation Run (Triggers Queue Message)
```bash
curl -X POST "https://your-api.azurewebsites.net/api/v1/eval/runs" \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "agent-123",
    "dataSetId": "987fcdeb-51d2-43e8-9f6a-123456789abc",
    "metricsConfigurationId": "456e7890-e89b-12d3-a456-426614174000",
    "type": "MCS",
    "environmentId": "789e1234-e89b-12d3-a456-426614174000",
    "agentSchemaName": "MyAgent.Schema"
  }'
```

### Monitoring Queue Status
```bash
curl -X GET "https://your-api.azurewebsites.net/api/v1/dataset-enrichment/queue/status"
```

### Peeking at Queue Messages
```bash
curl -X GET "https://your-api.azurewebsites.net/api/v1/dataset-enrichment/queue/peek?maxMessages=10"
```

## Implementation Details

### EvalRunRequestHandler Integration
The `EvalRunRequestHandler.CreateEvalRunAsync()` method now:

1. Creates the evaluation run in Azure Table Storage
2. Calls `SendDatasetEnrichmentRequestAsync()` to queue the enrichment request
3. Uses try-catch to ensure queue errors don't affect evaluation run creation
4. Logs all operations for debugging and monitoring

### Queue Service Usage
```csharp
// Automatic queue creation
var queueClient = await GetQueueClientAsync(queueName);

// JSON serialization with camelCase
var messageContent = JsonSerializer.Serialize(enrichmentRequest, 
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

// Send message
await _queueStorageService.SendMessageAsync("enrich-dataset-request", messageContent);
```

## Message Processing (Consumer Side)

### Recommended Consumer Pattern
```csharp
// Receive messages
var messages = await _queueService.ReceiveMessagesAsync("enrich-dataset-request", 10);

foreach (var message in messages)
{
    try
    {
        // Parse message
        var request = JsonSerializer.Deserialize<DatasetEnrichmentRequest>(message.MessageText);
        
        // Process enrichment
        await ProcessDatasetEnrichment(request);
        
        // Delete message after successful processing
        await _queueService.DeleteMessageAsync("enrich-dataset-request", 
            message.MessageId, message.PopReceipt);
    }
    catch (Exception ex)
    {
        // Log error - message will become visible again after visibility timeout
        _logger.LogError(ex, "Failed to process enrichment request: {MessageId}", message.MessageId);
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
Successfully sent dataset enrichment request to queue for EvalRunId: 123e4567-e89b-12d3-a456-426614174000
Failed to send dataset enrichment request to queue for EvalRunId: 123e4567-e89b-12d3-a456-426614174000
```

## Testing

### Integration Testing
1. Create evaluation run via API
2. Verify message appears in `enrich-dataset-request` queue
3. Verify message content matches expected schema
4. Test queue monitoring endpoints

### Local Development
1. Use Azure Storage Emulator or Azurite for local testing
2. Azure CLI authentication for local queue access
3. Monitor logs for queue operation confirmation

## Deployment Considerations

### Permissions Required
- **Storage Queue Data Contributor** role on the storage account
- **Storage Queue Data Reader** for monitoring operations
- **Storage Queue Data Message Processor** for consuming messages

### Scaling
- Queue operations are asynchronous and non-blocking
- Multiple consumer instances can process messages concurrently
- Azure Storage Queues handle up to 20k messages per second

### Reliability
- Messages persist until explicitly deleted
- Automatic retry via message visibility timeout
- Dead letter queue pattern can be implemented for poison messages

## Future Enhancements

### Planned Features
1. **Priority Queues**: Separate queues for high/normal/low priority
2. **Message Scheduling**: Delayed processing capabilities
3. **Batch Processing**: Bulk message operations
4. **Dead Letter Queue**: Automatic poison message handling
5. **Metrics Dashboard**: Real-time queue monitoring UI

### Integration Points
- **Enrichment Completion**: Queue for notifying completion status
- **Workflow Orchestration**: Integration with Azure Logic Apps or Functions
- **Event Grid**: Publish enrichment events for broader system integration

## Troubleshooting

### Common Issues

#### Queue Messages Not Appearing
- Check Azure Storage account permissions
- Verify queue name spelling: `enrich-dataset-request`
- Review application logs for send failures

#### Authentication Errors
- Ensure Managed Identity is enabled in Azure App Service
- Verify storage account permissions for the identity
- For local development, ensure Azure CLI is authenticated

#### Message Processing Failures
- Check message format matches expected schema
- Verify JSON serialization settings (camelCase)
- Monitor dequeue count for poison messages

### Debug Endpoints
- `GET /api/v1/dataset-enrichment/queue/status` - Queue health
- `GET /api/v1/dataset-enrichment/queue/peek` - Message preview
- Application logs contain detailed operation information