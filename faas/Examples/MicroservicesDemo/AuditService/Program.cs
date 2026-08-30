// =============================================================================
// AuditService — a standalone microservice process.
//
// In v8.0.6 this was a CustomFunctionBase running inside BMS-API's process.
// In v9.0.0 it's its own process. The supervisor spawns it via
// `dotnet exec AuditService.dll --urls http://localhost:5001` and pings
// /health every 5s.
//
// This is the "microservices without Docker" pitch: each service is a
// regular .NET 9 ASP.NET Core app, ~80 lines, deployable independently.
// =============================================================================

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
var app = builder.Build();

// Heartbeat endpoint — the supervisor pings this every 5s
app.MapGet("/health", () => new
{
    status = "alive",
    service = "audit",
    at = DateTime.UtcNow,
    pid = Environment.ProcessId
});

// The actual business endpoint — clients hit /api/audit/log (gateway routes here)
app.MapPost("/log", (AuditEntry entry) =>
{
    Console.WriteLine($"[AUDIT] {DateTime.UtcNow:O} {entry.EventType} user={entry.User} entity={entry.EntityType}/{entry.EntityId}");
    return Results.Ok(new
    {
        auditId = Guid.NewGuid(),
        recordedAt = DateTime.UtcNow,
        entry
    });
});

// List recent audit entries (just returns mock data for demo)
app.MapGet("/entries", () => new[]
{
    new { auditId = Guid.NewGuid(), eventType = "UserLogin", user = "alice", at = DateTime.UtcNow.AddMinutes(-5) },
    new { auditId = Guid.NewGuid(), eventType = "OrderCreated", user = "bob", at = DateTime.UtcNow.AddMinutes(-2) }
});

app.MapGet("/", () => "AuditService v9.0.0 — see /health, /log, /entries");

app.Run();

public record AuditEntry(string EventType, string User, string EntityType, string EntityId);
