using EPM.Application.Abstractions;
using EPM.Application.Features.Departments.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Departments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Departments.GetDepartmentById;

public sealed record GetDepartmentByIdQuery(int Id) : IRequest<Result<DepartmentResponse>>;

internal sealed class GetDepartmentByIdHandler(IAppDbContext context)
    : IRequestHandler<GetDepartmentByIdQuery, Result<DepartmentResponse>>
{
    public async Task<Result<DepartmentResponse>> Handle(
        GetDepartmentByIdQuery query,
        CancellationToken cancellationToken)
    {
        var department = await context.Departments
            .AsNoTracking()
            .Where(entity => entity.Id == query.Id)
            .Select(DepartmentProjections.ToResponse(context.Employees))
            .FirstOrDefaultAsync(cancellationToken);

        return department is null
            ? Result.Failure<DepartmentResponse>(DepartmentErrors.NotFound(query.Id))
            : Result.Success(department);
    }
}
