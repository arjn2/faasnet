// =============================================================================
// GameClient — a simulated flight sim client.
//
// In a real game, this would be your Unity/Unreal/Stride client running the per-frame
// game loop (physics, rendering, input). Here it's a console app that simulates a player
// flying through regions, emitting events that the supervisor routes to the game services.
//
// The pattern this demonstrates:
//   - Hot path (per-frame) stays in the game client
//   - Cold path (events) goes through the supervisor → game services
//   - Entities (AI traffic) spawn/despawn on demand via AircraftService
// =============================================================================

using System.Net.Http.Json;
using System.Text.Json;

var supervisorUrl = args.Length > 0 ? args[0] : "http://localhost:8080";
var playerId = args.Length > 1 ? args[1] : $"Pilot-{Random.Shared.Next(100, 999)}";

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
using var http = new HttpClient { BaseAddress = new Uri(supervisorUrl), Timeout = TimeSpan.FromSeconds(5) };

Console.WriteLine($"=== Flight Sim GameClient v9.1.0 ===");
Console.WriteLine($"Player: {playerId}");
Console.WriteLine($"Supervisor: {supervisorUrl}");
Console.WriteLine();

// Verify supervisor is up
try
{
    var resp = await http.GetAsync("/admin/status");
    if (!resp.IsSuccessStatusCode) throw new Exception($"HTTP {resp.StatusCode}");
    Console.WriteLine($"Connected to supervisor at {supervisorUrl}");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to connect to supervisor at {supervisorUrl}: {ex.Message}");
    Console.WriteLine("Make sure faas-supervisor is running (cd Artichoke.Microservices.Supervisor && dotnet run --urls http://localhost:8080)");
    return;
}

Console.WriteLine();
Console.WriteLine("=== Scenario: A short flight KLAX → KSFO through 2 regions ===");
Console.WriteLine();

// === 1. TAKEOFF from KLAX ===
Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {playerId} taking off from KLAX...");
await PostAsync("/api/atc/clearance", new { type = "takeoff", callsign = playerId, region = "KLAX", runway = "25L" });
await PostAsync("/api/scoring/multiplier/start", new { playerId, multiplier = 2 });
var weatherKLAX = await GetAsync<Weather>("/api/weather/KLAX");
if (weatherKLAX is not null)
    Console.WriteLine($"  Weather at KLAX: {weatherKLAX.Conditions}, wind {weatherKLAX.WindSpeed}kt at {weatherKLAX.WindDirection}°, {weatherKLAX.Temperature}°C");
await Task.Delay(1000);

// === 2. Enter region "ZLA" (Los Angeles Center) ===
Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {playerId} entered region ZLA (Los Angeles Center)...");
var spawnResult = await PostAndGetAsync<SpawnResult>("/api/aircraft/spawn", new { region = "ZLA", reason = "PlayerEnteredRegion" });
if (spawnResult is not null)
    Console.WriteLine($"  → Spawned {spawnResult.Spawned} AI aircraft in ZLA on demand!");
await PostAsync("/api/atc/clearance", new { type = "handoff", callsign = playerId, region = "ZLA" });
await PostAsync("/api/scoring/score", new { playerId, points = 100, reason = "region_enter:ZLA" });
await Task.Delay(1500);

// === 3. Enter region "ZOA" (Oakland Center) ===
Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {playerId} entered region ZOA (Oakland Center)...");
var spawn2 = await PostAndGetAsync<SpawnResult>("/api/aircraft/spawn", new { region = "ZOA", reason = "PlayerEnteredRegion" });
if (spawn2 is not null)
    Console.WriteLine($"  → Spawned {spawn2.Spawned} AI aircraft in ZOA on demand!");
await PostAsync("/api/atc/clearance", new { type = "handoff", callsign = playerId, region = "ZOA" });
await PostAsync("/api/scoring/score", new { playerId, points = 100, reason = "region_enter:ZOA" });
await Task.Delay(1500);

// === 4. Leave ZLA ===
Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {playerId} left region ZLA...");
var despawn = await PostAndGetAsync<DespawnResult>("/api/aircraft/despawn", new { region = "ZLA", reason = "PlayerLeftRegion" });
if (despawn is not null)
    Console.WriteLine($"  → Despawned {despawn.Despawned} AI aircraft from ZLA (no longer needed).");
await Task.Delay(1000);

// === 5. LAND at KSFO ===
Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {playerId} landing at KSFO...");
await PostAsync("/api/atc/clearance", new { type = "landing", callsign = playerId, region = "KSFO", runway = "28L" });
await PostAsync("/api/scoring/score", new { playerId, points = 500, reason = "landing:KSFO" });
await PostAsync("/api/persistence/flight-log", new { playerId, departure = "KLAX", arrival = "KSFO", durationMinutes = 65, finalScore = 1400 });
await Task.Delay(1000);

Console.WriteLine();
Console.WriteLine("=== Flight complete. Checking final state... ===");
Console.WriteLine();

// Show final state of each service
Console.WriteLine("--- Aircraft currently tracked (should only be ZOA — ZLA was despawned) ---");
var aircraft = await GetAsync<AircraftList>("/api/aircraft/aircraft");
if (aircraft is not null)
{
    Console.WriteLine($"  Total aircraft: {aircraft.Total}, regions active: {aircraft.Regions}");
    foreach (var region in aircraft.Aircraft.GroupBy(a => a.Region))
        Console.WriteLine($"    {region.Key}: {region.Count()} aircraft");
}

Console.WriteLine();
Console.WriteLine("--- ATC clearances issued ---");
var clearances = await GetAsync<ClearanceList>("/api/atc/clearances");
if (clearances is not null)
{
    Console.WriteLine($"  Total: {clearances.Count}");
    foreach (var c in clearances.Recent)
        Console.WriteLine($"    [{c.IssuedAt:HH:mm:ss}] {c.Message}");
}

Console.WriteLine();
Console.WriteLine("--- Player score ---");
var score = await GetAsync<ScoreResult>($"/api/scoring/scores/{playerId}");
if (score is not null)
{
    Console.WriteLine($"  Total: {score.Score.TotalScore}");
    Console.WriteLine($"  Flights: {score.Score.FlightsCompleted}");
    if (score.Achievements is not null && score.Achievements.Count > 0)
        foreach (var a in score.Achievements)
            Console.WriteLine($"  Achievement: {a.Description}");
}

Console.WriteLine();
Console.WriteLine("--- Flight logs ---");
var logs = await GetAsync<FlightLogList>("/api/persistence/flight-logs");
if (logs is not null)
{
    Console.WriteLine($"  Total: {logs.Count}");
    foreach (var l in logs.Logs)
        Console.WriteLine($"    [{l.SavedAt:HH:mm:ss}] {l.PlayerId} {l.Departure}→{l.Arrival} ({l.DurationMinutes} min, score {l.FinalScore})");
}

Console.WriteLine();
Console.WriteLine("=== Demo complete ===");
Console.WriteLine();
Console.WriteLine("Key observations:");
Console.WriteLine("  1. AI traffic was spawned ON DEMAND when you entered each region");
Console.WriteLine("  2. Traffic was despawned when you left (no wasted memory)");
Console.WriteLine("  3. ATC clearances, weather, scoring, persistence all fired as side-effects");
Console.WriteLine("  4. Each service is a separate process — kill one, supervisor restarts it");
Console.WriteLine("  5. The game client never knew where any service lived — just talked to :8080");

// Helpers
async Task PostAsync(string path, object body)
{
    try
    {
        var resp = await http.PostAsJsonAsync(path, body);
        if (!resp.IsSuccessStatusCode)
            Console.WriteLine($"  ! {path} → {resp.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ! {path} failed: {ex.Message}");
    }
}

async Task<T?> GetAsync<T>(string path)
{
    try
    {
        var resp = await http.GetAsync(path);
        if (!resp.IsSuccessStatusCode) return default;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, jsonOpts);
    }
    catch { return default; }
}

async Task<T?> PostAndGetAsync<T>(string path, object body)
{
    try
    {
        var resp = await http.PostAsJsonAsync(path, body);
        if (!resp.IsSuccessStatusCode) return default;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, jsonOpts);
    }
    catch { return default; }
}

// Types for JSON deserialization (PascalCase property names, PropertyNameCaseInsensitive=true above)
record Weather(string Region, int WindDirection, int WindSpeed, int Visibility, int CloudCeiling, int Temperature, string Conditions, DateTime UpdatedAt);
record SpawnResult(string Region, int Spawned);
record DespawnResult(string Region, int Despawned);
record AircraftList(int Total, int Regions, List<Aircraft> Aircraft);
record Aircraft(string Id, string Region, string Callsign, int Altitude, int Heading, int Speed, DateTime SpawnedAt, string SpawnedBy);
record ClearanceList(int Count, List<Clearance> Recent);
record Clearance(Guid Id, string Type, string Callsign, string Region, string Message, DateTime IssuedAt);
record ScoreResult(PlayerScore Score, List<Achievement> Achievements);
record PlayerScore(string PlayerId, long TotalScore, int ActiveMultiplier, DateTime? MultiplierStartedAt, int FlightsCompleted);
record Achievement(string PlayerId, string Type, string Description, DateTime EarnedAt);
record FlightLogList(int Count, List<FlightLog> Logs);
record FlightLog(Guid Id, string PlayerId, string Departure, string Arrival, int DurationMinutes, long FinalScore, DateTime SavedAt);

