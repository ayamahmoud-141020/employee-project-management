namespace EPM.Domain.Abstractions;

/// <summary>
/// Why an operation was refused. The <see cref="Type"/> is what the API layer turns into an
/// HTTP status code, so a handler never has to know or care about status codes itself.
/// </summary>
public enum ErrorType
{
    /// <summary>Input was malformed or out of range — 400.</summary>
    Validation,

    /// <summary>The thing being addressed does not exist — 404.</summary>
    NotFound,

    /// <summary>The request is well formed but clashes with current state — 409.</summary>
    Conflict,

    /// <summary>Authenticated, but not allowed to do this — 403.</summary>
    Forbidden,

    /// <summary>Credentials missing or wrong — 401.</summary>
    Unauthorized,
}

/// <summary>
/// A business failure. <paramref name="Code"/> is stable and machine-readable so the client
/// can branch on it; <paramref name="Message"/> is what a user actually reads and may change
/// freely without breaking anyone.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Placeholder carried by a successful <see cref="Result"/>.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Validation);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}
