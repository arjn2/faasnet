// ATCService — Air Traffic Control. Issues clearances on events.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var clearanceLog = new List<Clearance>();
var lockObj = new object();

app.MapGet("/health", () => new { status = "alive", service = "atc", at = DateTime.UtcNow, pid = Environment.ProcessId, clearances = clearanceLog.Count });

// Issue a clearance (called on takeoff, landing, region enter, etc.)
app.MapPost("/clearance", (ClearanceRequest req) =>
{
    var clearance = new Clearance
    {
        Id = Guid.NewGuid(),
        Type = req.Type,
        Callsign = req.Callsign,
        Region = req.Region,
        Message = req.Type switch
        {
            "takeoff" => $"{req.Callsign}, cleared for takeoff runway {req.Runway ?? "27L"}, wind 270 at 10.",
            "landing" => $"{req.Callsign}, cleared to land runway {req.Runway ?? "27L"}.",
            "handoff" => $"{req.Callsign}, contact {req.Region} Center on 124.7.",
            "emergency" => $"{req.Callsign}, emergency declared. All other aircraft hold position.",
            _ => $"{req.Callsign}, {req.Type} acknowledged."
        },
        IssuedAt = DateTime.UtcNow
    };

    lock (lockObj) { clearanceLog.Add(clearance); }
    Console.WriteLine($"[ATC] {clearance.Message}");
    return Results.Ok(clearance);
});

app.MapGet("/clearances", () => Results.Ok(new { count = clearanceLog.Count, recent = clearanceLog.TakeLast(20) }));

app.MapGet("/", () => "AtcService v9.1.0 — /health, /clearance, /clearances");

app.Run();

public record Clearance
{
    public Guid Id { get; init; }
    public string Type { get; init; } = "";
    public string Callsign { get; init; } = "";
    public string Region { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTime IssuedAt { get; init; }
}

public record ClearanceRequest
{
    public string Type { get; init; } = "";
    public string Callsign { get; init; } = "";
    public string Region { get; init; } = "";
    public string? Runway { get; init; }
}
