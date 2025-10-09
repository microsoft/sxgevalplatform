using System.Text.Json;
using System.Text.Json.Serialization;
using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.Converters;

/// <summary>
/// Custom JSON converter for CreateMetricsConfigurationDto that flattens the MetricsConfiguration
/// properties into the root object during serialization
/// </summary>
public class CreateMetricsConfigurationDtoConverter : JsonConverter<CreateMetricsConfigurationDto>
{
    public override CreateMetricsConfigurationDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var dto = new CreateMetricsConfigurationDto();

        // Extract known properties
        if (root.TryGetProperty("agentId", out var agentIdElement))
            dto.AgentId = agentIdElement.GetString() ?? string.Empty;

        if (root.TryGetProperty("configurationName", out var configNameElement))
            dto.ConfigurationName = configNameElement.GetString() ?? string.Empty;

        if (root.TryGetProperty("description", out var descriptionElement))
            dto.Description = descriptionElement.GetString();

        //if (root.TryGetProperty("passingThreshold", out var thresholdElement))
        //    dto.PassingThreshold = thresholdElement.GetDouble();

        //if (root.TryGetProperty("lastUpdatedBy", out var updatedByElement))
        //    dto.LastUpdatedBy = updatedByElement.GetString() ?? string.Empty;

        //if (root.TryGetProperty("lastUpdatedAt", out var updatedAtElement))
        //    dto.LastUpdatedAt = updatedAtElement.GetDateTime();

        // For MetricsConfiguration, we want to capture the entire JSON minus the known properties
        var metricsConfigJson = new Dictionary<string, JsonElement>();
        
        foreach (var property in root.EnumerateObject())
        {
            // Skip the known DTO properties and include everything else as part of MetricsConfiguration
            if (!IsKnownProperty(property.Name))
            {
                metricsConfigJson[property.Name] = property.Value.Clone();
            }
        }

        // Convert the dictionary back to JsonElement
        var jsonString = JsonSerializer.Serialize(metricsConfigJson, options);
        dto.MetricsConfiguration = JsonDocument.Parse(jsonString).RootElement;

        return dto;
    }

    public override void Write(Utf8JsonWriter writer, CreateMetricsConfigurationDto value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write known properties
        writer.WriteString("agentId", value.AgentId);
        writer.WriteString("configurationName", value.ConfigurationName);
        
        if (value.Description != null)
            writer.WriteString("description", value.Description);
        
        //writer.WriteNumber("passingThreshold", value.PassingThreshold);
        //writer.WriteString("lastUpdatedBy", value.LastUpdatedBy);
        //writer.WriteString("lastUpdatedAt", value.LastUpdatedAt);

        // Write MetricsConfiguration properties at root level
        if (value.MetricsConfiguration.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.MetricsConfiguration.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static bool IsKnownProperty(string propertyName)
    {
        return propertyName.Equals("agentId", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("configurationName", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("description", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("passingThreshold", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("lastUpdatedBy", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("lastUpdatedAt", StringComparison.OrdinalIgnoreCase);
    }
}