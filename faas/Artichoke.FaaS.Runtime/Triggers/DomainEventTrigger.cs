using Artichoke.FaaS.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Artichoke.FaaS.Runtime.Triggers;

/// <summary>
/// Fires a target function when a domain event of type <typeparamref name="TEvent"/> is published.
///
/// Subscribes to <see cref="IDomainEventBus"/> on start; when an event arrives, calls
/// <see cref="IFunctionHost.ExecuteAsync"/> with the result of <see cref="InputSelector"/>.
/// </summary>
public sealed class DomainEventTrigger<TEvent> : IDomainEventTrigger<TEvent>
    where TEvent : class, IDomainEvent
{
    private readonly IDomainEventBus _eventBus;
    private readonly ILogger<DomainEventTrigger<TEvent>>? _logger;
    private IDisposable? _subscription;

    public DomainEventTrigger(
        string targetFunctionType,
        Func<TEvent, object> inputSelector,
        IDomainEventBus eventBus,
        ILogger<DomainEventTrigger<TEvent>>? logger = null)
    {
        TargetFunctionType = targetFunctionType ?? throw new ArgumentNullException(nameof(targetFunctionType));
        InputSelector = inputSelector ?? throw new ArgumentNullException(nameof(inputSelector));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger;
    }

    public string TriggerType => $"DomainEventTrigger:{typeof(TEvent).Name}→{TargetFunctionType}";
    public string TargetFunctionType { get; }
    public string DisplayName => $"Domain Event Trigger ({typeof(TEvent).Name} → {TargetFunctionType})";
    public string Description => $"Fires '{TargetFunctionType}' when {typeof(TEvent).Name} is published.";
    public Func<TEvent, object> InputSelector { get; }

    public Task StartAsync(IFunctionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        _subscription = _eventBus.Subscribe<TEvent>(async (@event, ct) =>
        {
            try
            {
                var input = InputSelector(@event);
                var result = await host.ExecuteAsync(
                    TargetFunctionType,
                    input: input,
                    options: new FunctionExecutionOptions
                    {
                        EnableLogging = true,
                        EnableTiming = true,
                        ExecutionSource = $"DomainEvent:{typeof(TEvent).Name}"
                    },
                    cancellationToken: ct);

                if (!result.IsSuccess)
                {
                    _logger?.LogWarning("DomainEventTrigger target '{Function}' returned failure: {Message}",
                        TargetFunctionType, result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DomainEventTrigger for {Event} → '{Function}' threw.",
                    typeof(TEvent).Name, TargetFunctionType);
            }
        });

        _logger?.LogInformation("DomainEventTrigger subscribed: {Event} → '{Function}'",
            typeof(TEvent).Name, TargetFunctionType);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        _logger?.LogInformation("DomainEventTrigger unsubscribed: {Event} → '{Function}'",
            typeof(TEvent).Name, TargetFunctionType);
        return Task.CompletedTask;
    }
}
