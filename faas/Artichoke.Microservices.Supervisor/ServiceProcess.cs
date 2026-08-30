using System.Diagnostics;
using System.Text.Json;

namespace Artichoke.Microservices.Supervisor;

/// <summary>
/// Static configuration for one microservice. Loaded from services.json.
/// </summary>
public sealed record ServiceDescriptor
{
    /// <summary>Unique service name (e.g. "audit"). Used in the gateway route /api/{name}/*.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Absolute or relative path to the service .dll (the one with the ASP.NET Core Program.Main).</summary>
    public string DllPath { get; init; } = string.Empty;

    /// <summary>Port the child service listens on. Supervisor pings <c>http://localhost:{Port}/health</c>.</summary>
    public int Port { get; init; }

    /// <summary>Optional CLI args to pass after `--urls`.</summary>
    public string[] Args { get; init; } = Array.Empty<string>();

    /// <summary>Number of instances to run (round-robin load-balanced). Default 1.</summary>
    public int Instances { get; init; } = 1;

    /// <summary>Heartbeat interval. Default 5 seconds.</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Consecutive failed heartbeats before restart. Default 3.</summary>
    public int MaxMissedHeartbeats { get; init; } = 3;

    /// <summary>Restart backoff: first restart is immediate, then doubles up to this max. Default 30s.</summary>
    public TimeSpan MaxRestartBackoff { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Runtime state of one instance of a service. There can be N of these per ServiceDescriptor (if Instances > 1).
/// </summary>
public sealed class ServiceProcess : IAsyncDisposable
{
    private static int GlobalInstanceId;
    private readonly ServiceDescriptor _descriptor;
    private readonly ILogger<ServiceProcess> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private Process? _process;
    private int _consecutiveFailures;
    private int _restartAttempt;
    private DateTime? _lastHeartbeat;
    private CancellationTokenSource? _monitorCts;

    public int InstanceId { get; }
    public int Port { get; }
    public string Name => _descriptor.Name;
    public bool IsRunning => _process is not null && !_process.HasExited;
    public DateTime? StartedAt { get; private set; }
    public DateTime? LastHeartbeat => _lastHeartbeat;
    public int RestartCount { get; private set; }

    public ServiceProcess(ServiceDescriptor descriptor, int port, ILogger<ServiceProcess> logger, IHttpClientFactory httpClientFactory)
    {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Port = port;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        InstanceId = Interlocked.Increment(ref GlobalInstanceId);
    }

    public Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{_descriptor.DllPath}\" --urls http://localhost:{Port} " + string.Join(" ", _descriptor.Args),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(_descriptor.DllPath)) ?? Environment.CurrentDirectory,
        };

        // Pass through env vars so the child can log to the same place if needed
        foreach (var env in Environment.GetEnvironmentVariables())
        {
            var entry = (System.Collections.DictionaryEntry)env;
            psi.Environment[entry.Key.ToString()!] = entry.Value?.ToString();
        }
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["MICROSERVICE_NAME"] = _descriptor.Name;
        psi.Environment["MICROSERVICE_PORT"] = Port.ToString();
        psi.Environment["MICROSERVICE_INSTANCE_ID"] = InstanceId.ToString();

        _logger.LogInformation("Starting service '{Name}' instance #{InstanceId} on port {Port} (dll: {Dll})",
            Name, InstanceId, Port, _descriptor.DllPath);

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogInformation("[{Name}#{InstanceId}] {Line}", Name, InstanceId, e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogError("[{Name}#{InstanceId}] {Line}", Name, InstanceId, e.Data); };
        _process.Exited += (_, _) => OnProcessExited();

        if (!_process.Start())
        {
            _logger.LogError("Failed to start process for '{Name}' #{InstanceId}", Name, InstanceId);
            return Task.CompletedTask;
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        StartedAt = DateTime.UtcNow;
        Interlocked.Exchange(ref _restartAttempt, 0);
        _logger.LogInformation("Service '{Name}' #{InstanceId} started (PID {Pid})", Name, InstanceId, _process.Id);

        // Start the heartbeat monitor
        _monitorCts = new CancellationTokenSource();
        _ = Task.Run(() => MonitorHeartbeatsAsync(_monitorCts.Token));

        return Task.CompletedTask;
    }

    private void OnProcessExited()
    {
        var pid = _process?.Id ?? -1;
        var exitCode = _process?.ExitCode ?? -1;
        _logger.LogWarning("Service '{Name}' #{InstanceId} (PID {Pid}) exited with code {ExitCode}",
            Name, InstanceId, pid, exitCode);

        // Cancel the heartbeat monitor — it'll fail to connect and increment failures, but
        // we want to restart faster than that. The Exited event fires synchronously on the
        // thread pool, so we just kick off a restart task.
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay so the OS releases the port
                await Task.Delay(TimeSpan.FromSeconds(1));
                await RestartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Restart attempt for '{Name}' #{InstanceId} failed", Name, InstanceId);
            }
        });
    }

    private async Task MonitorHeartbeatsAsync(CancellationToken ct)
    {
        // Wait a bit for the service to start listening
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        var client = _httpClientFactory.CreateClient("supervisor");
        client.Timeout = TimeSpan.FromSeconds(3);

        while (!ct.IsCancellationRequested && IsRunning)
        {
            try
            {
                var resp = await client.GetAsync($"http://localhost:{Port}/health", ct);
                if (resp.IsSuccessStatusCode)
                {
                    _lastHeartbeat = DateTime.UtcNow;
                    Interlocked.Exchange(ref _consecutiveFailures, 0);
                }
                else
                {
                    Interlocked.Increment(ref _consecutiveFailures);
                    _logger.LogWarning("Service '{Name}' #{InstanceId} health check returned {Status} (failures: {Failures})",
                        Name, InstanceId, resp.StatusCode, _consecutiveFailures);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _consecutiveFailures);
                _logger.LogWarning("Service '{Name}' #{InstanceId} health check failed: {Message} (failures: {Failures})",
                    Name, InstanceId, ex.Message, _consecutiveFailures);
            }

            if (_consecutiveFailures >= _descriptor.MaxMissedHeartbeats)
            {
                _logger.LogError("Service '{Name}' #{InstanceId} missed {Failures} heartbeats — restarting",
                    Name, InstanceId, _consecutiveFailures);
                await RestartAsync();
                return; // RestartAsync spawns a new monitor
            }

            try { await Task.Delay(_descriptor.HeartbeatInterval, ct); }
            catch (TaskCanceledException) { break; }
        }
    }

    public async Task RestartAsync()
    {
        RestartCount++;
        var backoff = TimeSpan.FromSeconds(Math.Min(
            _descriptor.MaxRestartBackoff.TotalSeconds,
            Math.Pow(2, Math.Min(_restartAttempt, 5))));
        Interlocked.Increment(ref _restartAttempt);

        _logger.LogWarning("Service '{Name}' #{InstanceId} restarting (attempt {Attempt}, backoff {Backoff}s)",
            Name, InstanceId, _restartAttempt, backoff.TotalSeconds);

        await StopAsync();
        if (backoff > TimeSpan.Zero)
            await Task.Delay(backoff);
        await StartAsync();
    }

    public Task StopAsync()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;

        if (_process is not null && !_process.HasExited)
        {
            try
            {
                _logger.LogInformation("Stopping service '{Name}' #{InstanceId} (PID {Pid})", Name, InstanceId, _process.Id);
                _process.Kill(entireProcessTree: true);
                if (!_process.WaitForExit(5000))
                {
                    _logger.LogWarning("Service '{Name}' #{InstanceId} did not exit gracefully; force-killing", Name, InstanceId);
                    _process.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping service '{Name}' #{InstanceId}", Name, InstanceId);
            }
        }
        _process?.Dispose();
        _process = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    public object ToStatus() => new
    {
        name = Name,
        instanceId = InstanceId,
        port = Port,
        isRunning = IsRunning,
        pid = _process?.Id,
        startedAt = StartedAt,
        lastHeartbeat = _lastHeartbeat,
        restartCount = RestartCount,
        consecutiveFailures = _consecutiveFailures
    };
}
