using System.Text.Json;

namespace Artichoke.FaaS.Core.Interfaces;

// ============================================================================
// v8.0.6 — Trigger contracts.
//
// A trigger is something that fires functions automatically:
//   - TimerTrigger: fires a function on a schedule (heartbeat / cron)
//   - DomainEventTrigger<TEvent>: fires a function when a domain event is published
//   - HttpTrigger: fires a function when an HTTP request matches a route
//   - (your custom trigger)
//
// Triggers are started/stopped as part of the application lifecycle (via IHostedService
// in Artichoke.FaaS.Runtime.TriggerHost). A trigger's StartAsync runs for the lifetime
// of the app; when its condition fires, it calls host.ExecuteAsync(...) to invoke the
// target function.
//
// This replaces the legacy CustomTriggerBase stubs that did Task.Delay(50) and returned
// hardcoded objects.
// ============================================================================

/// <summary>
/// A trigger fires a target function when its condition is met.
/// Implemented as IHostedService by the runtime — StartAsync runs until the app shuts down.
/// </summary>
public interface ITrigger
{
    /// <summary>Unique trigger type identifier (e.g. "TimerTrigger:BMS.Heartbeat").</summary>
    string TriggerType { get; }

    /// <summary>The function type this trigger fires.</summary>
    string TargetFunctionType { get; }

    /// <summary>Display name for UI / logging.</summary>
    string DisplayName { get; }

    /// <summary>Human-readable description.</summary>
    string Description { get; }

    /// <summary>Start the trigger. Should return immediately (use a background task for the loop).</summary>
    Task StartAsync(IFunctionHost host, CancellationToken cancellationToken);

    /// <summary>Stop the trigger and release resources.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Strongly-typed trigger that fires when a domain event of type <typeparamref name="TEvent"/>
/// is published. The InputSelector maps the event to the function input payload.
/// </summary>
public interface IDomainEventTrigger<TEvent> : ITrigger
{
    /// <summary>Map a domain event to the input payload for the target function.</summary>
    Func<TEvent, object> InputSelector { get; }
}
