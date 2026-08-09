using EPM.Domain.Abstractions;
using MediatR;

namespace EPM.Application.Abstractions;

/// <summary>
/// Carries a domain event onto the MediatR bus.
/// </summary>
/// <remarks>
/// The adapter exists so <see cref="IDomainEvent"/> can stay free of MediatR and the domain
/// project can keep zero package references. Handlers subscribe as
/// INotificationHandler&lt;DomainEventNotification&lt;EmployeeDeactivated&gt;&gt;.
/// </remarks>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
