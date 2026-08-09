namespace EPM.Domain.Abstractions;

/// <summary>
/// Something that has already happened in the domain, named in the past tense.
/// </summary>
/// <remarks>
/// Intentionally not MediatR's INotification. Keeping this interface dependency-free is what
/// lets the domain project reference nothing at all; the infrastructure dispatcher wraps each
/// event in a MediatR notification on its way out. See DomainEventNotification.
/// </remarks>
public interface IDomainEvent
{
}
