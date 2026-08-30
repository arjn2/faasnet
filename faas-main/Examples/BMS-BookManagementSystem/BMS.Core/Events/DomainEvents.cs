using BMS.Core.Entities;

namespace BMS.Core.Events;

// ============================================================================
// BMS domain events.
//
// BMS.Core.Events.IDomainEvent now extends Artichoke.FaaS.Core.Interfaces.IDomainEvent
// so that BMS events can flow through the framework's IDomainEventBus and trigger
// ICustomFunction implementations via DomainEventTrigger<TEvent>.
//
// This is the only coupling between BMS.Core (the domain) and Artichoke.FaaS.Core
// (the framework) — and it's a marker interface, so the domain still has zero
// runtime dependency on the framework's behavior.
// ============================================================================

/// <summary>
/// BMS marker for domain events. Extends the framework's IDomainEvent so events
/// can be published to IDomainEventBus.
/// </summary>
public interface IDomainEvent : Artichoke.FaaS.Core.Interfaces.IDomainEvent
{
    // OccurredOn is inherited from Artichoke.FaaS.Core.Interfaces.IDomainEvent.
}

public record BookCreatedEvent(Book Book) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record BookUpdatedEvent(Book Book) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record BookDeletedEvent(int BookId, string Title) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
