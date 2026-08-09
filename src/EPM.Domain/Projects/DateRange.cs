using EPM.Domain.Abstractions;

namespace EPM.Domain.Projects;

/// <summary>
/// A project's schedule: a required start and an optional end.
/// </summary>
/// <remarks>
/// End is nullable on purpose — an open-ended project ("runs until we stop it") is a real
/// thing, and forcing a placeholder date would corrupt every report that filters on it.
/// Pairing the two dates in one value object is what makes start &lt;= end impossible to
/// violate: you cannot change one without the other being re-checked.
/// </remarks>
public sealed class DateRange : ValueObject
{
    private DateRange(DateOnly start, DateOnly? end)
    {
        Start = start;
        End = end;
    }

    public DateOnly Start { get; }

    public DateOnly? End { get; }

    public static Result<DateRange> Create(DateOnly start, DateOnly? end)
    {
        if (end.HasValue && end.Value < start)
        {
            return Result.Failure<DateRange>(ProjectErrors.EndDateBeforeStartDate);
        }

        return new DateRange(start, end);
    }

    /// <summary>
    /// True when <paramref name="date"/> falls on or between the endpoints. An open-ended
    /// range contains everything from Start onwards.
    /// </summary>
    public bool Contains(DateOnly date) => date >= Start && (End is null || date <= End.Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }

    public override string ToString() => End.HasValue ? $"{Start:O} - {End:O}" : $"{Start:O} - open";
}
