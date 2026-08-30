# FaasNet

> Microservices and event-driven architecture without Docker, without Kubernetes, without a cloud account. Just .NET 9 processes and a ~400-line supervisor.

**Version:** 9.1.1 — built, tested, and demoed end-to-end on Debian 13 / .NET 9.0.317.

## What this is

A small .NET 9 framework + supervisor for running event-driven microservices as **plain OS processes** — no containers, no orchestration layer, no per-call billing. The supervisor spawns your services via `dotnet exec`, monitors their `/health` endpoint, restarts them on crash, and exposes a single HTTP gateway that routes `/api/{service}/*` to the right child port.

If you've ever wanted microservices-grade isolation (process boundaries, independent deployment, auto-restart) without standing up Docker / minikube / k3s / a managed Kubernetes cluster — this is that.

### The pitch in one paragraph

For 25 years the industry has trained developers that "microservices" means "Docker containers." It doesn't. Microservices = process boundaries + HTTP + a supervisor. The OS has given you the first one for free since 1969. .NET gives you the second one in the box. This project wrote the third one in ~400 lines. No Docker daemon, no `kubectl`, no Helm charts, no Azure subscription, no cold starts, no per-invocation billing.

## What's in the repo

| Path | What |
|---|---|
| `Artichoke.FaaS.Core/` | Framework contracts: `ICustomFunction`, `IFunctionHost` (split into `IFunctionRegistry` + `IFunctionInvoker`), `ITrigger`, `IDomainEventBus`, `FunctionExecutionOptions` |
| `Artichoke.FaaS.Runtime/` | Implementation: `FunctionHost` (fast+slow path), `InProcessDomainEventBus`, `TimerTrigger`, `DomainEventTrigger<TEvent>`, `TriggerHost` (IHostedService), fluent `AddArtichokeFaaS()` builder |
| `Artichoke.Microservices.Supervisor/` | The host process: spawns child `dotnet exec` processes, heartbeats `/health` every 5 s, restarts on crash (exp. backoff up to 30 s), exposes HTTP gateway on :8080 |
| `Examples/BMS-BookManagementSystem/` | In-process sample: clean-architecture book CRUD with domain events triggering FaaS functions (Audit, SearchIndex, Notification). Requires EF Core + SQL Server/SQLite. |
| `Examples/MicroservicesDemo/` | Cross-process sample: AuditService + OrderService where OrderService calls AuditService through the supervisor gateway |
| `Examples/FlightSimDemo/` | Game-FaaS sample: 5 services (Aircraft, ATC, Weather, Scoring, Persistence) + a GameClient simulating a player flying KLAX → KSFO. AI traffic spawns on demand when the player enters a region, despawns when they leave. |
| `test/Artichoke.FaaS.SmokeTests/` | 11 xUnit tests covering `IFunctionHost` fast/slow paths + `IDomainEventBus` fan-out (~2 s total) |
| `Manual/` | 7-chapter design manual (Introduction, Quick Start, Installation, Architecture, Triggers, Development Kit, Function Interface) |
| `FaaS-ACTIONS-TRIGGERS.md` | Reference for built-in triggers and action types |
| `CHANGELOG.md` | Version history |

## Prerequisites

- **.NET 9 SDK** (9.0.x or higher). Download from <https://dotnet.microsoft.com/download/dotnet/9.0>.
- Linux / macOS / Windows. Tested on Debian 13 (trixie) x86_64 with .NET 9.0.317.
- `curl` (for the examples below). On Linux also `ss`/`lsof` to find child PIDs.
- **No Docker. No Kubernetes. No SQL Server** required for the supervisor demos (the BMS sample does need a database — see its own README).

## Installation

```bash
# 1. Extract the source
unzip faas-main.zip -d faas
cd faas/faas-main

# 2. Restore + build all 13 projects (Core, Runtime, Supervisor, 7 demo services, smoke tests)
dotnet build Artichoke-FaaS-Platform.sln --configuration Release
# Expect: 0 warnings, 0 errors, ~10 s on a warm machine.

# 3. Fix services.json — it ships with hardcoded absolute paths that won't match your machine.
#    Replace the placeholder below with the absolute path to your faas-main directory:
ROOT=$(pwd)
sed -i "s|/home/z/my-project/test-v910/faas-3.3.2.1|${ROOT}|g" \
    Artichoke.Microservices.Supervisor/services.json

# 4. The supervisor loads services.json from its own bin directory, not the source tree.
#    Copy the (now-fixed) config next to the built DLL:
cp Artichoke.Microservices.Supervisor/services.json \
   Artichoke.Microservices.Supervisor/bin/Release/net9.0/services.json

# 5. Run the smoke tests to confirm the framework itself works:
dotnet test test/Artichoke.FaaS.SmokeTests/Artichoke.FaaS.SmokeTests.csproj --configuration Release
# Expect: 11 passed, ~2 s.
```

If `services.json` still references missing DLLs, the supervisor will crash on startup with `FileNotFoundException: Service DLL not found: ...` — re-check step 3.

## Usage

### A. Start the supervisor (spawns all 7 demo services)

```bash
cd Artichoke.Microservices.Supervisor
dotnet run --configuration Release --urls http://localhost:8080
```

You should see in the log:

```
info: Artichoke.Microservices.Supervisor.ServiceProcess[0]
      [audit#1] Now listening on: http://localhost:5001
info: Artichoke.Microservices.Supervisor.ServiceProcess[0]
      [orders#2] Now listening on: http://localhost:5002
info: Artichoke.Microservices.Supervisor.ServiceProcess[0]
      [aircraft#3] Now listening on: http://localhost:5101
...
info: Program[0]
      === faas-supervisor v9.1.1 started on http://localhost:8080 ===
```

Leave this terminal running. In another terminal, exercise the gateway.

### B. The admin endpoint (service discovery + health)

```bash
curl -s http://localhost:8080/admin/status | jq .
```

Returns a JSON object with `supervisor` (version), `startedAt`, and a `services[]` array. Each entry has: `name`, `instanceId`, `port`, `isRunning`, `pid`, `startedAt`, `lastHeartbeat`, `restartCount`, `consecutiveFailures`.

### C. MicroservicesDemo — orders + audit via the gateway

```bash
# Create an order (gateway routes /api/orders/* to OrderService on :5002)
curl -X POST http://localhost:8080/api/orders/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Charlie","totalAmount":42.50}'
# {"id":1,"customerName":"Charlie","totalAmount":42.5,"createdAt":"2026-08-27T..."}

# List orders
curl -s http://localhost:8080/api/orders/orders

# Send an audit event (gateway routes /api/audit/* to AuditService on :5001)
curl -X POST http://localhost:8080/api/audit/log \
  -H "Content-Type: application/json" \
  -d '{"eventType":"UserLogin","user":"alice","entityType":"User","entityId":"42"}'

# List audit entries (will contain both OrderCreated events auto-published by OrderService
# and any events you POSTed manually)
curl -s http://localhost:8080/api/audit/entries
```

### D. Demonstrate auto-restart

```bash
# Find the OrderService PID and kill it
kill -9 $(ss -tlnp | grep ':5002' | grep -oP 'pid=\K[0-9]+' | head -1)

# Wait a few seconds, then hit it again
sleep 8
curl -s http://localhost:8080/api/orders/orders

# The supervisor will have logged:
#   warn: Service 'orders' #2 (PID xxxx) exited with code 137
#   warn: Service 'orders' #2 restarting (attempt 1, backoff 2s)
#   info:  Service 'orders' #2 started (PID yyyy)
# And /admin/status will show orders.restartCount = 1.
```

### E. FlightSimDemo — the cool one

In a third terminal:

```bash
cd Examples/FlightSimDemo/GameClient
dotnet run --configuration Release -- http://localhost:8080 CaptainAero
```

You'll see a simulated KLAX → KSFO flight:

```
=== Flight Sim GameClient v9.1.0 ===
Player: CaptainAero
Supervisor: http://localhost:8080

=== Scenario: A short flight KLAX → KSFO through 2 regions ===

[05:32:03] CaptainAero taking off from KLAX...
[05:32:05] CaptainAero entered region ZLA (Los Angeles Center)...
  → Spawned 4 AI aircraft in ZLA on demand!
[05:32:06] CaptainAero entered region ZOA (Oakland Center)...
  → Spawned 4 AI aircraft in ZOA on demand!
[05:32:08] CaptainAero left region ZLA...
  → Despawned 4 AI aircraft from ZLA (no longer needed).
[05:32:09] CaptainAero landing at KSFO...

=== Flight complete. Checking final state... ===

--- Aircraft currently tracked ---
  Total aircraft: 4, regions active: 1
    ZOA: 4 aircraft

--- ATC clearances issued ---
  Total: 4
    [05:32:04] CaptainAero, cleared for takeoff runway 25L, wind 270 at 10.
    [05:32:05] CaptainAero, contact ZLA Center on 124.7.
    [05:32:06] CaptainAero, contact ZOA Center on 124.7.
    [05:32:09] CaptainAero, cleared to land runway 28L.

--- Player score ---
  Total: 1400
  Flights: 3

--- Flight logs ---
  Total: 1
    [05:32:09] CaptainAero KLAX→KSFO (65 min, score 1400)
```

What just happened: each "enter region" call told the AircraftService to spawn 3-5 AI aircraft for that region (transient state, in-memory in a separate process). When the player left ZLA, those aircraft were despawned — they no longer exist anywhere. The game client never knew the actual port of any service; it only talked to `:8080` and the supervisor routed.

## The architecture pattern

```
                  ┌──────────────────────────┐
                  │  Your client / game       │
                  │  (per-frame loop stays    │
                  │   in-process; events go   │
                  │   through the gateway)    │
                  └──────────────┬───────────┘
                                 │ HTTP (port 8080)
                                 ▼
                  ┌──────────────────────────┐
                  │  faas-supervisor :8080    │
                  │  Gateway + restart         │
                  └──────────────┬───────────┘
                                 │ routes /api/{service}/*
                                 ▼
                       ┌─────────┬─────────┬─────────┐
                       │ service │ service │ service │  ← each is `dotnet exec`
                       │  :5001  │  :5002  │  :5003  │     a separate process
                       └─────────┴─────────┴─────────┘
```

The supervisor:

1. Reads `services.json` on startup
2. Spawns each service via `dotnet exec <dll> --urls http://localhost:<port>`
3. Pings `/health` every 5 s (configurable via `HeartbeatInterval`)
4. On `MaxMissedHeartbeats` consecutive failures OR `Process.Exited` → kill + respawn with exponential backoff up to `MaxRestartBackoff` (default 30 s)
5. Exposes `/api/{serviceName}/*` that proxies to the right child port (round-robin if `Instances > 1`)

## The `services.json` format

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
- `HeartbeatInterval: 5s` + `MaxMissedHeartbeats: 3` = 15 s before a stuck service gets force-restarted.
- Process exit triggers immediate restart (with exponential backoff up to `MaxRestartBackoff`).

## HTTP endpoints (the supervisor itself)

| Method | Path | What it does |
|---|---|---|
| `GET` | `/admin/status` | JSON snapshot of every service: pid, port, lastHeartbeat, restartCount, consecutiveFailures |
| `ANY` | `/api/{service}/*` | Proxies the rest of the path to the named service's port |

Swagger UI is available at `/swagger` when running in the `Development` environment.

## What a v9 microservice looks like

A v9 microservice is just a regular ASP.NET Core app — no special base class, no SDK, no framework dependency:

```csharp
// Program.cs — ~30 lines total
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

## Comparison to managed FaaS

| | Azure Functions (Consumption) | OpenFaaS | **v9.1.1** |
|---|---|---|---|
| Cloud account | required | self-hosted | not needed |
| Docker | no | required | **not needed** |
| Kubernetes | no | required (prod) | **not needed** |
| Cold start | 1-5 s | 200-2000 ms | none (always warm) |
| Warm HTTP call | 1-50 ms | 1-20 ms | ~5 ms (cross-service) |
| Process isolation | per function | per container | per service |
| Auto-restart | DIY | yes | yes |
| Scale-to-zero | yes | yes | no (services stay warm) |
| Multi-language | yes | yes | .NET only |

## What it's NOT

- **Not a drop-in Azure Functions replacement.** No scale-to-zero, no per-call billing, no multi-language, no cloud integration. If you want serverless, use serverless.
- **Not a competitor to Dapr / Istio / Linkerd.** Those are service meshes for hyperscale cross-machine deployments. This is for one machine (or a small fleet) with .NET on it.
- **Not a containerization tool.** If you need containers for security isolation, filesystem isolation, or multi-language runtimes, use containers.
- **Not production-hardened.** It's a working proof-of-concept with tests. Use it for prototypes, internal tools, learning. Audit it before betting a real product on it.

## Roadmap

What it would take to make this a real framework (in priority order):

1. **CLI** — `faas add`, `faas list`, `faas logs`, `faas scale`, `faas restart`. ~200 lines, System.CommandLine.
2. **Hot reload** — drop a new DLL, supervisor auto-restarts just that service. ~150 lines.
3. **Typed inter-service RPC** — `IServiceProxy<T>` via DispatchProxy so cross-service calls are typed. ~300 lines.
4. **Cross-process event bus** — Redis Streams or NATS-backed `IDomainEventBus`. ~500 lines.
5. **Multi-machine clustering** — multiple supervisors, gossip protocol, shared service registry. ~2000 lines.
6. **OpenTelemetry** — distributed tracing across service calls. ~500 lines.
7. **gRPC** — for high-throughput inter-service calls (5x faster than HTTP). ~400 lines.

Items 1-3 are 1-2 weeks. Items 4-6 are 1-2 months. Item 7 is a few days.

## License

MIT — see [LICENSE.txt](LICENSE.txt).

## Acknowledgments

The Artichoke architecture (Core / Interface / External layers, DDD-style domain events) came from the original BMS-API project by [@arlack9](https://github.com/arlack9). The v9.x refactor (FaaS framework + supervisor + flight sim demo) was built on top of it.
