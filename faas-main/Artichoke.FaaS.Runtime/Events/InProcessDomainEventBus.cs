using System.Collections.Concurrent;
using Artichoke.FaaS.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Artichoke.FaaS.Runtime.Events;

/// <summary>
/// In-process pub/sub for domain events. Simple, fast, no external dependencies.
///
/// - Publishers call PublishAsync&lt;TEvent&gt;(event) — returns when all handlers have run.
/// - Subscribers call Subscribe&lt;TEvent&gt;(handler) — returns an IDisposable to unsubscribe.
/// - Handlers run concurrently via Task.WhenAll (fire-and-forget within the publish call).
///
/// For cross-process pub/sub, implement IDomainEventBus yourself backed by RabbitMQ/Kafka/Redis.
/// </summary>
public sealed class InProcessDomainEventBus : IDomainEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Func<IDomainEvent, CancellationToken, Task>>> _handlers = new();
    private readonly ILogger<InProcessDomainEventBus>? _logger;

    public InProcessDomainEventBus(ILogger<InProcessDomainEventBus>? logger = null)
    {
        _logger = logger;
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        var eventType = typeof(TEvent);

        if (!_handlers.TryGetValue(eventType, out var bag) || bag.IsEmpty)
        {
            _logger?.LogDebug("No subscribers for {EventType}", eventType.Name);
            return Task.CompletedTask;
        }

        var snapshot = bag.ToArray();
        _logger?.LogDebug("Publishing {EventType} to {Count} subscriber(s)", eventType.Name, snapshot.Length);

        return Task.WhenAll(snapshot.Select(async handler =>
        {
            try
            {
                await handler(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Handler for {EventType} threw.", eventType.Name);
            }
        }));
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        var eventType = typeof(TEvent);

        // Wrap the typed handler in an untyped wrapper so we can store them all in one bag.
        Func<IDomainEvent, CancellationToken, Task> wrapped = (e, ct) => handler((TEvent)e, ct);

        var bag = _handlers.GetOrAdd(eventType, _ => new ConcurrentBag<Func<IDomainEvent, CancellationToken, Task>>());
        bag.Add(wrapped);

        _logger?.LogInformation("Subscribed handler to {EventType}", eventType.Name);

        // Note: ConcurrentBag doesn't support removal, so "unsubscribe" is a no-op here.
        // For a real unsubscribe, swap to ImmutableList<Func<...>>. For our use case
        // (triggers subscribe at startup and live for the app lifetime), this is fine.
        return new NoOpDisposable();
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
