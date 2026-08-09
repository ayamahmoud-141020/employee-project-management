using EPM.Application.Abstractions;
using EPM.Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EPM.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Publishes the domain events raised during a SaveChanges, once that save has succeeded.
/// </summary>
/// <remarks>
/// The timing is the point. Events are collected before the write (while the change tracker
/// still knows which aggregates were touched) but published after it, so a handler reacting
/// to "employee deactivated" cannot run for a save that then rolled back.
///
/// Handlers run in their own SaveChanges. That is a deliberate trade: it is not one atomic
/// transaction, but it keeps the event pipeline simple and the follow-up work here
/// (unassigning projects) is idempotent, so a retry costs nothing. A system needing stricter
/// guarantees would write the events to an outbox table inside the same transaction instead.
/// </remarks>
public sealed class DispatchDomainEventsInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    private readonly List<IDomainEvent> _pendingEvents = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CollectEvents(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        // Copied out and cleared first: a handler that saves again re-enters this interceptor,
        // and iterating the live list while that happens would either loop or throw.
        var events = _pendingEvents.ToArray();
        _pendingEvents.Clear();

        foreach (var domainEvent in events)
        {
            await PublishAsync(domainEvent, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void CollectEvents(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            _pendingEvents.AddRange(aggregate.DomainEvents);
            aggregate.ClearDomainEvents();
        }
    }

    // MediatR dispatches on the closed generic type, so the wrapper has to be built with the
    // event's runtime type — DomainEventNotification<IDomainEvent> would match no handler.
    private Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        var notification = (INotification)Activator.CreateInstance(notificationType, domainEvent)!;

        return publisher.Publish(notification, cancellationToken);
    }
}
