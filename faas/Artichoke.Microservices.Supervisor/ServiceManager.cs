using System.Collections.Concurrent;
using System.Text.Json;

namespace Artichoke.Microservices.Supervisor;

/// <summary>
/// Manages all running service instances. Loads config from services.json on startup,
/// spawns the configured services, monitors them, restarts on crash.
/// </summary>
public sealed class ServiceManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, List<ServiceProcess>> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ServiceManager> _logger;
    private int _nextPort = 5001;

    public ServiceManager(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<ServiceManager>();
    }

    /// <summary>Load service descriptors from a JSON file and start all instances.</summary>
    public async Task LoadFromConfigAsync(string configPath)
    {
        if (!File.Exists(configPath))
        {
            _logger.LogWarning("Services config not found at {Path}", configPath);
            return;
        }

        var json = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<ServicesConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (config?.Services is null || config.Services.Count == 0)
        {
            _logger.LogWarning("No services found in {Path}", configPath);
            return;
        }

        foreach (var descriptor in config.Services)
        {
            await AddServiceAsync(descriptor);
        }
    }

    public async Task AddServiceAsync(ServiceDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.Name))
            throw new ArgumentException("Service name is required.", nameof(descriptor));
        if (!File.Exists(descriptor.DllPath))
            throw new FileNotFoundException($"Service DLL not found: {descriptor.DllPath}", descriptor.DllPath);

        var instances = new List<ServiceProcess>();
        for (int i = 0; i < Math.Max(1, descriptor.Instances); i++)
        {
            // For multi-instance, give each its own port
            var port = descriptor.Port != 0 ? descriptor.Port + i : Interlocked.Increment(ref _nextPort);
            var process = new ServiceProcess(
                descriptor with { Port = port },
                port,
                _loggerFactory.CreateLogger<ServiceProcess>(),
                _httpClientFactory);
            await process.StartAsync();
            instances.Add(process);
        }

        _services[descriptor.Name] = instances;
        _logger.LogInformation("Added service '{Name}' with {Count} instance(s)", descriptor.Name, instances.Count);
    }

    /// <summary>Pick an instance for the given service (round-robin).</summary>
    public ServiceProcess? GetInstance(string serviceName)
    {
        if (!_services.TryGetValue(serviceName, out var instances) || instances.Count == 0)
            return null;
        // Simple round-robin: return the first running instance
        return instances.FirstOrDefault(p => p.IsRunning) ?? instances[0];
    }

    /// <summary>Get all running service instances (for the /admin/status endpoint).</summary>
    public IEnumerable<ServiceProcess> GetAllInstances()
        => _services.Values.SelectMany(list => list);

    public async ValueTask DisposeAsync()
    {
        foreach (var instances in _services.Values)
        {
            foreach (var p in instances)
            {
                await p.DisposeAsync();
            }
        }
        _services.Clear();
    }

    private sealed class ServicesConfig
    {
        public List<ServiceDescriptor> Services { get; set; } = new();
    }
}
