using Artichoke.Microservices.Supervisor;
using Microsoft.AspNetCore.OpenApi;

// =============================================================================
// faas-supervisor — the only process you start manually.
//
// What it does:
//   1. Reads services.json (lists the microservices to run)
//   2. Spawns each service as `dotnet exec <dll> --urls http://localhost:<port>`
//   3. Pings each service's /health endpoint every 5s
//   4. If 3 pings fail in a row, kills + restarts the service (with backoff)
//   5. Exposes port 8080 as the single external entry point:
//      - GET  /admin/status  → list of all services + health
//      - ANY  /api/{service}/* → proxy to that service's port
//
// What it doesn't do (intentionally):
//   - No Docker, no containers
//   - No Kubernetes, no orchestration beyond restart-on-crash
//   - No cloud dependencies
//   - No CLI (yet) — just edit services.json and restart
//
// Usage:
//   dotnet run --project Artichoke.Microservices.Supervisor --urls http://localhost:8080
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("supervisor");
builder.Services.AddHttpClient("gateway");
builder.Services.AddSingleton<ServiceManager>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Load + start all configured services.
// Find the config path: first arg that looks like a file path, else services.json next to the DLL.
var configPath = args.FirstOrDefault(a =>
        !a.StartsWith("--")
        && !a.StartsWith("/")
        && !a.StartsWith("http://")
        && !a.StartsWith("https://")
        && a.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    ?? Path.Combine(AppContext.BaseDirectory, "services.json");

var manager = app.Services.GetRequiredService<ServiceManager>();
await manager.LoadFromConfigAsync(configPath);

app.MapControllers();

// Make sure we clean up child processes on shutdown
var lifetime = app.Lifetime;
lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Supervisor shutting down — stopping all child services...");
    manager.DisposeAsync().AsTask().Wait();
});

app.Logger.LogInformation("=== faas-supervisor v9.1.1 started on http://localhost:8080 ===");
app.Logger.LogInformation("Admin:    GET  http://localhost:8080/admin/status");
app.Logger.LogInformation("Gateway:  ANY  http://localhost:8080/api/{{service}}/*");
app.Logger.LogInformation("Config:   services.json loaded {Count} service(s)", manager.GetAllInstances().Count());

app.Run();
