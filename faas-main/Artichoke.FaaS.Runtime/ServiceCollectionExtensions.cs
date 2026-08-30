using Artichoke.FaaS.Core.Interfaces;
using Artichoke.FaaS.Runtime.Events;
using Artichoke.FaaS.Runtime.Triggers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artichoke.FaaS.Runtime;

/// <summary>
/// Fluent builder for registering functions and triggers with the Artichoke-FaaS framework.
///
/// Usage in Program.cs:
/// <code>
/// builder.Services.AddArtichokeFaaS(faas =&gt; faas
///     .RegisterFunction&lt;AuditFunction&gt;()
///     .RegisterFunction&lt;SearchIndexFunction&gt;()
///     .RegisterFunction&lt;NotificationFunction&gt;()
///     .RegisterFunction&lt;HeartbeatFunction&gt;()
///     .AddTimerTrigger("BMS.Heartbeat", TimeSpan.FromSeconds(10))
///     .AddDomainEventTrigger&lt;BookCreatedEvent&gt;("BMS.Audit", e =&gt; new { eventType = "BookCreatedEvent", bookId = e.Book.Id, ... })
///     .AddDomainEventTrigger&lt;BookCreatedEvent&gt;("BMS.SearchIndex", e =&gt; new { ... })
///     .AddDomainEventTrigger&lt;BookCreatedEvent&gt;("BMS.Notification", e =&gt; new { ... }));
/// </code>
/// </summary>
public sealed class ArtichokeFaaSBuilder
{
    private readonly IServiceCollection _services;

    internal ArtichokeFaaSBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>Register a function by type. The function will be instantiated via DI (ActivatorUtilities).</summary>
    public ArtichokeFaaSBuilder RegisterFunction<TFunction>() where TFunction : class, ICustomFunction
    {
        _services.AddSingleton<ICustomFunction, TFunction>();
        return this;
    }

    /// <summary>Register a function instance directly (e.g. for tests or pre-built singletons).</summary>
    public ArtichokeFaaSBuilder RegisterFunction(ICustomFunction instance)
    {
        _services.AddSingleton(instance);
        return this;
    }

    /// <summary>Add a timer trigger that fires <paramref name="functionType"/> every <paramref name="interval"/>.</summary>
    public ArtichokeFaaSBuilder AddTimerTrigger(string functionType, TimeSpan interval)
    {
        _services.AddSingleton<ITrigger>(sp => new TimerTrigger(
            functionType, interval,
            sp.GetService<ILogger<TimerTrigger>>()));
        return this;
    }

    /// <summary>
    /// Add a domain-event trigger that fires <paramref name="functionType"/> when an event of type
    /// <typeparamref name="TEvent"/> is published. <paramref name="inputSelector"/> maps the event
    /// to the function input payload.
    /// </summary>
    public ArtichokeFaaSBuilder AddDomainEventTrigger<TEvent>(
        string functionType,
        Func<TEvent, object> inputSelector)
        where TEvent : class, IDomainEvent
    {
        _services.AddSingleton<ITrigger>(sp => new DomainEventTrigger<TEvent>(
            functionType, inputSelector,
            sp.GetRequiredService<IDomainEventBus>(),
            sp.GetService<ILogger<DomainEventTrigger<TEvent>>>()));
        return this;
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the Artichoke-FaaS framework: IFunctionHost (singleton), IDomainEventBus (in-process),
    /// and TriggerHost (IHostedService). Use the callback to register functions and triggers.
    /// </summary>
    public static IServiceCollection AddArtichokeFaaS(
        this IServiceCollection services,
        Action<ArtichokeFaaSBuilder>? configure = null)
    {
        // Core: the host (picks up ICustomFunction instances from DI) and the event bus.
        services.AddSingleton<IFunctionHost, FunctionHost>();
        services.AddSingleton<IDomainEventBus, InProcessDomainEventBus>();

        // TriggerHost: IHostedService that starts/stops all ITrigger instances registered in DI.
        services.AddHostedService<TriggerHost>();

        // Let the caller register functions and triggers.
        if (configure is not null)
        {
            configure(new ArtichokeFaaSBuilder(services));
        }

        return services;
    }
}
