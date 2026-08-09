using System.Text.RegularExpressions;
using EPM.Domain.Abstractions;

namespace EPM.Domain.Employees;

/// <summary>
/// An optional contact number, stored as typed but with the separators stripped from the
/// digit count so "+1 (555) 010-0100" and "+15550100100" are judged the same length.
/// </summary>
public sealed partial class PhoneNumber : ValueObject
{
    public const int MaxLength = 32;

    private const int MinDigits = 7;
    private const int MaxDigits = 15; // E.164 caps national + country digits at 15.

    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Builds a phone number, or returns null for blank input — phone is optional on an
    /// employee, so "not provided" is a legitimate outcome rather than a validation failure.
    /// </summary>
    public static Result<PhoneNumber?> CreateOptional(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Success<PhoneNumber?>(null);
        }

        var trimmed = input.Trim();

        if (trimmed.Length > MaxLength || !AllowedCharacters().IsMatch(trimmed))
        {
            return Result.Failure<PhoneNumber?>(EmployeeErrors.PhoneInvalid);
        }

        var digitCount = trimmed.Count(char.IsDigit);

        if (digitCount is < MinDigits or > MaxDigits)
        {
            return Result.Failure<PhoneNumber?>(EmployeeErrors.PhoneInvalid);
        }

        return Result.Success<PhoneNumber?>(new PhoneNumber(trimmed));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    // A character whitelist, deliberately not a format. The digit count above is what actually
    // validates; this only keeps letters and punctuation out of the column. Numbering plans
    // differ too much between countries for a stricter pattern to do anything but reject
    // legitimate numbers.
    [GeneratedRegex(@"^\+?[\d\s\-.()]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCharacters();
}
