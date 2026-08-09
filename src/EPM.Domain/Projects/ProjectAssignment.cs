using EPM.Domain.Abstractions;
using EPM.Domain.Employees;

namespace EPM.Domain.Projects;

/// <summary>
/// One employee's involvement in one project — the join between the two, carrying its own
/// data (role, allocation, when it started) rather than being a bare link table.
/// </summary>
/// <remarks>
/// Part of the Project aggregate, not a root of its own. That is why the factory and the
/// mutators are internal: the only legitimate way to create or change an assignment is
/// through <see cref="Project"/>, which is the object that can see the sibling assignments
/// and the schedule needed to validate it.
/// </remarks>
public sealed class ProjectAssignment : Entity
{
    public const int MaxRoleLength = 100;

    private ProjectAssignment()
    {
    }

    public int ProjectId { get; private set; }

    public Project? Project { get; private set; }

    public int EmployeeId { get; private set; }

    /// <summary>Navigation for read-side Includes; assignments never mutate the employee.</summary>
    public Employee? Employee { get; private set; }

    public string Role { get; private set; } = null!;

    public DateOnly AssignedDate { get; private set; }

    public Allocation Allocation { get; private set; } = null!;

    internal static Result<ProjectAssignment> Create(
        int employeeId,
        string? role,
        DateOnly assignedDate,
        int allocationPercentage)
    {
        var roleResult = ValidateRole(role);

        if (roleResult.IsFailure)
        {
            return Result.Failure<ProjectAssignment>(roleResult.Error);
        }

        var allocationResult = Allocation.Create(allocationPercentage);

        if (allocationResult.IsFailure)
        {
            return Result.Failure<ProjectAssignment>(allocationResult.Error);
        }

        return new ProjectAssignment
        {
            EmployeeId = employeeId,
            Role = roleResult.Value,
            AssignedDate = assignedDate,
            Allocation = allocationResult.Value,
        };
    }

    internal Result ChangeRoleAndAllocation(string? role, int allocationPercentage)
    {
        var roleResult = ValidateRole(role);

        if (roleResult.IsFailure)
        {
            return Result.Failure(roleResult.Error);
        }

        var allocationResult = Allocation.Create(allocationPercentage);

        if (allocationResult.IsFailure)
        {
            return Result.Failure(allocationResult.Error);
        }

        Role = roleResult.Value;
        Allocation = allocationResult.Value;

        return Result.Success();
    }

    private static Result<string> ValidateRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Result.Failure<string>(ProjectAssignmentErrors.RoleRequired);
        }

        var trimmed = role.Trim();

        return trimmed.Length > MaxRoleLength
            ? Result.Failure<string>(ProjectAssignmentErrors.RoleTooLong)
            : trimmed;
    }
}
