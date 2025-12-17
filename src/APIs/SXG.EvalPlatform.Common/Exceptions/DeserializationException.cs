using System;

namespace SXG.EvalPlatform.Common.Exceptions;

/// <summary>
/// Exception thrown when JSON deserialization fails.
/// Used for security logging and audit trail of deserialization failures.
/// </summary>
public class DeserializationException : Exception
{
    /// <summary>
    /// The source of the content (e.g., blob path, configuration ID)
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// The type of content being deserialized (e.g., "MetricsConfiguration", "DataSet")
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// The length of the content that failed to deserialize
    /// </summary>
    public int? ContentLength { get; }

    /// <summary>
    /// Configuration or entity ID for tracking
    /// </summary>
    public string? EntityId { get; }

    /// <summary>
    /// Additional error context (e.g., corruption pattern detected)
    /// </summary>
    public string? ErrorContext { get; }

    /// <summary>
    /// Client IP address for security logging
    /// </summary>
    public string? ClientIP { get; }

    /// <summary>
    /// User identity for security logging
    /// </summary>
    public string? UserId { get; }

    /// <summary>
    /// Request path for security logging
    /// </summary>
    public string? RequestPath { get; }

    /// <summary>
    /// Creates a new instance of DeserializationException
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="source">The source of the content</param>
    /// <param name="contentType">The type of content being deserialized</param>
    /// <param name="innerException">The underlying exception</param>
    /// <param name="contentLength">The length of the content</param>
    /// <param name="entityId">Configuration or entity ID</param>
    /// <param name="errorContext">Additional error context</param>
    /// <param name="clientIP">Client IP address for security logging</param>
    /// <param name="userId">User identity for security logging</param>
    /// <param name="requestPath">Request path for security logging</param>
    public DeserializationException(
        string message,
        string source,
        string contentType,
        Exception? innerException = null,
        int? contentLength = null,
        string? entityId = null,
        string? errorContext = null,
        string? clientIP = null,
        string? userId = null,
        string? requestPath = null)
        : base(message, innerException)
    {
        Source = source;
        ContentType = contentType;
        ContentLength = contentLength;
        EntityId = entityId;
        ErrorContext = errorContext;
        ClientIP = clientIP;
        UserId = userId;
        RequestPath = requestPath;
    }
}
