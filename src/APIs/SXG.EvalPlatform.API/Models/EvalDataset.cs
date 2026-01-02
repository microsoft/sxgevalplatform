using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Data.Tables;

namespace SxgEvalPlatformApi.Models
{
    /// <summary>
    /// Represents a single dataset record for evaluation
    /// </summary>
    [JsonConverter(typeof(EvalDatasetJsonConverter))]
    public class EvalDataset
    {
        /// <summary>
        /// Canonical user message for this turn.
        /// In JSON, this is typically "query" (legacy producers may send "prompt").
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Ground truth / expected answer for this turn.
        /// </summary>
        public string GroundTruth { get; set; } = string.Empty;

        /// <summary>
        /// Actual response produced by the agent (enrichment fills this).
        /// </summary>
        public string ActualResponse { get; set; } = string.Empty;

        /// <summary>
        /// Optional evaluation context. Supports either a string or array/object payload.
        /// </summary>
        public JsonElement? Context { get; set; }

        /// <summary>
        /// Optional expected response field used by some legacy pipelines.
        /// </summary>
        public string? ExpectedResponse { get; set; }

        /// <summary>
        /// Logical conversation grouping key in the dataset.
        /// </summary>
        public string? ConversationId { get; set; }

        /// <summary>
        /// Turn ordering within a conversation.
        /// </summary>
        public int? TurnIndex { get; set; }

        /// <summary>
        /// Copilot runtime conversation id (x-ms-conversation-id) used to continue multi-turn.
        /// Typically filled during enrichment.
        /// </summary>
        public string? CopilotConversationId { get; set; }
    }

    /// <summary>
    /// Backward-compatible JSON converter for EvalDataset.
    /// Accepts both legacy single-turn fields and multi-turn fields.
    /// Canonical output fields are: query, groundTruth, actualResponse, context, expectedResponse, conversationId, turnIndex, copilotConversationId.
    /// </summary>
    internal sealed class EvalDatasetJsonConverter : JsonConverter<EvalDataset>
    {
        public override EvalDataset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new EvalDataset();
            }

            string query = GetString(root, "query")
                ?? GetString(root, "prompt")
                ?? GetString(root, "Prompt")
                ?? string.Empty;

            string groundTruth = GetString(root, "groundTruth")
                ?? GetString(root, "GroundTruth")
                ?? GetString(root, "ground_truth")
                ?? string.Empty;

            string actualResponse = GetString(root, "actualResponse")
                ?? GetString(root, "ActualResponse")
                ?? GetString(root, "actual_response")
                ?? string.Empty;

            string? expectedResponse = GetString(root, "expectedResponse")
                ?? GetString(root, "ExpectedResponse");

            JsonElement? context = null;
            if (root.TryGetProperty("context", out var contextElement) || root.TryGetProperty("Context", out contextElement))
            {
                context = contextElement.Clone();
            }

            string? conversationId = GetString(root, "conversationId")
                ?? GetString(root, "ConversationId");

            int? turnIndex = GetInt(root, "turnIndex")
                ?? GetInt(root, "TurnIndex");

            string? copilotConversationId = GetString(root, "copilotConversationId")
                ?? GetString(root, "CopilotConversationId")
                ?? GetString(root, "x-ms-conversation-id");

            return new EvalDataset
            {
                Query = query,
                GroundTruth = groundTruth,
                ActualResponse = actualResponse,
                Context = context,
                ExpectedResponse = expectedResponse,
                ConversationId = conversationId,
                TurnIndex = turnIndex,
                CopilotConversationId = copilotConversationId,
            };
        }

        public override void Write(Utf8JsonWriter writer, EvalDataset value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("query", value.Query ?? string.Empty);
            writer.WriteString("groundTruth", value.GroundTruth ?? string.Empty);
            writer.WriteString("actualResponse", value.ActualResponse ?? string.Empty);

            if (value.Context.HasValue)
            {
                writer.WritePropertyName("context");
                value.Context.Value.WriteTo(writer);
            }

            if (!string.IsNullOrWhiteSpace(value.ExpectedResponse))
            {
                writer.WriteString("expectedResponse", value.ExpectedResponse);
            }

            if (!string.IsNullOrWhiteSpace(value.ConversationId))
            {
                writer.WriteString("conversationId", value.ConversationId);
            }

            if (value.TurnIndex.HasValue)
            {
                writer.WriteNumber("turnIndex", value.TurnIndex.Value);
            }

            if (!string.IsNullOrWhiteSpace(value.CopilotConversationId))
            {
                writer.WriteString("copilotConversationId", value.CopilotConversationId);
            }

            writer.WriteEndObject();
        }

        private static string? GetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element))
            {
                return null;
            }

            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        }

        private static int? GetInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var element))
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
            {
                return number;
            }

            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }
    }

    /// <summary>
    /// DTO for saving datasets
    /// </summary>
    public class SaveDatasetDto
    {
        [Required]
        public string AgentId { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Synthetic|Golden)$", ErrorMessage = "DatasetType must be either 'Synthetic' or 'Golden'")]
        public string DatasetType { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string DatasetName { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<EvalDataset> DatasetRecords { get; set; } = new();
    }

    /// <summary>
    /// DTO for updating datasets
    /// </summary>
    public class UpdateDatasetDto
    {
        [Required]
        [MinLength(1)]
        public List<EvalDataset> DatasetRecords { get; set; } = new();
    }

    /// <summary>
    /// Dataset types constants
    /// </summary>
    public static class DatasetTypes
    {
        public const string Synthetic = "Synthetic";
        public const string Golden = "Golden";
    }

    /// <summary>
    /// Response DTO for dataset save operation
    /// </summary>
    public class DatasetSaveResponseDto
    {
        public string DatasetId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? LastUpdatedBy { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
    }

    /// <summary>
    /// Dataset metadata entity for Azure Table Storage
    /// </summary>
    public class DatasetMetadataEntity : ITableEntity
    {
        /// <summary>
        /// Dataset ID - GUID generated automatically
        /// </summary>
        public string DatasetId { get; set; } = string.Empty;

        /// <summary>
        /// Last updated timestamp
        /// </summary>
        public DateTime LastUpdatedOn { get; set; }

        /// <summary>
        /// Agent ID
        /// </summary>
        public string AgentId { get; set; } = string.Empty;

        /// <summary>
        /// Container name where blob is stored
        /// </summary>
        public string ContainerName { get; set; } = string.Empty;

        /// <summary>
        /// Blob file path including folder and file name
        /// </summary>
        public string BlobFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Dataset type: Synthetic or Golden
        /// </summary>
        public string DatasetType { get; set; } = string.Empty;

        /// <summary>
        /// Dataset name
        /// </summary>
        public string DatasetName { get; set; } = string.Empty;

        /// <summary>
        /// Number of records in the dataset
        /// </summary>
        public int RecordCount { get; set; }

        // ITableEntity implementation
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }

    /// <summary>
    /// DTO for dataset metadata list response
    /// </summary>
    public class DatasetMetadataDto
    {
        public string DatasetId { get; set; } = string.Empty;
        public string AgentId { get; set; } = string.Empty;
        public string DatasetType { get; set; } = string.Empty;
        public string DatasetName { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string LastUpdatedBy { get; set; } = string.Empty;
        public DateTime LastUpdatedOn { get; set; }
    }

    /// <summary>
    /// Response DTO for dataset list by agent
    /// </summary>
    public class DatasetListResponseDto
    {
        public string AgentId { get; set; } = string.Empty;
        public List<DatasetMetadataDto> Datasets { get; set; } = new();
    }
}

