using EPM.Application.Abstractions;
using EPM.Application.Features.Projects.Contracts;
using EPM.Domain.Abstractions;
using EPM.Domain.Projects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Features.Projects.CreateProject;

public sealed record CreateProjectCommand(
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly? EndDate,
    ProjectStatus Status) : IRequest<Result<ProjectResponse>>;

internal sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(Project.MaxNameLength);

        RuleFor(command => command.Description)
            .MaximumLength(Project.MaxDescriptionLength);

        RuleFor(command => command.Status)
            .IsInEnum().WithMessage("Status must be one of Planning, Active, Completed or Cancelled.");

        // The message names both fields because the error is about the pair, and the form
        // shows it against the end-date control where the user can act on it.
        RuleFor(command => command.EndDate)
            .GreaterThanOrEqualTo(command => command.StartDate)
            .When(command => command.EndDate.HasValue)
            .WithMessage("End date must be on or after the start date.");
    }
}

internal sealed class CreateProjectHandler(IAppDbContext context)
    : IRequestHandler<CreateProjectCommand, Result<ProjectResponse>>
{
    public async Task<Result<ProjectResponse>> Handle(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();

        var nameTaken = await context.Projects.AnyAsync(project => project.Name == name, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure<ProjectResponse>(ProjectErrors.NameAlreadyExists);
        }

        var project = Project.Create(name, command.Description, command.StartDate, command.EndDate, command.Status);

        if (project.IsFailure)
        {
            return Result.Failure<ProjectResponse>(project.Error);
        }

        context.Projects.Add(project.Value);
        await context.SaveChangesAsync(cancellationToken);

        return new ProjectResponse(
            project.Value.Id,
            project.Value.Name,
            project.Value.Description,
            project.Value.Schedule.Start,
            project.Value.Schedule.End,
            project.Value.Status,
            AssignedEmployeeCount: 0);
    }
}
