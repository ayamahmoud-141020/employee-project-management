using EPM.Application.Abstractions;
using EPM.Domain.Employees.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EPM.Application.Features.Employees.DeactivateEmployee;

/// <summary>
/// Removes a deactivated employee from every project they were still assigned to.
/// </summary>
/// <remarks>
/// This is the other half of the rule "inactive employees cannot be assigned to projects".
/// Blocking new assignments is not enough on its own — without this, deactivating someone
/// leaves their existing allocations in place, so a project keeps showing a person who no
/// longer works here and their percentage stays booked against capacity nobody can reclaim.
///
/// Runs as a domain event handler rather than inline in the deactivate handler because it
/// touches a different aggregate. Idempotent: re-running it finds no assignments and does
/// nothing, which matters because event dispatch happens outside the original transaction.
/// </remarks>
internal sealed class UnassignEmployeeFromProjectsHandler(
    IAppDbContext context,
    ILogger<UnassignEmployeeFromProjectsHandler> logger)
    : INotificationHandler<DomainEventNotification<EmployeeDeactivated>>
{
    public async Task Handle(
        DomainEventNotification<EmployeeDeactivated> notification,
        CancellationToken cancellationToken)
    {
        var employeeId = notification.DomainEvent.EmployeeId;

        // Loads whole Project aggregates, not the assignment rows, so the removal goes through
        // Project.RemoveEmployeeIfAssigned and the aggregate stays the only thing that edits
        // its own collection.
        var projects = await context.Projects
            .Include(project => project.Assignments)
            .Where(project => project.Assignments.Any(assignment => assignment.EmployeeId == employeeId))
            .ToListAsync(cancellationToken);

        if (projects.Count == 0)
        {
            return;
        }

        foreach (var project in projects)
        {
            project.RemoveEmployeeIfAssigned(employeeId);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Removed deactivated employee {EmployeeId} from {ProjectCount} project(s)",
            employeeId,
            projects.Count);
    }
}
