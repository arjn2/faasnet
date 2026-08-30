using BMS.Core.Events;

namespace BMS.Core.Entities;

/// <summary>
/// Marker interface for aggregate roots in domain
/// </summary>
public interface IAggregateRoot
{
    int Id { get; }
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}