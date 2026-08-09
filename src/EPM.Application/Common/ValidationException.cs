namespace EPM.Application.Common;

/// <summary>
/// Thrown when a request fails input validation, carrying every broken rule keyed by field.
/// </summary>
/// <remarks>
/// This is the one deliberate exception to "business failures return a Result". Validation
/// runs in a pipeline behaviour, before the handler, where there is no Result to return —
/// making it work would mean constraining every TResponse in the system to be a Result, which
/// is a lot of generic machinery to avoid one throw at a well-defined boundary.
///
/// The field-keyed shape matters to the frontend: Angular attaches each message to the
/// matching form control, so "Email" has to arrive separately from "HireDate" rather than
/// concatenated into one sentence.
/// </remarks>
public sealed class ValidationException : Exception
{
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.") =>
        Errors = new Dictionary<string, string[]>(errors, StringComparer.OrdinalIgnoreCase);

    /// <summary>Field name (camelCased to match the JSON payload) to the messages against it.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
