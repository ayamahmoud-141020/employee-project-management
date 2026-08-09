using EPM.Domain.Abstractions;

namespace EPM.Domain.Projects;

/// <summary>
/// The share of an employee's time committed to one project, as a whole percentage.
/// </summary>
/// <remarks>
/// A bare int would let 0 or 250 through anywhere in the codebase. Wrapping it means the
/// 1-100 rule is checked exactly once, at construction, and every later use is safe by type.
/// </remarks>
public sealed class Allocation : ValueObject
{
    public const int Minimum = 1;
    public const int Maximum = 100;

    private Allocation(int percentage) => Percentage = percentage;

    public int Percentage { get; }

    public static Result<Allocation> Create(int percentage) =>
        percentage is < Minimum or > Maximum
            ? Result.Failure<Allocation>(ProjectAssignmentErrors.AllocationOutOfRange)
            : new Allocation(percentage);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Percentage;
    }

    public override string ToString() => $"{Percentage}%";
}
