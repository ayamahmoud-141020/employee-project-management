using EPM.Domain.Abstractions;

namespace EPM.Domain.Projects;

public static class ProjectErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Project.NameRequired", "Project name is required.");

    public static readonly Error NameTooLong =
        Error.Validation("Project.NameTooLong", $"Project name cannot exceed {Project.MaxNameLength} characters.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("Project.DescriptionTooLong", $"Description cannot exceed {Project.MaxDescriptionLength} characters.");

    public static readonly Error EndDateBeforeStartDate =
        Error.Validation("Project.EndDateBeforeStartDate", "End date must be on or after the start date.");

    public static readonly Error StatusInvalid =
        Error.Validation("Project.StatusInvalid", "Status must be one of Planning, Active, Completed or Cancelled.");

    public static readonly Error NameAlreadyExists =
        Error.Conflict("Project.NameExists", "A project with this name already exists.");

    public static Error ScheduleConflictsWithAssignments(int conflictingAssignments) =>
        Error.Conflict(
            "Project.ScheduleConflictsWithAssignments",
            $"The new schedule would leave {conflictingAssignments} existing assignment(s) outside the project dates.");

    public static Error NotFound(int projectId) =>
        Error.NotFound("Project.NotFound", $"Project with id {projectId} was not found.");
}
