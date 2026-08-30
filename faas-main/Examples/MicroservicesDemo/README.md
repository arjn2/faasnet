# Artichoke-FaaS v9.0.0 — Microservices without Docker

> **The pitch:** All the benefits of microservices (process isolation, independent deployment, auto-restart, HTTP gateway, service discovery) without Docker, without Kubernetes, without Azure, without AWS. Just `dotnet exec` + a supervisor.

## What changed in v9.0.0

v8.0.6 was an **in-process** function framework — fast, but no isolation. One function crashing could take down the whole app.

v9.0.0 adds a **microservices layer**: each service runs in its own `dotnet exec` process, spawned and monitored by a supervisor. If a service crashes, only that service dies — the supervisor restarts it within seconds.

```
┌────────────────────────────────────────────────────────────────┐
│  faas-supervisor  (one process, you start this manually)        │
│                                                                  │
│  Spawns: dotnet exec AuditService.dll   --urls :5001            │
│          dotnet exec OrderService.dll   --urls :5002            │
│          dotnet exec NotifyService.dll  --urls :5003            │
│                                                                  │
│  Monitors: GET http://localhost:5001/health every 5s            │
│  Restarts: child exit → kill + respawn (exp. backoff)           │
│                                                                  │
│  Gateway:  GET  /api/audit/*  → proxy to :5001/*                │
│            POST /api/orders/* → proxy to :5002/*                │
│                                                                  │
│  Exposes port 8080 (single external entry point)                │
└────────────────────────────────────────────────────────────────┘
         ↑
         │ HTTP (port 8080)
   external clients
```

## Why this is interesting

| | Azure Functions | OpenFaaS | **Artichoke v9.0.0** |
|---|---|---|---|
| Cloud account required | ✅ | ❌ (self-hosted) | ❌ |
| Docker required | ❌ | ✅ | ❌ |
| Kubernetes required | ❌ | ✅ (for prod) | ❌ |
| Cold start | 1-5s | 200-2000ms (container pull) | **~3s** (one-time boot) |
| Warm invocation | 1-50ms | 1-20ms | **~1ms** (in-process) / **~5ms** (cross-service HTTP) |
| Process isolation | ✅ (per function) | ✅ (container per function) | ✅ (per service) |
| Auto-restart | ❌ (you write it) | ✅ | ✅ |
| Service discovery | ❌ | ✅ (Prometheus-style) | ✅ (`/admin/status`) |
| HTTP gateway | built-in | built-in | built-in (`/api/{service}/*`) |
| Multi-language | ✅ | ✅ | ❌ (.NET only) |
| Scale-to-zero | ✅ | ✅ | ❌ (services stay warm) |
| Cost | $ per invocation | infra cost | just infra |

**The trade-off:** v9.0.0 doesn't try to compete on scale-to-zero or multi-language. It competes on **operational simplicity**. You get microservices-grade isolation and orchestration with zero infrastructure beyond .NET 9.

## What's in this release

### New: `Artichoke.Microservices.Supervisor/`
- `Program.cs` — supervisor entry point (port 8080)
- `ServiceProcess.cs` — wraps a child process, heartbeats, auto-restart on exit
- `ServiceManager.cs` — loads `services.json`, tracks all instances, round-robin routing
- `Controllers.cs` — admin endpoints + HTTP gateway proxy

### New: `Examples/MicroservicesDemo/`
- `AuditService/` — tiny ASP.NET Core app on port 5001 (logs audit events)
- `OrderService/` — tiny ASP.NET Core app on port 5002 (creates orders, calls AuditService via the gateway to log the creation)

### Existing (from v8.0.6, unchanged)
- `Artichoke.FaaS.Core/` — function/trigger/event-bus contracts
- `Artichoke.FaaS.Runtime/` — in-process `IFunctionHost` (now also usable inside individual microservices)
- `Examples/BMS-BookManagementSystem/` — the in-process sample (still works as-is)

## How to run it

```bash
# 1. Build everything
dotnet build Artichoke-FaaS-Platform.sln --configuration Release

# 2. Configure services (already done in services.json)
# Edit Artichoke.Microservices.Supervisor/services.json if you want to add/remove services

# 3. Start the supervisor (it spawns the services)
cd Artichoke.Microservices.Supervisor
dotnet run --configuration Release --urls http://localhost:8080
```

You should see:

```
info: Artichoke.Microservices.Supervisor.ServiceProcess[0]
      Starting service 'audit' instance #1 on port 5001 (dll: .../AuditService.dll)
info: Artichoke.Microservices.Supervisor.ServiceProcess[0]
      Service 'audit' #1 started (PID 6458)
info: Artichoke.Microservices.Supervisor.ServiceProcess[0]
      Starting service 'orders' instance #1 on port 5002 (dll: .../OrderService.dll)
info: Artichoke.Microservices.Supervisor.ServiceProcess[0]
      Service 'orders' #1 started (PID 6468)
info: Artichoke.Microservices.Supervisor.Program[0]
      === faas-supervisor v9.0.0 started on http://localhost:8080 ===
```

Now exercise it:

```bash
# Admin: list all services + their health
curl http://localhost:8080/admin/status

# Gateway: create an order (routed to OrderService on :5002)
curl -X POST http://localhost:8080/api/orders/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Charlie","totalAmount":42.50}'

# Gateway: list orders
curl http://localhost:8080/api/orders/orders

# Gateway: hit AuditService directly
curl -X POST http://localhost:8080/api/audit/log \
  -H "Content-Type: application/json" \
  -d '{"eventType":"UserLogin","user":"alice","entityType":"User","entityId":"42"}'

# Demonstrate auto-restart: kill the OrderService process
kill -9 $(ss -tlnp | grep ':5002' | grep -oP 'pid=\K[0-9]+' | head -1)

# Wait 5 seconds, hit it again — supervisor restarted it
sleep 5
curl http://localhost:8080/api/orders/orders
```

The supervisor log will show:

```
warn: Service 'orders' #1 (PID 6468) exited with code 137
warn: Service 'orders' #1 restarting (attempt 1, backoff 2s)
info:  Service 'orders' #1 started (PID 6908)
```

## The services.json format

```json
{
  "Services": [
    {
      "Name": "audit",
      "DllPath": "/absolute/path/to/AuditService.dll",
      "Port": 5001,
      "Instances": 1,
      "HeartbeatInterval": "00:00:05",
      "MaxMissedHeartbeats": 3,
      "MaxRestartBackoff": "00:00:30"
    },
    {
      "Name": "orders",
      "DllPath": "/absolute/path/to/OrderService.dll",
      "Port": 5002,
      "Instances": 3
    }
  ]
}
```

- `Instances: 3` runs 3 copies on ports 5002/5003/5004 — the gateway round-robins between them.
- `HeartbeatInterval: 5s` + `MaxMissedHeartbeats: 3` = 15s before a stuck service gets force-restarted.
- Process exit triggers immediate restart (with exponential backoff up to `MaxRestartBackoff`).

## What each service looks like

A v9.0.0 microservice is just a regular ASP.NET Core app. No special base class, no SDK, no framework dependency:

```csharp
// Program.cs for a v9.0.0 microservice — ~30 lines total
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();

// Required: /health endpoint that the supervisor pings
app.MapGet("/health", () => new
{
    status = "alive",
    service = "audit",
    at = DateTime.UtcNow,
    pid = Environment.ProcessId
});

// Your business endpoints (the gateway routes /api/{service}/* here)
app.MapPost("/log", (AuditEntry entry) => { /* ... */ });
app.MapGet("/entries", () => /* ... */);

app.Run();
```

That's it. The supervisor doesn't know or care how the service is implemented — only that it has `/health` and listens on the configured port.

## What's next (roadmap)

This is v9.0.0 — the **proof that microservices-without-Docker is viable in .NET**. To make it a complete Azure Functions / OpenFaaS competitor, the next steps are:

1. **CLI** — `faas add service.dll`, `faas list`, `faas logs <service>`, `faas scale <service> N`, `faas restart <service>`. (~200 lines, System.CommandLine.)
2. **Hot reload** — supervisor watches `services/*.dll` for changes; new DLL = automatic restart of just that service. No downtime for the rest.
3. **Inter-service typed RPC** — `IServiceProxy<T>` via DispatchProxy, so `services.Orders.CreateOrder(req)` is a typed call instead of `HttpClient.PostAsync("http://supervisor/api/orders/orders", ...)`.
4. **Event bus across processes** — OrderService publishes `OrderCreatedEvent`, AuditService subscribes. Backed by Redis Streams or NATS so it works across machines.
5. **Multi-machine clustering** — multiple supervisors on different machines, gossip protocol, shared service registry. (This is where it starts to look like Dapr or Istio.)
6. **OpenTelemetry** — distributed tracing across service calls, metrics export to Prometheus.
7. **gRPC support** — for high-throughput inter-service calls (HTTP is fine for most cases but gRPC is ~5x faster).
8. **YAML/JSON service manifests** — `faas.yaml` per service with env vars, scaling, health check paths, dependencies.

Items 1-3 are probably 1-2 weeks of focused work. Items 4-6 are 1-2 months. Item 7-8 are nice-to-haves.

## The honest comparison

v9.0.0 is **not** a drop-in Azure Functions replacement. It doesn't have scale-to-zero, multi-language, or the cloud integration. What it has is:

- **Operational simplicity.** No Docker daemon to run, no Kubernetes API to learn, no cloud account. `dotnet run` and you're done.
- **Process isolation.** One service crashing doesn't take down the others — unlike v8.0.6's in-process model.
- **Auto-restart.** Free, with exponential backoff.
- **Single entry point.** Clients only know about `localhost:8080`. Internal service topology is invisible.
- **Independent deployment.** Build `AuditService.dll`, drop it in the path, restart supervisor. Done.
- **.NET-native.** No sidecar containers, no Python shims, no JVM warmup.

For a .NET team that wants microservices without the infrastructure overhead, v9.0.0 is a viable starting point. Add the CLI + hot reload + inter-service RPC and it's a real framework.

## License

MIT — same as the rest of the project.
