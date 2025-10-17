namespace SxG.EvalPlatform.Plugins.Models.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response model for PostEvalRun Custom API
    /// </summary>
    public class PostEvalRunResponse
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Unique identifier of the created eval job
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// HTTP status code
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Eval Job ID (Primary Key)
        /// </summary>
        public string EvalJobId { get; set; }

        /// <summary>
        /// Agent identifier
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Environment identifier
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Schema name
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// Status of the eval job
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        /// <param name="evalJob">The created eval job entity</param>
        /// <returns>Success response</returns>
        public static PostEvalRunResponse CreateSuccess(EvalJobEntity evalJob)
        {
            return new PostEvalRunResponse
            {
                Success = true,
                Id = evalJob.Id,
                Message = "Eval job created successfully",
                StatusCode = 202, // Accepted
                Timestamp = DateTime.UtcNow,
                EvalJobId = evalJob.EvalJobId.ToString(),
                AgentId = evalJob.AgentId,
                EnvironmentId = evalJob.EnvironmentId,
                SchemaName = evalJob.SchemaName,
                Status = evalJob.Status
            };
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <returns>Error response</returns>
        public static PostEvalRunResponse CreateError(string message, int statusCode = 400)
        {
            return new PostEvalRunResponse
            {
                Success = false,
                Id = null,
                Message = message,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow,
                EvalJobId = null,
                AgentId = null,
                EnvironmentId = null,
                SchemaName = null,
                Status = 0
            };
        }
    }

    /// <summary>
    /// Response model for GetEvalRun Custom API
    /// </summary>
    public class GetEvalRunResponse
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The eval job record (single record since we're retrieving by Id)
        /// </summary>
        public EvalJobEntity EvalJob { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// HTTP status code
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Eval Job ID (Primary Key)
        /// </summary>
        public string EvalJobId { get; set; }

        /// <summary>
        /// Agent identifier
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Environment identifier
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Schema name
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// Status of the eval job
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Input JSON data
        /// </summary>
        public string Input { get; set; }

        /// <summary>
        /// Output JSON data
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// Creates a successful response
        /// </summary>
        /// <param name="evalJob">The eval job record</param>
        /// <returns>Success response</returns>
        public static GetEvalRunResponse CreateSuccess(EvalJobEntity evalJob)
        {
            if (evalJob != null)
            {
                return new GetEvalRunResponse
                {
                    Success = true,
                    EvalJob = evalJob,
                    Message = "Eval job retrieved successfully",
                    StatusCode = 200, // OK
                    Timestamp = DateTime.UtcNow,
                    EvalJobId = evalJob.EvalJobId.ToString(),
                    AgentId = evalJob.AgentId,
                    EnvironmentId = evalJob.EnvironmentId,
                    SchemaName = evalJob.SchemaName,
                    Status = evalJob.Status,
                    Input = evalJob.Input,
                    Output = evalJob.Output
                };
            }
            else
            {
                return new GetEvalRunResponse
                {
                    Success = true,
                    EvalJob = null,
                    Message = "Eval job not found",
                    StatusCode = 404, // Not Found
                    Timestamp = DateTime.UtcNow,
                    EvalJobId = null,
                    AgentId = null,
                    EnvironmentId = null,
                    SchemaName = null,
                    Status = 0,
                    Input = null,
                    Output = null
                };
            }
        }

        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <returns>Error response</returns>
        public static GetEvalRunResponse CreateError(string message, int statusCode = 400)
        {
            return new GetEvalRunResponse
            {
                Success = false,
                EvalJob = null,
                Message = message,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow,
                EvalJobId = null,
                AgentId = null,
                EnvironmentId = null,
                SchemaName = null,
                Status = 0,
                Input = null,
                Output = null
            };
        }
    }
}