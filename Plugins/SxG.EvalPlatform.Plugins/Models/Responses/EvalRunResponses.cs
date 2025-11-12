namespace SxG.EvalPlatform.Plugins.Models.Responses
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using SxG.EvalPlatform.Plugins.Models.DTO;

    /// <summary>
    /// Model for individual dataset items
    /// </summary>
    public class DatasetItem
    {
        /// <summary>
        /// Question or instruction text
        /// </summary>
        public string Prompt { get; set; }

        /// <summary>
        /// Expected ground truth (optional)
        /// </summary>
        public string GroundTruth { get; set; }

        /// <summary>
        /// Actual agent response (optional)
        /// </summary>
        public string ActualResponse { get; set; }

        /// <summary>
        /// Expected response for evaluation
        /// </summary>
        public string ExpectedResponse { get; set; }
    }

    /// <summary>
    /// Response model for PostEvalRun Custom API (standardized format)
    /// </summary>
    public class PostEvalRunResponse
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        /// <param name="evalRun">The created eval run DTO</param>
        /// <returns>Success response</returns>
        public static PostEvalRunResponse CreateSuccess()
        {
            return new PostEvalRunResponse
            {
                Success = true,
                Message = "Eval run created successfully",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <returns>Error response</returns>
        public static PostEvalRunResponse CreateError(string message)
        {
            return new PostEvalRunResponse
            {
                Success = false,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Response model for GetEvalRun Custom API (detailed format with parsed dataset)
    /// </summary>
    public class GetEvalRunResponse
    {
        /// <summary>
        /// Eval Run identifier (Primary Key as GUID string)
        /// </summary>
        public string EvalRunId { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Agent identifier
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Environment identifier
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Agent schema name
        /// </summary>
        public string AgentSchemaName { get; set; }

        /// <summary>
        /// Status of the eval run as string
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Dataset as parsed JSON objects
        /// </summary>
        public List<DatasetItem> Dataset { get; set; }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        /// <param name="evalRun">The eval run DTO</param>
        /// <returns>Success response</returns>
        public static GetEvalRunResponse CreateSuccess(EvalRunDto evalRun)
        {
            if (evalRun != null)
            {
                // Parse dataset JSON string into objects
                List<DatasetItem> parsedDataset = null;
                if (!string.IsNullOrEmpty(evalRun.Dataset))
                {
                    try
                    {
                        parsedDataset = JsonConvert.DeserializeObject<List<DatasetItem>>(evalRun.Dataset);
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, return empty list
                        parsedDataset = new List<DatasetItem>();
                    }
                }

                return new GetEvalRunResponse
                {
                    EvalRunId = evalRun.EvalRunId.ToString(),
                    Message = "Eval run retrieved successfully",
                    Timestamp = DateTime.UtcNow,
                    AgentId = evalRun.AgentId,
                    EnvironmentId = evalRun.EnvironmentId,
                    AgentSchemaName = evalRun.AgentSchemaName,
                    Status = evalRun.GetStatusName(),
                    Dataset = parsedDataset
                };
            }
            else
            {
                return new GetEvalRunResponse
                {
                    EvalRunId = null,
                    Message = "Eval run not found",
                    Timestamp = DateTime.UtcNow,
                    AgentId = null,
                    EnvironmentId = null,
                    AgentSchemaName = null,
                    Status = null,
                    Dataset = null
                };
            }
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <returns>Error response</returns>
        public static GetEvalRunResponse CreateError(string message)
        {
            return new GetEvalRunResponse
            {
                EvalRunId = null,
                Message = message,
                Timestamp = DateTime.UtcNow,
                AgentId = null,
                EnvironmentId = null,
                AgentSchemaName = null,
                Status = null,
                Dataset = null
            };
        }
    }

    /// <summary>
    /// Response model for UpdateDataset Custom API (standardized format)
    /// </summary>
    public class UpdateDatasetResponse
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        /// <returns>Success response</returns>
        public static UpdateDatasetResponse CreateSuccess()
        {
            return new UpdateDatasetResponse
            {
                Success = true,
                Message = "Dataset updated successfully",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="evalRunId">Eval run ID</param>
        /// <returns>Error response</returns>
        public static UpdateDatasetResponse CreateError(string message, string evalRunId = null)
        {
            return new UpdateDatasetResponse
            {
                Success = false,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Response model for PublishEnrichedDataset Custom API (standardized format)
    /// </summary>
    public class PublishEnrichedDatasetResponse
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        /// <returns>Success response</returns>
        public static PublishEnrichedDatasetResponse CreateSuccess()
        {
            return new PublishEnrichedDatasetResponse
            {
                Success = true,
                Message = "Enriched dataset published successfully",
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="evalRunId">Eval run ID</param>
        /// <returns>Error response</returns>
        public static PublishEnrichedDatasetResponse CreateError(string message, string evalRunId = null)
        {
            return new PublishEnrichedDatasetResponse
            {
                Success = false,
                Message = message,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}