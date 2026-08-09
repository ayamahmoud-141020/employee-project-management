using System.Text.RegularExpressions;
using EPM.Domain.Abstractions;

namespace EPM.Domain.Employees;

/// <summary>
/// A validated, normalised email address.
/// </summary>
public sealed partial class Email : ValueObject
{
    public const int MaxLength = 256;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<Email>(EmployeeErrors.EmailRequired);
        }

        // Normalising to lower case at the boundary means the unique index enforces
        // "one account per address" no matter what collation the database happens to use,
        // and comparisons never need a case-insensitive scan.
        var normalised = input.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
        {
            return Result.Failure<Email>(EmployeeErrors.EmailTooLong);
        }

        if (!Pattern().IsMatch(normalised))
        {
            return Result.Failure<Email>(EmployeeErrors.EmailInvalid);
        }

        return new Email(normalised);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    // Deliberately not RFC 5322. That grammar accepts addresses no mail server will route and
    // is a known catastrophic-backtracking risk. This covers the shape users actually type;
    // anything subtler is a job for a confirmation email, not a regex.
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
