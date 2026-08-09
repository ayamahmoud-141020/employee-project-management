using EPM.Application.Abstractions;
using EPM.Application.Features.Employees.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Employees;
using EPM.Domain.Projects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Employees.GetEmployeeById;

/// <summary>The employee detail view: the record plus the projects they are on.</summary>
public sealed record EmployeeDetailResponse(
    EmployeeResponse Employee,
    IReadOnlyList<EmployeeAssignmentResponse> Assignments);

public sealed record EmployeeAssignmentResponse(
    int ProjectId,
    string ProjectName,
    ProjectStatus ProjectStatus,
    string Role,
    DateOnly AssignedDate,
    int AllocationPercentage);

public sealed record GetEmployeeByIdQuery(int Id) : IRequest<Result<EmployeeDetailResponse>>;

internal sealed class GetEmployeeByIdHandler(IAppDbContext context)
    : IRequestHandler<GetEmployeeByIdQuery, Result<EmployeeDetailResponse>>
{
    public async Task<Result<EmployeeDetailResponse>> Handle(
        GetEmployeeByIdQuery query,
        CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .AsNoTracking()
            .Where(entity => entity.Id == query.Id)
            .Select(EmployeeProjections.ToResponse)
            .FirstOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return Result.Failure<EmployeeDetailResponse>(EmployeeErrors.NotFound(query.Id));
        }

        // Second query rather than a join: one row plus N assignment rows in a single result
        // set would repeat every employee column N times, and EF would have to de-duplicate.
        var assignments = await context.ProjectAssignments
            .AsNoTracking()
            .Where(assignment => assignment.EmployeeId == query.Id)
            .OrderBy(assignment => assignment.AssignedDate)
            .Select(assignment => new EmployeeAssignmentResponse(
                assignment.ProjectId,
                assignment.Project!.Name,
                assignment.Project.Status,
                assignment.Role,
                assignment.AssignedDate,
                assignment.Allocation.Percentage))
            .ToListAsync(cancellationToken);

        return new EmployeeDetailResponse(employee, assignments);
    }
}
