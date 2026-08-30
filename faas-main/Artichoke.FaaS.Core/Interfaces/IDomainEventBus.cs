namespace Artichoke.FaaS.Core.Interfaces;

// ============================================================================
// v8.0.6 — In-process domain event bus.
//
// A minimal pub/sub for domain events. The EventPublisher publishes; triggers
// (and any other subscribers) receive. The implementation lives in
// Artichoke.FaaS.Runtime.InProcessDomainEventBus.
//
// Why not use MediatR / Orleans / MessagePack? Because the goal of this framework
// is to be a self-contained library with zero external dependencies beyond .NET 9.
// If you need cross-process pub/sub, plug in your own IDomainEventBus implementation
// backed by RabbitMQ / Kafka / Redis.
// ============================================================================

/// <summary>
/// Marker for domain events. Implement on your event records (e.g. BookCreatedEvent).
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

/// <summary>
/// In-process pub/sub for domain events. Publishers call PublishAsync; subscribers
/// register via Subscribe&lt;TEvent&gt; and get a callback when an event of that type
/// is published.
/// </summary>
public interface IDomainEventBus
{
    /// <summary>Publish an event to all subscribers of type TEvent.</summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;

    /// <summary>Subscribe to events of type TEvent. Returns an unsubscribe disposable.</summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IDomainEvent;
}
