using Artichoke.FaaS.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artichoke.FaaS.Runtime.Triggers;

/// <summary>
/// IHostedService that starts/stops all registered <see cref="ITrigger"/> instances.
///
/// Register triggers as singleton ITrigger services in DI; this hosted service picks them up
/// via IEnumerable&lt;ITrigger&gt; and runs them for the lifetime of the app.
/// </summary>
public sealed class TriggerHost : IHostedService
{
    private readonly IFunctionHost _functionHost;
    private readonly IEnumerable<ITrigger> _triggers;
    private readonly ILogger<TriggerHost>? _logger;
    private CancellationTokenSource? _cts;

    public TriggerHost(
        IFunctionHost functionHost,
        IEnumerable<ITrigger> triggers,
        ILogger<TriggerHost>? logger = null)
    {
        _functionHost = functionHost ?? throw new ArgumentNullException(nameof(functionHost));
        _triggers = triggers ?? Enumerable.Empty<ITrigger>();
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var triggerList = _triggers.ToList();
        _logger?.LogInformation("TriggerHost starting {Count} trigger(s)", triggerList.Count);

        foreach (var trigger in triggerList)
        {
            try
            {
                await trigger.StartAsync(_functionHost, _cts.Token);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start trigger {TriggerType}", trigger.TriggerType);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("TriggerHost stopping {Count} trigger(s)", _triggers.Count());
        _cts?.Cancel();

        foreach (var trigger in _triggers)
        {
            try
            {
                await trigger.StopAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to stop trigger {TriggerType}", trigger.TriggerType);
            }
        }
    }
}
