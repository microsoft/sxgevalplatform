using System;

namespace SXG.EvalPlatform.Common.Exceptions;

/// <summary>
/// Exception thrown when a cryptographic operation fails during Azure Storage operations.
/// Used for security logging and audit trail of cryptographic failures.
/// </summary>
public class CryptographicOperationException : Exception
{
    /// <summary>
    /// The type of operation that failed (e.g., "ReadBlob", "WriteBlob", "DeleteBlob")
    /// </summary>
    public string OperationType { get; }

    /// <summary>
    /// The source of the failure (e.g., container name, blob path)
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Azure error code if available
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// HTTP status code if available (e.g., 401, 403 for auth failures)
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Client IP address for security logging
    /// </summary>
    public string? ClientIP { get; }

    /// <summary>
    /// Creates a new instance of CryptographicOperationException
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="operationType">The type of operation that failed</param>
    /// <param name="source">The source of the failure</param>
    /// <param name="innerException">The underlying exception</param>
    /// <param name="errorCode">Azure error code if available</param>
    /// <param name="statusCode">HTTP status code if available</param>
    /// <param name="clientIP">Client IP address for security logging</param>
    public CryptographicOperationException(
        string message,
        string operationType,
        string source,
        Exception? innerException = null,
        string? errorCode = null,
        int? statusCode = null,
        string? clientIP = null)
        : base(message, innerException)
    {
        OperationType = operationType;
        Source = source;
        ErrorCode = errorCode;
        StatusCode = statusCode;
        ClientIP = clientIP;
    }
}
