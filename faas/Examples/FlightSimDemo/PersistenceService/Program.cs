// PersistenceService — saves flight logs and crash reports.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var flightLogs = new List<FlightLog>();
var crashReports = new List<CrashReport>();
var lockObj = new object();

app.MapGet("/health", () => new { status = "alive", service = "persistence", at = DateTime.UtcNow, pid = Environment.ProcessId, flightsLogged = flightLogs.Count, crashes = crashReports.Count });

// Save a completed flight log (called on landing)
app.MapPost("/flight-log", (FlightLog log) =>
{
    lock (lockObj)
    {
        flightLogs.Add(log with { Id = Guid.NewGuid(), SavedAt = DateTime.UtcNow });
        Console.WriteLine($"[PERSIST] Saved flight log: {log.PlayerId} {log.Departure}→{log.Arrival} ({log.DurationMinutes} min, score {log.FinalScore})");
        return Results.Ok(new { saved = true, id = flightLogs[^1].Id, totalFlights = flightLogs.Count });
    }
});

// Save a crash report (called on crash)
app.MapPost("/crash-report", (CrashReport report) =>
{
    lock (lockObj)
    {
        crashReports.Add(report with { Id = Guid.NewGuid(), SavedAt = DateTime.UtcNow });
        Console.WriteLine($"[PERSIST] Saved crash report: {report.PlayerId} at {report.Location} ({report.Cause})");
        return Results.Ok(new { saved = true, id = crashReports[^1].Id, totalCrashes = crashReports.Count });
    }
});

app.MapGet("/flight-logs", () => Results.Ok(new { count = flightLogs.Count, logs = flightLogs.TakeLast(20) }));
app.MapGet("/crash-reports", () => Results.Ok(new { count = crashReports.Count, reports = crashReports.TakeLast(20) }));

app.MapGet("/", () => "PersistenceService v9.1.0 — /health, /flight-log, /crash-report, /flight-logs, /crash-reports");

app.Run();

public record FlightLog
{
    public Guid Id { get; init; }
    public string PlayerId { get; init; } = "";
    public string Departure { get; init; } = "";
    public string Arrival { get; init; } = "";
    public int DurationMinutes { get; init; }
    public long FinalScore { get; init; }
    public DateTime SavedAt { get; init; }
}

public record CrashReport
{
    public Guid Id { get; init; }
    public string PlayerId { get; init; } = "";
    public string Location { get; init; } = "";
    public string Cause { get; init; } = "";
    public DateTime SavedAt { get; init; }
}
