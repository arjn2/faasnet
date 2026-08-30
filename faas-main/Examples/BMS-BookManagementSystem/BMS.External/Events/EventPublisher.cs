using Artichoke.FaaS.Core.Interfaces;
using BMS.Core.Events;
using BMS.Interface.Services;
using Microsoft.Extensions.Logging;

namespace BMS.External.Events;

// ============================================================================
// v8.0.6 EventPublisher — publishes to IDomainEventBus, period.
//
// In v8.0.4 the publisher instantiated AuditFunction/SearchIndexFunction/NotificationFunction
// directly with `new` and called ExecuteAsync on each. That worked, but it hardcoded the
// "which functions care about which events" mapping inside the publisher.
//
// In v8.0.6 the publisher is dumb: it just publishes the event to IDomainEventBus. The
// DomainEventTrigger<TEvent> instances (registered in Program.cs via AddDomainEventTrigger)
// subscribe to the bus and dispatch to their target functions.
//
// This means:
//   - Adding a new function for BookCreatedEvent = one line in Program.cs (no EventPublisher change).
//   - The publisher has zero knowledge of which functions exist.
//   - Tests can swap the IDomainEventBus for a fake and verify publishing without running functions.
// ============================================================================

public class EventPublisher : IEventPublisher
{
    private readonly IDomainEventBus _eventBus;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(IDomainEventBus eventBus, ILogger<EventPublisher> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync(BMS.Core.Events.IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var eventType = domainEvent.GetType().Name;
        _logger.LogInformation("Publishing domain event: {EventType}", eventType);

        // The event already implements Artichoke.FaaS.Core.Interfaces.IDomainEvent (via
        // BMS.Core.Events.IDomainEvent : Artichoke.FaaS.Core.Interfaces.IDomainEvent), so
        // we can pass it directly to the bus. The bus dispatches to all subscribers
        // (DomainEventTrigger<TEvent> instances) which invoke their target functions.
        await _eventBus.PublishAsync(domainEvent);
    }
}
