using EPM.Domain.Abstractions;

namespace EPM.Domain.Projects;

/// <summary>
/// A body of work with a schedule, a status and a team.
/// </summary>
/// <remarks>
/// Project is the aggregate root for assignments, which is the main modelling decision in
/// this codebase. Every assignment rule in the spec — no duplicate employee, allocation
/// 1-100, dates inside the project schedule — needs to compare a candidate against the other
/// assignments and against the project's own dates. Putting the collection behind this root
/// means all three are checked in one place, on data the root already has loaded, and no
/// caller can bypass them by inserting into a join table directly.
///
/// The one rule the root cannot check itself is "the employee must be active": that lives in
/// a different aggregate. Rather than reach across, the root asks for the answer as a
/// parameter and the calling handler is responsible for supplying it.
/// </remarks>
public sealed class Project : AggregateRoot
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;

    private readonly List<ProjectAssignment> _assignments = [];

    private Project()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public DateRange Schedule { get; private set; } = null!;

    public ProjectStatus Status { get; private set; }

    /// <summary>
    /// Read-only to the outside world. Mutating the team goes through
    /// <see cref="AssignEmployee"/> / <see cref="RemoveEmployee"/> so the invariants hold.
    /// </summary>
    public IReadOnlyCollection<ProjectAssignment> Assignments => _assignments.AsReadOnly();

    public static Result<Project> Create(
        string? name,
        string? description,
        DateOnly startDate,
        DateOnly? endDate,
        ProjectStatus status)
    {
        var details = ValidateDetails(name, description);

        if (details.IsFailure)
        {
            return Result.Failure<Project>(details.Error);
        }

        if (!Enum.IsDefined(status))
        {
            return Result.Failure<Project>(ProjectErrors.StatusInvalid);
        }

        var schedule = DateRange.Create(startDate, endDate);

        if (schedule.IsFailure)
        {
            return Result.Failure<Project>(schedule.Error);
        }

        return new Project
        {
            Name = details.Value.Name,
            Description = details.Value.Description,
            Schedule = schedule.Value,
            Status = status,
        };
    }

    public Result Update(
        string? name,
        string? description,
        DateOnly startDate,
        DateOnly? endDate,
        ProjectStatus status)
    {
        var details = ValidateDetails(name, description);

        if (details.IsFailure)
        {
            return Result.Failure(details.Error);
        }

        if (!Enum.IsDefined(status))
        {
            return Result.Failure(ProjectErrors.StatusInvalid);
        }

        var schedule = DateRange.Create(startDate, endDate);

        if (schedule.IsFailure)
        {
            return Result.Failure(schedule.Error);
        }

        // Shrinking the schedule under existing assignments would leave rows that violate the
        // very rule AssignEmployee enforces. Refusing here keeps the aggregate consistent,
        // and the count in the message tells the user how much is in the way. This only
        // holds when the caller loaded the assignments — see the update handler's Include.
        var stranded = _assignments.Count(assignment => !schedule.Value.Contains(assignment.AssignedDate));

        if (stranded > 0)
        {
            return Result.Failure(ProjectErrors.ScheduleConflictsWithAssignments(stranded));
        }

        Name = details.Value.Name;
        Description = details.Value.Description;
        Schedule = schedule.Value;
        Status = status;

        return Result.Success();
    }

    /// <summary>
    /// Adds an employee to the team.
    /// </summary>
    /// <param name="employeeIsActive">
    /// Read from the Employee aggregate by the caller. Passing it in rather than holding an
    /// Employee reference keeps the two aggregates independent — this one never loads or
    /// mutates the other, it just needs the answer to one question.
    /// </param>
    public Result AssignEmployee(
        int employeeId,
        bool employeeIsActive,
        string? role,
        DateOnly assignedDate,
        int allocationPercentage)
    {
        // Ordered so the most specific refusal wins. "You are already on this project" is
        // more useful than "allocation must be 1-100" when both happen to be true.
        if (!employeeIsActive)
        {
            return Result.Failure(ProjectAssignmentErrors.EmployeeInactive);
        }

        if (_assignments.Any(assignment => assignment.EmployeeId == employeeId))
        {
            return Result.Failure(ProjectAssignmentErrors.DuplicateAssignment);
        }

        if (!Schedule.Contains(assignedDate))
        {
            return Result.Failure(ProjectAssignmentErrors.AssignedDateOutsideProjectSchedule(Schedule));
        }

        var assignment = ProjectAssignment.Create(employeeId, role, assignedDate, allocationPercentage);

        if (assignment.IsFailure)
        {
            return Result.Failure(assignment.Error);
        }

        _assignments.Add(assignment.Value);

        return Result.Success();
    }

    public Result UpdateAssignment(int employeeId, string? role, int allocationPercentage)
    {
        var assignment = _assignments.SingleOrDefault(a => a.EmployeeId == employeeId);

        return assignment is null
            ? Result.Failure(ProjectAssignmentErrors.NotAssigned(employeeId))
            : assignment.ChangeRoleAndAllocation(role, allocationPercentage);
    }

    public Result RemoveEmployee(int employeeId)
    {
        var assignment = _assignments.SingleOrDefault(a => a.EmployeeId == employeeId);

        if (assignment is null)
        {
            return Result.Failure(ProjectAssignmentErrors.NotAssigned(employeeId));
        }

        _assignments.Remove(assignment);

        return Result.Success();
    }

    /// <summary>
    /// Drops an employee if they happen to be on the team, and reports whether anything
    /// changed. Used by the deactivation flow, where "they were not on this project" is a
    /// perfectly normal outcome rather than an error worth surfacing.
    /// </summary>
    public bool RemoveEmployeeIfAssigned(int employeeId)
    {
        var assignment = _assignments.SingleOrDefault(a => a.EmployeeId == employeeId);

        if (assignment is null)
        {
            return false;
        }

        _assignments.Remove(assignment);

        return true;
    }

    private static Result<(string Name, string? Description)> ValidateDetails(string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<(string, string?)>(ProjectErrors.NameRequired);
        }

        var trimmedName = name.Trim();

        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<(string, string?)>(ProjectErrors.NameTooLong);
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (trimmedDescription?.Length > MaxDescriptionLength)
        {
            return Result.Failure<(string, string?)>(ProjectErrors.DescriptionTooLong);
        }

        return (trimmedName, trimmedDescription);
    }
}
