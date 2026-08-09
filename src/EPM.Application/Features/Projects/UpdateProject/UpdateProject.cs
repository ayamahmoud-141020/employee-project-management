using EPM.Application.Abstractions;
using EPM.Application.Features.Projects.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Projects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Projects.UpdateProject;

public sealed record UpdateProjectCommand(
    int Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly? EndDate,
    ProjectStatus Status) : IRequest<Result<ProjectResponse>>;

internal sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator()
    {
        RuleFor(command => command.Id).GreaterThan(0);

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(Project.MaxNameLength);

        RuleFor(command => command.Description)
            .MaximumLength(Project.MaxDescriptionLength);

        RuleFor(command => command.Status)
            .IsInEnum().WithMessage("Status must be one of Planning, Active, Completed or Cancelled.");

        RuleFor(command => command.EndDate)
            .GreaterThanOrEqualTo(command => command.StartDate)
            .When(command => command.EndDate.HasValue)
            .WithMessage("End date must be on or after the start date.");
    }
}

internal sealed class UpdateProjectHandler(IAppDbContext context)
    : IRequestHandler<UpdateProjectCommand, Result<ProjectResponse>>
{
    public async Task<Result<ProjectResponse>> Handle(
        UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        // Assignments are loaded because Project.Update refuses a schedule change that would
        // strand one outside the new dates. Without the Include the aggregate would see an
        // empty collection and happily allow it.
        var project = await context.Projects
            .Include(entity => entity.Assignments)
            .FirstOrDefaultAsync(entity => entity.Id == command.Id, cancellationToken);

        if (project is null)
        {
            return Result.Failure<ProjectResponse>(ProjectErrors.NotFound(command.Id));
        }

        var name = command.Name.Trim();

        var nameTaken = await context.Projects
            .AnyAsync(other => other.Id != command.Id && other.Name == name, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<ProjectResponse>(ProjectErrors.NameAlreadyExists);
        }

        var update = project.Update(name, command.Description, command.StartDate, command.EndDate, command.Status);

        if (update.IsFailure)
        {
            return Result.Failure<ProjectResponse>(update.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new ProjectResponse(
            project.Id,
            project.Name,
            project.Description,
            project.Schedule.Start,
            project.Schedule.End,
            project.Status,
            project.Assignments.Count);
    }
}
