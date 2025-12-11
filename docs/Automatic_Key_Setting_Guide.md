# Automatic Key Setting in MetricsConfigurationEntity

## Overview

The `MetricsConfigurationEntity` has been enhanced with automatic key setting to improve developer experience and reduce errors. The entity now automatically manages PartitionKey and RowKey assignments when the corresponding properties are set.

## Key Changes

### 1. **Constructor Initialization**
```csharp
public MetricsConfigurationEntity()
{
    ConfigurationId = Guid.NewGuid().ToString(); // Automatically sets RowKey
    LastUpdatedOn = DateTime.UtcNow;
}
```

**Benefits:**
- ? Every new entity gets a unique ConfigurationId automatically
- ? RowKey is immediately set without manual intervention
- ? No risk of forgetting to initialize the ID

### 2. **Automatic PartitionKey Setting**
```csharp
public string AgentId 
{ 
    get => _agentId;
    set 
    { 
        _agentId = value;
        PartitionKey = value; // Automatically update PartitionKey
    }
}
```

**Benefits:**
- ? PartitionKey is always in sync with AgentId
- ? No manual `SetPartitionKey()` calls needed
- ? Eliminates errors from forgetting to set keys

### 3. **Automatic RowKey Setting**
```csharp
public string ConfigurationId 
{ 
    get => _configurationId;
    set 
    { 
        _configurationId = value;
        RowKey = value; // Automatically update RowKey
    }
}
```

**Benefits:**
- ? RowKey always matches ConfigurationId
- ? Consistent key management
- ? Prevents key mismatch issues

## Usage Examples

### **Simple Entity Creation**
```csharp
// Old way (still works)
var entity = new MetricsConfigurationEntity();
entity.AgentId = "agent-001";
entity.ConfigurationName = "test-config";
entity.SetKeys(); // Not needed anymore but still works

// New way (automatic)
var entity = new MetricsConfigurationEntity
{
    AgentId = "agent-001", // PartitionKey automatically set
    ConfigurationName = "test-config"
    // ConfigurationId and RowKey already set in constructor
};
```

### **Using Helper Methods**
```csharp
// Auto-generated ConfigurationId
var entity = MetricsConfigurationEntityHelper.CreateEntity(
    "agent-001", 
    "config-name", 
    "production"
); // All keys automatically set

// Specific ConfigurationId
var entity = MetricsConfigurationEntityHelper.CreateEntity(
    "agent-001", 
    "config-name", 
    "production", 
    "12345678-1234-1234-1234-123456789012"
); // All keys automatically set
```

### **Property Updates**
```csharp
var entity = new MetricsConfigurationEntity();

// These automatically update the corresponding keys
entity.AgentId = "new-agent-id";        // PartitionKey = "new-agent-id"
entity.ConfigurationId = "new-uuid";    // RowKey = "new-uuid"

// No manual key setting required!
```

## Backward Compatibility

All existing methods are preserved for backward compatibility:

```csharp
// These still work but are mostly redundant now
entity.SetPartitionKey();  // PartitionKey = AgentId
entity.SetRowKey();        // RowKey = ConfigurationId  
entity.SetKeys();          // Calls both methods above

entity.SetConfigurationId("new-id");       // Sets ConfigurationId and RowKey
entity.GenerateNewConfigurationId();       // Generates new GUID and sets keys
```

## Benefits Summary

### ? **Developer Experience**
- **Simpler Code**: Less boilerplate for key management
- **Fewer Errors**: Automatic synchronization prevents mistakes
- **Intuitive**: Properties work as expected without surprises

### ? **Reliability**
- **Consistent State**: Keys always match their source properties
- **No Forgotten Keys**: Constructor ensures ID is always set
- **Thread Safe**: Property setters are atomic operations

### ? **Maintainability**
- **Single Source of Truth**: Property values drive key values
- **Clear Intent**: Code shows what properties are being set
- **Easy Testing**: Predictable behavior for unit tests

## Migration Guide

### **For New Code**
```csharp
// Just set the properties - keys are automatic
var entity = new MetricsConfigurationEntity
{
    AgentId = agentId,
    ConfigurationName = configName,
    EnvironmentName = environment,
    Description = description
};
// Ready to save - no additional setup needed
```

### **For Existing Code**
```csharp
// Old code still works
var entity = new MetricsConfigurationEntity();
entity.AgentId = agentId;
entity.ConfigurationName = configName;
entity.SetKeys(); // Optional now, but harmless

// Can be simplified to
var entity = new MetricsConfigurationEntity
{
    AgentId = agentId,
    ConfigurationName = configName
}; // Keys set automatically
```

## Testing

The automatic behavior can be verified with simple tests:

```csharp
var entity = new MetricsConfigurationEntity();
entity.AgentId = "test-agent";

Assert.Equal("test-agent", entity.AgentId);
Assert.Equal("test-agent", entity.PartitionKey);
Assert.Equal(entity.ConfigurationId, entity.RowKey);
Assert.True(MetricsConfigurationEntityHelper.ValidateKeys(entity));
```

## Conclusion

The automatic key setting feature makes the `MetricsConfigurationEntity` more robust and easier to use while maintaining full backward compatibility. Developers can now focus on business logic rather than infrastructure concerns like key management.

**Key Takeaway**: Set your properties, and the entity handles the Azure Table Storage keys automatically! ??