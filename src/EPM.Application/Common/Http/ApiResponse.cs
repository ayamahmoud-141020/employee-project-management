namespace EPM.Application.Common.Http;

/// <summary>
/// The envelope every endpoint returns, success or failure.
/// </summary>
/// <remarks>
/// One shape for both outcomes is what lets the Angular HTTP interceptor handle errors in a
/// single place: it reads `success` and `message` without needing to know which endpoint it
/// called or whether the failure came from validation, a business rule or a crash.
/// </remarks>
public sealed record ApiResponse<T>
{
    public bool Success { get; init; }

    /// <summary>Present on failure, and on the handful of writes with nothing to return.</summary>
    public string? Message { get; init; }

    public T? Data { get; init; }

    /// <summary>
    /// Stable error code, e.g. "Employee.EmailExists". Lets the client branch on a specific
    /// failure without string-matching the human-readable message.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Field-keyed validation messages, e.g. { "email": ["Email must be valid."] }.
    /// Null for everything that is not a validation failure.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };

    public static ApiResponse<T> Ok(T data, string message) =>
        new() { Success = true, Data = data, Message = message };
}

/// <summary>Envelope for operations that return nothing useful, such as a delete.</summary>
public sealed record ApiResponse
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public string? Code { get; init; }

    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse Ok(string message) => new() { Success = true, Message = message };

    public static ApiResponse Fail(string message, string? code = null,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, Code = code, Errors = errors };
}
