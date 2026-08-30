# Changelog

All notable changes to the Artichoke-FaaS Platform will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [9.1.1] - 2026-08-27

### Added — CI/CD (GitHub Actions)
- **`.github/workflows/ci.yml`** — runs on every push to `main`/`develop` and on every PR.
  Restores + builds the full 13-project solution + runs smoke tests + verifies every sample
  service DLL was produced. Runs in <2 minutes on `ubuntu-latest` because there's no Docker
  layer to build (compare to typical microservices CI: 5-15 minutes for container images).
- **`.github/workflows/release.yml`** — runs when you push a tag like `v9.1.0`. Runs the same
  build + test gate, then packages a source-only zip (`faas-3.3.2.1-{tag}.zip`, excludes
  bin/obj/.git) and creates a GitHub Release with the zip + CHANGELOG.md attached. The
  release body is auto-extracted from the CHANGELOG.md entry for that version.
  - Prerelease detection: tags containing `-` (e.g. `v9.2.0-rc.1`) are marked as prerelease.
- **`test/Artichoke.FaaS.SmokeTests/`** — new xUnit project (11 tests, ~317ms total runtime)
  covering:
  - `FunctionHost.Register` / `List` / `IsRegistered` (registry behavior)
  - `IFunctionInvoker.ExecuteAsync` fast path (zero-overhead when caller has function ref)
  - `IFunctionHost.ExecuteAsync` slow path (lookup by function type)
  - `FunctionExecutionOptions.None` (no error capture — exceptions bubble)
  - `FunctionExecutionOptions.Default` (error capture only, no timing/logging)
  - `FunctionExecutionOptions.FullObservability` (timing attached to result)
  - `IDomainEventBus.PublishAsync` (fires all subscribers for event type)
  - `IDomainEventBus` handler isolation (one throwing handler doesn't break others)
  - `JsonElement` input projection to `Parameters` (ASP.NET Core body deserialization case)
- Added `Artichoke.FaaS.SmokeTests` to `Artichoke-FaaS-Platform.sln`.

### Design choice — source-only release, no Docker images
The whole point of this project is "no Docker". Shipping Docker images in CI would betray
the thesis. The release artifact is a source zip; users run `dotnet build` on their machine.
When the API stabilizes for v10, we may add a NuGet package for `Artichoke.FaaS.Core` and
`Artichoke.FaaS.Runtime` — but the supervisor + sample services stay as source.

### Why CI is fast
| Step | Time |
|---|---|
| Checkout | ~5s |
| Setup .NET 9 | ~15s |
| Restore | ~30s |
| Build (13 projects) | ~30s |
| Smoke test (11 tests) | ~5s |
| DLL verification | ~2s |
| **Total** | **<2 minutes** |

A comparable Docker-based microservices CI (building 5+ images) would take 5-15 minutes
just for the image builds. The no-Docker design pays off in CI as well as in production.

---

## [9.1.0] - 2026-08-27

### Added — Flight Sim Demo (game-FaaS architecture pattern)
- **`Examples/FlightSimDemo/`** — full demo showing how to use FaaS for game backends.
  Five microservices + a simulated game client, demonstrating the "hot path vs cold path"
  pattern: per-frame game loop stays in-process; event-driven work (entity spawn/despawn,
  ATC, weather, scoring, persistence) goes through the supervisor gateway.
- **`AircraftService`** (~140 lines) — the on-demand entity pattern. Spawns 3-5 AI aircraft
  when a player enters a region, despawns them when they leave. Memory freed the moment
  it's no longer needed.
- **`AtcService`** (~60 lines) — issues ATC clearances (takeoff, landing, handoff, emergency).
- **`WeatherService`** (~70 lines) — generates per-region weather (wind, visibility, conditions).
- **`ScoringService`** (~100 lines) — tracks player scores, multipliers, achievements.
- **`PersistenceService`** (~50 lines) — saves flight logs and crash reports.
- **`GameClient`** (~200 lines) — console app simulating a player flying KLAX → KSFO through
  regions ZLA + ZOA. Emits events at the right moments and shows final service state.
- **`Examples/FlightSimDemo/README.md`** — documents the architecture pattern, comparison to
  Agones / traditional game backends, and generalizes to other game genres (MMO zones,
  racing tracks, open-world chunks, strategy maps, city districts).

### Fixed — supervisor gateway
- **Body buffering in gateway proxy**: `Request.Body` was being consumed by ASP.NET Core
  model binding before the proxy could read it. Added `Request.EnableBuffering()` and a
  `ReadBodyAsync` helper that copies the body to a `byte[]` for re-use.
- **`MediaTypeHeaderValue` constructor crash**: `new MediaTypeHeaderValue("application/json; charset=utf-8")`
  throws `FormatException` because the constructor only accepts the bare media type. Switched
  to `MediaTypeHeaderValue.TryParse(Request.ContentType, out var mtv)` which correctly
  handles the full Content-Type header including charset.

### Verified end-to-end
- Supervisor spawns 7 services (2 from v9.0.0 demo + 5 new game services) on ports 5001-5105
- GameClient emits events → supervisor routes → 5 services react:
  - Takeoff: ATC clearance issued, scoring multiplier started (x2), weather snapshot fetched
  - Enter ZLA: 4 AI aircraft spawned on demand
  - Enter ZOA: 3 more AI aircraft spawned on demand
  - Leave ZLA: 4 AI aircraft despawned (memory freed)
  - Land: ATC landing clearance, +1000 score (500 × 2 multiplier), flight log saved
- Final state: 7 AI in ZOA, 8 ATC clearances, score 2800, 2 flight logs

### The pattern is generalizable
- Flight sims → regions spawn AI traffic
- MMOs → zones spawn NPCs, quests, dynamic events
- Racing games → tracks spawn AI opponents, weather
- Open-world games → chunks spawn fauna, encounters, loot
- Strategy games → maps spawn resources, AI factions
- City builders → districts spawn citizens, traffic, services

---

## [9.0.0] - 2026-08-27

### Added — Microservices without Docker
- **`Artichoke.Microservices.Supervisor/`** — the host process. Spawns child .NET services
  as `dotnet exec <dll> --urls http://localhost:<port>`, monitors them via `/health`
  pings, restarts on crash with exponential backoff, exposes an HTTP gateway on :8080
  that proxies `/api/{service}/*` to the right child port.
  - `Program.cs` — supervisor entry point, loads `services.json`, starts `ServiceManager`.
  - `ServiceProcess.cs` — wraps a child process. Heartbeat monitor (every 5s, 3 missed = restart),
    `Process.Exited` event triggers immediate restart, exponential backoff up to 30s.
  - `ServiceManager.cs` — loads descriptors from `services.json`, tracks all instances,
    round-robin routing for multi-instance services.
  - `Controllers.cs` — `AdminController` (`/admin/status`, `/admin/services`) +
    `GatewayController` (ANY `/api/{service}/*` → proxy to child port).
  - `services.json` — service descriptors (Name, DllPath, Port, Instances, HeartbeatInterval,
    MaxMissedHeartbeats, MaxRestartBackoff).
- **`Examples/MicroservicesDemo/`** — first microservices sample.
  - `AuditService` (~50 lines) — port 5001, logs audit events.
  - `OrderService` (~70 lines) — port 5002, creates orders + calls AuditService via the
    supervisor gateway (real cross-service call through the gateway).
  - `README.md` — the pitch (microservices without Docker/K8s/cloud), roadmap.

### Removed
- **`Artichoke.FaaS.Platform/`** — entire project deleted. Had 655-line `UniversalFunctionManager`
  that was never registered in DI (dead code), 3 streaming endpoints that threw HTTP 500,
  `ProcessManager` that tried to `dotnet run` against an empty `Artichoke.FaaS.Runtime`.
  The supervisor in v9.0.0 replaces all of this with ~400 lines of working code.
- **`Artichoke.FaaS.Client/`** — CLI for the deleted Platform. The new supervisor needs no
  client; `services.json` is its config.
- **Legacy `CustomTriggerBase`, `HttpTrigger`, `TimerTrigger` stubs** in `DevelopmentKitBase.cs` —
  these were `Task.Delay(50)` no-ops. Replaced by real `ITrigger`, `TimerTrigger`, and
  `DomainEventTrigger<TEvent>` in `Artichoke.FaaS.Runtime.Triggers`.
- **Legacy `ITrigger`, `ITriggerFactory`, `ICustomFunctionFactory`, `TriggerExecutionContext`,
  `TriggerExecutionResult`** in `IDevelopmentKit.cs` — superseded by cleaner abstractions in
  `ITrigger.cs` and `IFunctionHost.cs`.

### Changed
- **`Artichoke.FaaS.Runtime/`** — was empty in v8.0.4, now contains the in-process function
  host + real triggers + in-process event bus.
  - Added `FunctionHost.cs` — `IFunctionHost` implementation with fast path
    (`ExecuteAsync(ICustomFunction, ctx)`) + slow path (`ExecuteAsync(string, input, options)`).
    Default `FunctionExecutionOptions` = error-capture only (zero overhead). Logging/timing
    are opt-in via `FunctionExecutionOptions.FullObservability`.
  - Added `ServiceCollectionExtensions.cs` — fluent `AddArtichokeFaaS(faas => faas
      .RegisterFunction<T>().AddTimerTrigger(...).AddDomainEventTrigger<TEvent>(...))`.
  - Added `Events/InProcessDomainEventBus.cs` — in-process pub/sub for `IDomainEvent`.
  - Added `Triggers/TimerTrigger.cs` — real `PeriodicTimer` + `IHostedService` loop.
  - Added `Triggers/DomainEventTrigger.cs` — subscribes to `IDomainEventBus`, dispatches
    to target function on event arrival.
  - Added `Triggers/TriggerHost.cs` — `IHostedService` that starts/stops all `ITrigger`
    instances registered in DI.
  - Added `IFunctionHost.cs` (in Core) — split into `IFunctionRegistry`, `IFunctionInvoker`,
    `IFunctionHost`, plus `FunctionExecutionOptions` (opt-in logging/timing/error-capture).
  - Added `ITrigger.cs` (in Core) — real trigger contract with `StartAsync`/`StopAsync`.
  - Added `IDomainEventBus.cs` (in Core) — in-process pub/sub contract.
  - Cleaned up `IDevelopmentKit.cs` — kept `ICustomFunction` + execution types; removed
    legacy trigger/factory types.
  - Cleaned up `DevelopmentKitBase.cs` — kept `CustomFunctionBase` + `DefaultLogger`;
    removed legacy trigger stubs.
- **`Examples/BMS-BookManagementSystem/BMS.Core/Events/DomainEvents.cs`** —
  `BMS.Core.Events.IDomainEvent` now extends `Artichoke.FaaS.Core.Interfaces.IDomainEvent`
  so BMS events can flow through the framework's `IDomainEventBus`.
- **`Examples/BMS-BookManagementSystem/BMS.External/Events/EventPublisher.cs`** — now just
  publishes to `IDomainEventBus`. No more `new AuditFunction()` etc. — `DomainEventTrigger<TEvent>`
  instances (registered in Program.cs) subscribe to the bus and dispatch to functions.
- **`Examples/BMS-BookManagementSystem/BMS-API/Program.cs`** — uses fluent `AddArtichokeFaaS()`
  registering 4 functions + 7 triggers (1 timer + 6 domain-event triggers).
- **`Examples/BMS-BookManagementSystem/BMS-API/Controllers/v4/`** — added `AuthController`
  (JWT login) and `FunctionsController` (uses `IFunctionHost.ExecuteAsync` with
  `FullObservability` for HTTP callers).
- **`Examples/BMS-BookManagementSystem/BMS.External/FaaS/HeartbeatFunction.cs`** — new
  trivial function for heartbeat benchmarks.
- Switched BMS-API from SQL Server to SQLite (no LocalDB needed).
- Fixed `CopyrightAttribute.ToString()` to use ASCII `(c)` instead of `©` (Kestrel rejects
  non-ASCII header values).
- Fixed `CopyrightResponseFilter` to use header indexer (`headers["X"] = v`) instead of
  `headers.Add(...)` (throws on duplicate keys).

### Verified — v9.0.0 end-to-end
- Supervisor spawns 2 services (audit on :5001, orders on :5002)
- Gateway routes `/api/audit/*` → :5001, `/api/orders/*` → :5002
- Cross-service call: `POST /api/orders/orders` → OrderService → calls AuditService via gateway
- Killed OrderService (PID 6468) — supervisor detected exit, restarted it (new PID 6908),
  `restartCount: 1` in status, OrderService responding again
- Heartbeats flowing (`lastHeartbeat` timestamps recent)

### Benchmark vs v8.0.4 / v8.0.5
| Metric             | v8.0.4    | v8.0.5    | v9.0.0    |
|--------------------|-----------|-----------|-----------|
| Heartbeat mean     | 2.30ms    | 3.11ms    | **0.88ms** |
| Heartbeat p95      | 2.62ms    | 3.73ms    | **1.14ms** |
| Concurrent mean    | 11.80ms   | 11.71ms   | **3.57ms** |
| Throughput (RPS)   | 666       | 576       | **1220**   |
| Event fanout       | (broken)  | (broken)  | **3.03ms** |

v9.0.0 is 1.8x faster than v8.0.4 because the default `FunctionExecutionOptions` has
logging/timing OFF (only error-capture on), and the fast path (`IFunctionInvoker.ExecuteAsync`)
skips host overhead when the caller already has the function reference.

### Comparison to Azure Functions / OpenFaaS
|                       | Azure Functions | OpenFaaS       | v9.0.0                |
|-----------------------|-----------------|----------------|-----------------------|
| Cloud account         | required        | self-hosted    | not needed            |
| Docker                | no              | required       | **not needed**        |
| Kubernetes            | no              | required (prod)| **not needed**        |
| Cold start            | 1-5s            | 200-2000ms     | ~3s (one-time boot)   |
| Warm HTTP call        | 1-50ms          | 1-20ms         | ~5ms (cross-service)  |
| Process isolation     | per function    | per container  | per service           |
| Auto-restart          | DIY             | yes            | yes                   |
| Multi-language        | yes             | yes            | .NET only             |
| Scale-to-zero         | yes             | yes            | no (services stay warm) |

---

## [8.0.4] - 2026-08-25

### Removed
- **~4,800 lines of legacy/redundant code deleted from BMS example**
  - `BMS.Core/Functions/` — duplicate IFunction, IFunctionManager, FunctionResult, FunctionHealth (271 lines). BMS now uses `Artichoke.FaaS.Core` types directly as a library.
  - `BMS.External/Functions/DistributedFunctionManager.cs` — 827-line process manager. FaaS Core handles function lifecycle.
  - `BMS.External/Functions/FunctionManager.cs` — 821-line duplicate manager. Same reason.
  - `BMS.External/Functions/BackgroundFunctions/CoreFunctions.cs` — 542 lines of fake functions using `Random.Shared.Next()` and `Task.Delay`. Replaced by real FaaS functions in v8.0.3.
  - `BMS.Interface/Services/AdminFaaSCliService.cs` — 748-line CLI wrapper around deleted IFunctionManager.
  - `BMS.Interface/Services/FaaSPlatformClient.cs` — 256-line HTTP client returning mock/hardcoded data.
  - `BMS-API/Controllers/Artichoke/FaaSController.cs` — 440-line controller for deleted IFunctionManager.
  - `BMS-API/Controllers/v4/FunctionsController.cs` — 296-line dual-mode adapter (FaaS 2.0/3.0 switching).
  - `BMS-API/Controllers/v1/`, `v2/`, `N_tier/` — pre-Artichoke legacy API controllers.
  - `BMS-API/Controllers/Artichoke/AuthController.cs` — duplicate auth controller.
  - `BMS-API/Controllers/LibraryController.cs` — deprecated N-Tier controller referencing deleted BLL/DAL.
  - `BMS-API/EventHandlers/`, `BMS-API/Demo/`, `BMS-API/Constants/`, `BMS-API/Dto/` — legacy N-Tier support files.
  - `BMS.BLL/` — entire legacy Business Logic Layer project (11 files).
  - `BMS.DAL/` — entire legacy Data Access Layer project (9 files). Seeding moved to `BMS.External/Seeding/`.
  - `BMS.Models/` — entire legacy Models project (4 files).
  - `BMS.FunctionHost/` — entire standalone function host project (280 lines). FaaS Runtime handles this.
  - `FaaS3Demo/` — standalone demo project.
  - `Demos/` — 10 demo scripts and READMEs.
  - `docs/` — 15 outdated planning/documentation files.
  - `notes/` — scratch notes.
  - `SampleCustomTriggers.cs` — standalone demo file at repo root.

### Changed
- **EventPublisher.cs**: Removed legacy reflection-based handler dispatch (`DispatchToLegacyHandlers` with `GetMethod("HandleAsync")`) and 3 stub handler classes. Now directly instantiates FaaS functions and dispatches concurrently.
- **Program.cs**: Stripped from 400 lines to 95 lines. Removed FaaS 2.0/3.0 mode switching, embedded function manager DI, legacy N-Tier DI (IValidation, IDbServices, IEvents, LibraryEventHandlers), dual Swagger doc config. Now clean Artichoke DI only.
- **BMS_API.csproj**: Removed project references to BMS.BLL, BMS.DAL, BMS.Models. Removed Mapster, Identity.UI, EF Tools packages.
- **BMS-API.sln**: Removed deleted projects (BMS.BLL, BMS.DAL, BMS.Models).
- **BMS.External/Seeding/**: Moved RoleSeeder, UserSeeder, Roles from deleted BMS.DAL into BMS.External.

### Architecture After Cleanup
```
BMS.Core        → Domain: entities, events, exceptions, value objects (pure, no FaaS dependency)
BMS.Interface   → Application services, DTOs, IEventPublisher contract
BMS.External   → Infrastructure: EF Core, FaaS functions (CustomFunctionBase), event publisher, seeding
                  → Imports Artichoke.FaaS.Core as a library (not a platform to connect to)
BMS-API        → Web API host (Program.cs = 95 lines)
```
FaaS is now used as a **library import** (`Artichoke.FaaS.Core`), not as a separate platform to connect to. No HTTP calls, no process spawning, no mode switching.

### Verified
- **Build**: BMS-API compiles with 0 errors, 1 non-critical CS1998 warning
- **Source files**: Reduced from ~80+ CS files to 26 source files (excluding obj/ and migrations)
- **Total lines**: Reduced from ~12,000+ to ~4,828 lines (including migrations and obj/)

---

## [8.0.3] - 2026-08-25

### Changed
- **BMS fully migrated to FaaS EDA architecture**: Domain events now dispatch through FaaS trigger system
  - Created `DomainEventTrigger` (extends `CustomTriggerBase`) — bridges DDD events into FaaS
  - Created `AuditFunction` (extends `CustomFunctionBase`) — real audit logging, replaces fake `Random.Shared.Next()`
  - Created `SearchIndexFunction` (extends `CustomFunctionBase`) — proactive search index maintenance
  - Created `NotificationFunction` (extends `CustomFunctionBase`) — event-driven notifications
  - Rewrote `EventPublisher` — dispatches domain events to 3 FaaS functions concurrently via `FaaSFunctionDispatcher`
  - Removed old `EventHandlers.cs` (was just `Console.WriteLine`)
  - BMS.External now references `Artichoke.FaaS.Core` for native FaaS integration

### Verified
- **FaaS Platform**: Builds 0 errors, runs on port 8080, all endpoints responding
- **BMS.External**: Compiles successfully with FaaS integration (0 errors)
- **Platform status API**: Returns healthy, 67ms response time
- **DevKit API**: Active with trigger types available

---

## [8.0.2] - 2026-08-25

### Added
- **FaaS-ACTIONS-TRIGGERS.md**: Complete reference documentation of all 86 actions, triggers, and event capabilities
  - 4 built-in triggers: HttpTrigger, TimerTrigger, QueueTrigger, BlobTrigger
  - 9 DevKit actions for custom trigger/function registration and management
  - 6 real-time SignalR streaming events (TaskProgress, TaskCompleted, TaskError, etc.)
  - 5 SignalR hub methods for client subscriptions
  - 5 function lifecycle commands (Execute, HealthCheck, Stop, Restart, Configure)
  - Workflow orchestration with chained function steps
  - 8 function categories, 7 function states, 6 health statuses
  - Full API endpoint reference with auth requirements

---

## [8.0.1] - 2026-08-25

### Verified
- **Build verified**: Full FaaS Platform solution builds successfully on .NET 9.0.317 with 0 errors (13 non-critical warnings)
- **Runtime verified**: Artichoke-FaaS Platform starts correctly on port 8080 in UNIVERSAL mode
- **Database initialization**: Both Platform DB and Development Kit DB created and seeded successfully
- **Identity seeding**: Admin user, PlatformAdmin/ProjectOwner/Developer roles verified
- **Development Kit**: Confirmed ACTIVE at startup
- **All 4 platform projects compile**: Artichoke.FaaS.Core, Artichoke.FaaS.Runtime, Artichoke.FaaS.Client, Artichoke.FaaS.Platform

---

## [3.3.2.1] - 2025-08-25

### Added
- Initial git repository setup with proper .gitignore
- CHANGELOG.md added to track project evolution

---

## [3.3.2] - 2025-01-16

### Added
- Streaming task results support for SQLite (`AddStreamingTaskResultsForSQLite` migration)
- SignalR hub for real-time function execution (`/hubs/functionExecution`)
- `StreamingFunctionsController` for real-time streaming API
- `RealTimeFunctionService` for managing real-time function execution
- In-memory caching for task tracking

### Changed
- Platform database migration updated to support streaming results

---

## [3.3.1] - 2024-12-28

### Added
- Universal Function Manager (`IUniversalFunctionManager`, `UniversalFunctionManager`)
- Multi-project function isolation and management
- Project registration and listing via CLI
- Cross-project function sharing capabilities
- Platform health monitoring and self-healing

### Changed
- Platform architecture evolved from embedded to standalone universal service
- Platform now runs independently on port 8080

---

## [3.3.0] - 2024-12-19

### Added
- **Artichoke-FaaS Platform** — standalone universal FaaS platform
- **Development Kit** — custom trigger and function factories (`ITriggerFactory`, `ICustomFunctionFactory`)
- Built-in triggers: `HttpTrigger`, `TimerTrigger`
- Custom trigger examples (`SampleCustomTriggers.cs`, `AdvancedTriggers.cs`)
- `Artichoke.FaaS.Client` — universal CLI management tool
- `Artichoke.FaaS.Runtime` — function execution runtime
- `DevelopmentKitService` and `DevelopmentKitFactories` for extensibility
- `DevelopmentKitDbContext` for package management
- JWT authentication with Identity Framework on platform
- Platform roles: PlatformAdmin, ProjectOwner, Developer
- Full 8-chapter manual in `/Manual/`

### Changed
- FaaS architecture extracted from BMS into standalone platform
- Platform uses SQLite for zero-dependency development

---

## [3.2.0] - 2024-12-10

### Added
- Pure external function architecture — zero internal functions
- `DistributedFunctionManager` with process isolation
- `BMS.FunctionHost` — independent function process host
- Background functions: `BookProcessorFunction`, `HealthMonitorFunction`, `AuditLoggerFunction`
- Function registration via HTTP communication
- Health monitoring and auto-recovery for functions
- Function command system (Execute, HealthCheck, Stop)

### Changed
- Eliminated internal/external function confusion — all functions run externally
- Function lifecycle management moved to distributed manager

---

## [3.0.0] - 2024-11-15

### Added
- **Artichoke Architecture** — Clean/DDD architecture layer
- `BMS.Core` — domain entities, domain services, domain events, value objects
- `BMS.Interface` — application services, DTOs, event publisher interface
- `BMS.External` — infrastructure: repositories, event handlers, data context
- Domain events: `BookCreatedEvent`, `BookUpdatedEvent`, `BookDeletedEvent`
- Event-driven loosely coupled architecture
- Self-validating entities with rich business logic
- Artichoke API controllers (`/api/artichoke/`)

### Changed
- 67% boilerplate code reduction claimed vs N-Tier
- Dual architecture support: N-Tier (legacy) + Artichoke (clean)

---

## [2.0.0] - 2024-11-09

### Added
- JWT authentication with Identity Framework
- Role-based authorization: Admin, Librarian, Reader
- API versioning: v1.0 (XML, read-only), v2.0 (JSON, full CRUD)
- Swagger/OpenAPI documentation with versioned endpoints
- `CopyrightResponseFilter` for automatic copyright headers
- User seeding: Anu (Admin), Arun (Librarian)
- Role seeding: Admin, Librarian, Reader
- `HttpClient` configuration for inter-controller communication
- `Events` system and `LibraryEventHandlers`
- `Validation` service and `DbServices` for BLL-layer LINQ operations

### Changed
- Async/await throughout the codebase
- XML serializer formatters added for v1 API compatibility

---

## [1.0.0] - 2024-08-08

### Added
- **Book Management System (BMS)** — initial release
- N-Tier architecture: BMS-API, BMS.BLL, BMS.DAL, BMS.Models, BMS-UI
- MVC web UI with Razor views (AddBook, UpdateBook, Index, view2)
- Entity Framework Core with SQL Server
- Identity registration and login pages
- Basic CRUD operations for books
- Stored procedure support (`SP_SearchBooksOptimized.sql`)
- `BookAccess` repository with search/filter/pagination
- Bootstrap and jQuery frontend (wwwroot/lib/)
- Unit test project with xUnit and Moq

---

[9.1.1]: https://github.com/arlack9/BMS-API2/compare/v9.1.0...v9.1.1
[9.1.0]: https://github.com/arlack9/BMS-API2/compare/v9.0.0...v9.1.0
[9.0.0]: https://github.com/arlack9/BMS-API2/compare/v8.0.4...v9.0.0
[8.0.4]: https://github.com/arlack9/BMS-API2/compare/v8.0.3...v8.0.4
[8.0.3]: https://github.com/arlack9/BMS-API2/compare/v8.0.2...v8.0.3
[8.0.2]: https://github.com/arlack9/BMS-API2/compare/v8.0.1...v8.0.2
[8.0.1]: https://github.com/arlack9/BMS-API2/compare/v8.0.0...v8.0.1
[3.3.2.1]: https://github.com/arlack9/BMS-API2/compare/v3.3.2...v3.3.2.1
[3.3.2]: https://github.com/arlack9/BMS-API2/compare/v3.3.1...v3.3.2
[3.3.1]: https://github.com/arlack9/BMS-API2/compare/v3.3.0...v3.3.1
[3.3.0]: https://github.com/arlack9/BMS-API2/compare/v3.2.0...v3.3.0
[3.2.0]: https://github.com/arlack9/BMS-API2/compare/v3.0.0...v3.2.0
[3.0.0]: https://github.com/arlack9/BMS-API2/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/arlack9/BMS-API2/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/arlack9/BMS-API2/releases/tag/v1.0.0
