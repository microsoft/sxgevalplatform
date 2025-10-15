# Azure Table Storage Partitioning Strategy for MetricsConfigurationEntity

## Overview

The `MetricsConfigurationEntity` uses `AgentId` as the partition key, which provides optimal performance for the most common query patterns in the SXG Evaluation Platform.

## Partitioning Design

### Partition Key: `AgentId`
- **Why**: Most queries will be agent-specific (e.g., "get all configurations for agent X")
- **Benefits**: 
  - Efficient single-partition queries
  - Good distribution across storage nodes
  - Scales with the number of agents in the system

### Row Key: `UUID (ConfigurationId)`
- **Why**: Guarantees absolute uniqueness and avoids character encoding issues
- **Benefits**:
  - Guaranteed uniqueness across all entities
  - No special character concerns
  - Consistent key format
  - Supports unlimited configurations per agent
  - Prevents conflicts with configuration name changes

## Query Patterns Supported

### 1. **Get All Configurations for an Agent** (Most Efficient)
```csharp
// Single partition query - very fast
var filter = $"PartitionKey eq '{agentId}'";
```

### 2. **Get Specific Configuration by ID** (Most Efficient)
```csharp
// Direct entity lookup - fastest possible
var partitionKey = agentId;
var rowKey = configurationId; // UUID
var entity = await tableClient.GetEntityAsync<MetricsConfigurationEntity>(partitionKey, rowKey);
```

### 3. **Get Configurations by Environment** (Efficient)
```csharp
// Single partition query with filter - fast
var filter = $"PartitionKey eq '{agentId}' and EnvironmentName eq '{environmentName}'";
```

### 4. **Get Configurations by Name Across Environments** (Efficient)
```csharp
// Single partition query with filter - fast
var filter = $"PartitionKey eq '{agentId}' and ConfigurationName eq '{configurationName}'";
```

## Performance Characteristics

### ? **Excellent Performance**
- All agent-specific queries (90%+ of use cases)
- Direct entity lookups by agent + configuration + environment
- Range queries within an agent's configurations

### ?? **Moderate Performance**
- Cross-agent queries (rare in this system)
- Global configuration searches (use secondary indexes if needed)

### ? **Poor Performance (Avoid)**
- Queries without specifying AgentId
- Full table scans

## Best Practices

### 1. **Always Include AgentId in Queries**
```csharp
// Good - Single partition query
var filter = $"PartitionKey eq '{agentId}' and ConfigurationName eq '{configName}'";

// Bad - Cross-partition query
var filter = $"ConfigurationName eq '{configName}'";
```

### 2. **Use Direct Entity Access When Possible**
```csharp
// Best - Direct access
var entity = await tableClient.GetEntityAsync<MetricsConfigurationEntity>(agentId, $"{configName}_{environment}");

// Good - Single partition query
var filter = $"PartitionKey eq '{agentId}' and ConfigurationName eq '{configName}'";
```

### 3. **Set Keys Properly**
```csharp
var entity = new MetricsConfigurationEntity
{
    AgentId = "agent-001",
    ConfigurationName = "accuracy-config",
    EnvironmentName = "production"
};
entity.SetKeys(); // Sets PartitionKey = AgentId, RowKey = ConfigurationName_EnvironmentName
```

## Scaling Considerations

### **Current Design Scales Well Because:**
1. **Even Distribution**: Each agent gets its own partition
2. **Predictable Growth**: Partitions grow with configurations per agent
3. **Natural Boundaries**: Agent boundaries provide logical separation

### **Potential Issues and Solutions:**
1. **Hot Partitions**: If one agent has thousands of configurations
   - **Solution**: Consider sub-partitioning by environment if needed
   - **Current**: Unlikely to be an issue with typical usage patterns

2. **Large Partitions**: If configurations become very large
   - **Solution**: Monitor partition sizes and consider archiving old configurations
   - **Current**: JSON serialization keeps data compact

## Query Examples

### Get All Configurations for Agent
```csharp
var configurations = new List<MetricsConfigurationEntity>();
var filter = $"PartitionKey eq 'agent-001'";

await foreach (var entity in tableClient.QueryAsync<MetricsConfigurationEntity>(filter))
{
    configurations.Add(entity);
}
```

### Get Specific Configuration by ID
```csharp
var entity = await tableClient.GetEntityAsync<MetricsConfigurationEntity>(
    "agent-001", 
    "12345678-1234-1234-1234-123456789012" // UUID
);
```

### Find Configuration by Name and Environment
```csharp
var filter = $"PartitionKey eq 'agent-001' and ConfigurationName eq 'accuracy-config' and EnvironmentName eq 'production'";
var matchingConfigs = tableClient.QueryAsync<MetricsConfigurationEntity>(filter);
```

### Get Configurations for Environment
```csharp
var filter = $"PartitionKey eq 'agent-001' and EnvironmentName eq 'production'";
var productionConfigs = tableClient.QueryAsync<MetricsConfigurationEntity>(filter);
```

## Migration Considerations

If you need to change the partitioning strategy in the future:
1. **Create new table** with new partitioning scheme
2. **Migrate data** using Azure Data Factory or custom migration tool
3. **Update application code** to use new table
4. **Retire old table** after verification

## Summary

Using `AgentId` as the partition key is an excellent choice for this entity because:
- ? Aligns with primary query patterns
- ? Provides excellent performance for common operations
- ? Scales naturally with the number of agents
- ? Maintains data locality for related configurations
- ? Supports efficient secondary queries by environment or configuration name