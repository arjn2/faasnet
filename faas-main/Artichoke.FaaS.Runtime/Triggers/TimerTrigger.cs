using Artichoke.FaaS.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Artichoke.FaaS.Runtime.Triggers;

/// <summary>
/// Fires a target function on a fixed interval. The classic "heartbeat" / "cron" trigger.
///
/// This is a real trigger (not the legacy CustomTriggerBase stub that did Task.Delay(50)).
/// It uses PeriodicTimer under the hood and runs the function via IFunctionHost.ExecuteAsync.
/// </summary>
public sealed class TimerTrigger : ITrigger
{
    private readonly TimeSpan _interval;
    private readonly ILogger<TimerTrigger>? _logger;
    private PeriodicTimer? _timer;
    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    public TimerTrigger(string targetFunctionType, TimeSpan interval, ILogger<TimerTrigger>? logger = null)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
        TargetFunctionType = targetFunctionType ?? throw new ArgumentNullException(nameof(targetFunctionType));
        _interval = interval;
        _logger = logger;
    }

    public string TriggerType => $"TimerTrigger:{TargetFunctionType}";
    public string TargetFunctionType { get; }
    public string DisplayName => $"Timer Trigger ({_interval.TotalSeconds:F1}s → {TargetFunctionType})";
    public string Description => $"Fires '{TargetFunctionType}' every {_interval.TotalSeconds:F1} second(s).";

    public Task StartAsync(IFunctionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(_interval);
        _loopTask = Task.Run(() => RunLoopAsync(host, _cts.Token), _cts.Token);
        _logger?.LogInformation("TimerTrigger started: firing '{Function}' every {Interval}s",
            TargetFunctionType, _interval.TotalSeconds);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _timer?.Dispose();
        if (_loopTask is not null)
        {
            try { await _loopTask.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex) { _logger?.LogWarning(ex, "TimerTrigger loop ended with exception."); }
        }
        _logger?.LogInformation("TimerTrigger stopped: {Function}", TargetFunctionType);
    }

    private async Task RunLoopAsync(IFunctionHost host, CancellationToken ct)
    {
        try
        {
            while (await _timer!.WaitForNextTickAsync(ct))
            {
                try
                {
                    var result = await host.ExecuteAsync(
                        TargetFunctionType,
                        input: null,
                        options: new FunctionExecutionOptions
                        {
                            EnableLogging = true,
                            EnableTiming = true,
                            ExecutionSource = "TimerTrigger"
                        },
                        cancellationToken: ct);

                    if (!result.IsSuccess)
                    {
                        _logger?.LogWarning("TimerTrigger target '{Function}' returned failure: {Message}",
                            TargetFunctionType, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "TimerTrigger target '{Function}' threw.", TargetFunctionType);
                }
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
    }
}
