using EPM.Domain.Employees;
using EPM.Domain.Projects;

namespace EPM.Domain.UnitTests;

/// <summary>
/// Fixed dates and ready-made aggregates so each test only spells out the one field it is
/// actually about. Everything is deterministic — no DateTime.Today anywhere in the suite,
/// which is the whole reason the domain takes "today" as a parameter.
/// </summary>
internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 6, 15);
    public static readonly DateOnly Yesterday = Today.AddDays(-1);
    public static readonly DateOnly Tomorrow = Today.AddDays(1);

    public static readonly DateTime UtcNow = new(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);

    public const int DepartmentId = 7;

    public static Employee AnActiveEmployee(string email = "ada.lovelace@epm.local") =>
        Employee.Create(
            firstName: "Ada",
            lastName: "Lovelace",
            email: email,
            phone: "+1 555 010 0100",
            jobTitle: "Principal Engineer",
            departmentId: DepartmentId,
            hireDate: Today.AddYears(-3),
            today: Today).Value;

    public static Employee AnInactiveEmployee()
    {
        var employee = AnActiveEmployee("grace.hopper@epm.local");
        employee.Deactivate(UtcNow);

        return employee;
    }

    public static readonly DateOnly DefaultProjectStart = new(2026, 1, 1);
    public static readonly DateOnly DefaultProjectEnd = new(2026, 12, 31);

    /// <summary>A project running for the whole of 2026, so most dates fall inside it.</summary>
    // `end` is not a nullable-with-?? default: null has to stay expressible, because
    // "open-ended project" is a case worth testing and `?? DefaultProjectEnd` would swallow it.
    public static Project AProject(DateOnly? start = null, DateOnly? end = null) =>
        AProject(start ?? DefaultProjectStart, end ?? DefaultProjectEnd, openEnded: false);

    public static Project AnOpenEndedProject(DateOnly? start = null) =>
        AProject(start ?? DefaultProjectStart, end: null, openEnded: true);

    private static Project AProject(DateOnly start, DateOnly? end, bool openEnded) =>
        Project.Create(
            name: "Apollo",
            description: "Migrate the billing platform.",
            startDate: start,
            endDate: openEnded ? null : end,
            status: ProjectStatus.Active).Value;
}
