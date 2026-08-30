// =============================================================================
// AircraftService — the on-demand entity spawner.
//
// When a player enters a region, this service spawns 3-5 AI aircraft for that region.
// When the player leaves, it despawns them. This is the "FaaS = entity lifecycle" pattern.
//
// In a real flight sim, "spawn" would mean: notify the game client to render the new aircraft,
// start a physics+AI simulation for them, register them with ATC. Here we just track them
// in-memory and expose /aircraft/region/{name} for inspection.
// =============================================================================

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();

// In-memory store: region → list of AI aircraft
var aircraftByRegion = new Dictionary<string, List<Aircraft>>();
var lockObj = new object();

// Health check — supervisor pings this every 5s
app.MapGet("/health", () => new
{
    status = "alive",
    service = "aircraft",
    at = DateTime.UtcNow,
    pid = Environment.ProcessId,
    regionsActive = aircraftByRegion.Count,
    totalAircraft = aircraftByRegion.Values.SelectMany(a => a).Count()
});

// POST /spawn — spawn AI traffic for a region (called when PlayerEnteredRegion fires)
app.MapPost("/spawn", (SpawnRequest req) =>
{
    lock (lockObj)
    {
        if (!aircraftByRegion.ContainsKey(req.Region))
            aircraftByRegion[req.Region] = new List<Aircraft>();

        var rng = Random.Shared;
        var count = rng.Next(3, 6); // 3-5 aircraft per region
        var newAircraft = new List<Aircraft>();
        for (int i = 0; i < count; i++)
        {
            var ac = new Aircraft
            {
                Id = $"AI-{req.Region}-{rng.Next(1000, 9999)}",
                Region = req.Region,
                Callsign = $"AAL{rng.Next(100, 999)}",
                Altitude = rng.Next(10000, 35000),
                Heading = rng.Next(0, 360),
                Speed = rng.Next(200, 450),
                SpawnedAt = DateTime.UtcNow,
                SpawnedBy = req.Reason
            };
            aircraftByRegion[req.Region].Add(ac);
            newAircraft.Add(ac);
        }

        Console.WriteLine($"[AIRCRAFT] Spawned {newAircraft.Count} AI aircraft in region '{req.Region}' (reason: {req.Reason})");
        return Results.Ok(new
        {
            region = req.Region,
            spawned = newAircraft.Count,
            aircraft = newAircraft
        });
    }
});

// POST /despawn — despawn all AI traffic for a region (called when PlayerLeftRegion fires)
app.MapPost("/despawn", (DespawnRequest req) =>
{
    lock (lockObj)
    {
        if (!aircraftByRegion.TryGetValue(req.Region, out var list))
        {
            return Results.Ok(new { region = req.Region, despawned = 0, reason = "no aircraft in region" });
        }

        var count = list.Count;
        list.Clear();
        aircraftByRegion.Remove(req.Region);

        Console.WriteLine($"[AIRCRAFT] Despawned {count} AI aircraft from region '{req.Region}' (reason: {req.Reason})");
        return Results.Ok(new
        {
            region = req.Region,
            despawned = count,
            reason = req.Reason
        });
    }
});

// GET /aircraft — list all AI aircraft currently tracked
app.MapGet("/aircraft", () =>
{
    lock (lockObj)
    {
        var all = aircraftByRegion.SelectMany(kv => kv.Value).ToList();
        return Results.Ok(new
        {
            total = all.Count,
            regions = aircraftByRegion.Count,
            aircraft = all
        });
    }
});

// GET /aircraft/region/{name} — list AI aircraft in a specific region
app.MapGet("/aircraft/region/{name}", (string name) =>
{
    lock (lockObj)
    {
        if (!aircraftByRegion.TryGetValue(name, out var list))
            return Results.NotFound(new { region = name, error = "no aircraft in region" });
        return Results.Ok(new { region = name, count = list.Count, aircraft = list });
    }
});

app.MapGet("/", () => "AircraftService v9.1.0 — /health, /spawn, /despawn, /aircraft");

app.Run();

public record Aircraft
{
    public string Id { get; init; } = "";
    public string Region { get; init; } = "";
    public string Callsign { get; init; } = "";
    public int Altitude { get; init; }
    public int Heading { get; init; }
    public int Speed { get; init; }
    public DateTime SpawnedAt { get; init; }
    public string SpawnedBy { get; init; } = "";
}

public record SpawnRequest(string Region, string Reason);
public record DespawnRequest(string Region, string Reason);
