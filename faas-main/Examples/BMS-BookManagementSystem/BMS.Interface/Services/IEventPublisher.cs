using BMS.Core.Events;

namespace BMS.Interface.Services;

/// <summary>
/// Interface layer - Event publisher contract
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent);
}