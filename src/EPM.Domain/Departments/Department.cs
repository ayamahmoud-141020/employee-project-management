using EPM.Domain.Abstractions;

namespace EPM.Domain.Departments;

/// <summary>
/// An organisational unit employees belong to.
/// </summary>
/// <remarks>
/// Two department rules from the spec are deliberately absent here:
/// name uniqueness and "cannot delete while active employees exist". Both need to look
/// outside this instance — at every other department, or at the employees table — so they
/// live in the slice handlers backed by a unique index and a count query. An aggregate can
/// only guard what it can see.
/// </remarks>
public sealed class Department : AggregateRoot
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    private Department()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public static Result<Department> Create(string? name, string? description)
    {
        var validated = Validate(name, description);

        if (validated.IsFailure)
        {
            return Result.Failure<Department>(validated.Error);
        }

        return new Department
        {
            Name = validated.Value.Name,
            Description = validated.Value.Description,
        };
    }

    public Result Update(string? name, string? description)
    {
        var validated = Validate(name, description);

        if (validated.IsFailure)
        {
            return Result.Failure(validated.Error);
        }

        Name = validated.Value.Name;
        Description = validated.Value.Description;

        return Result.Success();
    }

    private static Result<(string Name, string? Description)> Validate(string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<(string, string?)>(DepartmentErrors.NameRequired);
        }

        var trimmedName = name.Trim();

        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<(string, string?)>(DepartmentErrors.NameTooLong);
        }

        // Blank and null both mean "no description"; collapsing them keeps the column free of
        // empty strings that would otherwise sort and filter differently from NULL.
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (trimmedDescription?.Length > MaxDescriptionLength)
        {
            return Result.Failure<(string, string?)>(DepartmentErrors.DescriptionTooLong);
        }

        return (trimmedName, trimmedDescription);
    }
}
