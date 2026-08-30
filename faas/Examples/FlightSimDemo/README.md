# Artichoke-FaaS v9.1.0 — Flight Sim Demo (Game-FaaS Architecture)

> **The pitch:** A flight simulator where game objects (AI aircraft, weather, ATC clearances, scores, flight logs) are managed by separate microservices. Entities spawn on demand when a player enters a region, despawn when they leave. No Docker, no Kubernetes, no cloud — just .NET 9 processes.

## What this demo shows

```
                  ┌──────────────────────────┐
                  │  GameClient (simulated)   │
                  │  Per-frame loop stays     │
                  │  in-process; events go    │
                  │  through the gateway.     │
                  └──────────────┬───────────┘
                                 │ HTTP (port 8080)
                                 ▼
                  ┌──────────────────────────┐
                  │  faas-supervisor :8080    │
                  │  Gateway + restart        │
                  └──────────────┬───────────┘
                                 │ routes /api/{service}/*
       ┌───────────┬─────────────┼─────────────┬───────────┐
       ▼           ▼             ▼             ▼           ▼
  ┌────────┐ ┌─────────┐  ┌──────────┐  ┌─────────┐  ┌─────────┐
  │aircraft│ │  atc    │  │ weather  │  │ scoring │  │ persist │
  │ :5101  │ │ :5102   │  │ :5103    │  │ :5104   │  │ :5105   │
  └────────┘ └─────────┘  └──────────┘  └─────────┘  └─────────┘
   spawns   issues       simulates     tracks        saves
   AI on    clearances   wind/clouds   scores +      flight logs
   region   on event     on demand     achievements  on landing
   enter
```

## The key insight: hot path vs cold path

A flight sim's per-frame loop runs in 16.67ms (60 FPS). Cross-service HTTP calls take ~5ms. So you can do **maybe 1-2 cross-service calls per frame** — not viable for physics or rendering.

**But games have a TON of non-per-frame work that's perfect for FaaS:**

| Game work | Frequency | FaaS-friendly? |
|---|---|---|
| Flight model integration | 60 Hz | ❌ in-process |
| Rendering | 60 Hz | ❌ in-process |
| Input handling | 60 Hz | ❌ in-process |
| AI traffic spawning/despawning | on event | ✅ |
| ATC clearance issuance | on event | ✅ |
| Weather simulation | on demand | ✅ |
| Player took off / landed / crashed | on event | ✅ |
| Region enter/exit → load/unload entities | on event | ✅ |
| Scoring + achievements | on event | ✅ |
| Persistence (save flight log) | on event | ✅ |
| Multiplayer position sync | 10-20 Hz | ✅ (batched) |

## The on-demand entity pattern (the cool part)

In a flight sim, you don't pre-spawn 10,000 AI aircraft globally. You spawn them **when a player enters a region**, despawn **when they leave**. This is exactly FaaS — functions invoked on event triggers, creating transient state.

When the GameClient emits `PlayerEnteredRegion`:

```
GameClient → POST /api/aircraft/spawn { region: "ZLA", reason: "PlayerEnteredRegion" }
          → AircraftService spawns 3-5 random AI aircraft for ZLA
          → Returns them to the client (which renders them)

GameClient → POST /api/atc/clearance { type: "handoff", callsign: "CaptainAero", region: "ZLA" }
          → AtcService issues "CaptainAero, contact ZLA Center on 124.7."

GameClient → GET /api/weather/ZLA
          → WeatherService generates (or returns cached) weather for ZLA

GameClient → POST /api/scoring/score { playerId, points: 100, reason: "region_enter:ZLA" }
          → ScoringService adds 100 × multiplier to player's score
```

When the player leaves:

```
GameClient → POST /api/aircraft/despawn { region: "ZLA", reason: "PlayerLeftRegion" }
          → AircraftService clears all AI aircraft for ZLA
          → (Memory freed — those entities no longer exist anywhere.)
```

## What's in this demo

### 5 game services (each ~50-100 lines)

| Service | Port | Role |
|---|---|---|
| `AircraftService` | 5101 | Spawns/despawns AI aircraft per region. The on-demand entity lifecycle. |
| `AtcService` | 5102 | Issues ATC clearances (takeoff, landing, handoff, emergency). |
| `WeatherService` | 5103 | Generates weather per region (wind, visibility, conditions). |
| `ScoringService` | 5104 | Tracks player scores, multipliers, achievements. |
| `PersistenceService` | 5105 | Saves flight logs and crash reports. |

### 1 game client (simulated)

`GameClient` (~200 lines) simulates a player flying KLAX → KSFO through 2 regions (ZLA, ZOA). It emits events at the right moments:

1. **Takeoff from KLAX** → calls ATC (takeoff clearance), scoring (start multiplier), weather (snapshot)
2. **Enter ZLA** → spawns AI traffic, ATC handoff, weather snapshot, +100 points
3. **Enter ZOA** → spawns AI traffic, ATC handoff, +100 points
4. **Leave ZLA** → despawns AI traffic (no longer needed)
5. **Land at KSFO** → ATC landing clearance, +500 points, save flight log

## How to run it

```bash
# 1. Build everything
cd faas-3.3.2.1
dotnet build Artichoke-FaaS-Platform.sln --configuration Release

# 2. Copy services.json into supervisor bin (it lists all 7 services)
cp Artichoke.Microservices.Supervisor/services.json \
   Artichoke.Microservices.Supervisor/bin/Release/net9.0/services.json

# 3. Start the supervisor (spawns all 7 services)
cd Artichoke.Microservices.Supervisor
dotnet run --configuration Release --urls http://localhost:8080
```

Wait for "All services spawned" in the log. Then in another terminal:

```bash
# 4. Run the flight sim GameClient
cd Examples/FlightSimDemo/GameClient
dotnet run --configuration Release -- http://localhost:8080 CaptainAero
```

You'll see:

```
=== Flight Sim GameClient v9.1.0 ===
Player: CaptainAero
Supervisor: http://localhost:8080

=== Scenario: A short flight KLAX → KSFO through 2 regions ===

[04:34:52] CaptainAero taking off from KLAX...
[04:34:53] CaptainAero entered region ZLA (Los Angeles Center)...
  → Spawned 4 AI aircraft in ZLA on demand!
[04:34:55] CaptainAero entered region ZOA (Oakland Center)...
  → Spawned 3 AI aircraft in ZOA on demand!
[04:34:56] CaptainAero left region ZLA...
  → Despawned 4 AI aircraft from ZLA (no longer needed).
[04:34:57] CaptainAero landing at KSFO...

=== Flight complete. Checking final state... ===

--- Aircraft currently tracked ---
  Total aircraft: 7, regions active: 1
    ZOA: 7 aircraft

--- ATC clearances issued ---
  Total: 8
    [04:34:52] CaptainAero, cleared for takeoff runway 25L, wind 270 at 10.
    [04:34:53] CaptainAero, contact ZLA Center on 124.7.
    [04:34:55] CaptainAero, contact ZOA Center on 124.7.
    [04:34:57] CaptainAero, cleared to land runway 28L.

--- Player score ---
  Total: 2800
  Flights: 6

--- Flight logs ---
  Total: 2
    [04:34:44] CaptainAero KLAX→KSFO (65 min, score 1400)
    [04:34:57] CaptainAero KLAX→KSFO (65 min, score 1400)
```

## Demonstrating auto-restart

Kill one of the service processes:

```bash
# Find and kill the AircraftService
kill -9 $(ss -tlnp | grep ':5101' | grep -oP 'pid=\K[0-9]+' | head -1)
```

The supervisor will:
1. Detect the exit within seconds (Process.Exited event)
2. Restart the service with exponential backoff
3. Health-check it back to "alive"

Check the supervisor log:

```
warn: Service 'aircraft' #3 (PID 9123) exited with code 137
warn: Service 'aircraft' #3 restarting (attempt 1, backoff 2s)
info:  Service 'aircraft' #3 started (PID 9456)
```

## The architecture pattern (generalizable beyond flight sims)

```
┌──────────────────────────────────────────────────────────┐
│  Game Client (Unity/Unreal/Stride/console)               │
│                                                          │
│  ┌──────────────────────┐  ┌─────────────────────────┐  │
│  │  Per-frame loop      │  │  Event dispatcher       │  │
│  │  - Physics           │  │  - Player took off      │  │
│  │  - Rendering         │  │  - Region entered       │  │
│  │  - Input             │  │  - Player crashed       │  │
│  │  - Local AI          │  │  - etc.                 │  │
│  │  (60 FPS, in-proc)   │  │  (on event, → gateway)  │  │
│  └──────────────────────┘  └────────┬────────────────┘  │
└──────────────────────────────────────┼───────────────────┘
                                       │ HTTP
                                       ▼
                          ┌────────────────────────┐
                          │  faas-supervisor :8080  │
                          └────────┬───────────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              ▼                    ▼                    ▼
         [Entity services]  [Side-effect services]  [Persistence]
         - aircraft spawn   - ATC clearances        - flight logs
         - missions         - scoring               - replay save
         - buildings        - achievements          - telemetry
         - NPCs             - weather               - audit
         (spawn on demand,  (event-driven)          (event-driven)
          despawn on leave)
```

**This pattern applies to:**
- **Flight sims** (this demo) — regions spawn AI traffic
- **MMOs** — zones spawn NPCs, quests, dynamic events
- **Racing games** — tracks spawn AI opponents, weather
- **Open-world games** — chunks spawn fauna, encounters, loot
- **Strategy games** — maps spawn resources, AI factions
- **City builders** — districts spawn citizens, traffic, services

## What v9.1.0 does NOT do (yet)

- **Multiplayer sync**: services track per-player state but don't broadcast to other players. You'd add a `MultiplayerService` that maintains a player registry and broadcasts position updates.
- **WebSockets / SignalR**: HTTP request/response only. Real-time push (for live ATC chatter, weather updates) would need SignalR.
- **Persistence to disk**: services keep state in-memory. A real game would wire them to Postgres/Redis.
- **Hot reload**: dropping a new `AircraftService.dll` requires a supervisor restart.

## Comparison to traditional game backends

| | Traditional (custom server) | Agones (Kubernetes) | **Artichoke v9.1.0** |
|---|---|---|---|
| Infrastructure | Bare metal / VM | Kubernetes cluster | Just .NET 9 |
| Game server model | Monolithic | Containerized per match | Process per service |
| Auto-restart | DIY | K8s handles it | Built-in |
| Service discovery | Hardcoded | Kube-DNS | Supervisor `/admin/status` |
| Hot path / cold path split | Manual | Manual | Built into the pattern |
| Entity lifecycle | Manual | Manual | FaaS pattern (spawn/despawn on event) |
| Cost | infra | K8s cluster (expensive) | infra |

## License

MIT.
