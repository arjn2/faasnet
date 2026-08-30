// =============================================================================
// OrderService — another standalone microservice process.
//
// Spawns on port 5002. Gateway routes /api/orders/* here.
//
// In a real app this would have a database, message bus integration, etc.
// For demo purposes it keeps orders in-memory and "publishes" an audit event
// by calling the audit service (via the supervisor's gateway).
// =============================================================================

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpClient("audit", c =>
{
    // Talk to the audit service via the supervisor gateway (port 8080)
    c.BaseAddress = new Uri("http://localhost:8080/api/audit/");
    c.Timeout = TimeSpan.FromSeconds(5);
});
var app = builder.Build();

app.MapGet("/health", () => new
{
    status = "alive",
    service = "orders",
    at = DateTime.UtcNow,
    pid = Environment.ProcessId
});

// In-memory order store (demo only — in production use EF Core + a real DB)
var orders = new List<Order>();
var nextId = 1;

// Create order — also fires an audit event via the supervisor gateway
app.MapPost("/orders", async (CreateOrderRequest req, IHttpClientFactory httpFactory) =>
{
    var order = new Order
    {
        Id = nextId++,
        CustomerName = req.CustomerName,
        TotalAmount = req.TotalAmount,
        CreatedAt = DateTime.UtcNow
    };
    orders.Add(order);

    // Cross-service call: OrderService → supervisor gateway → AuditService
    // This is what "microservices" means in practice — services calling services.
    try
    {
        var client = httpFactory.CreateClient("audit");
        var auditEntry = new
        {
            eventType = "OrderCreated",
            user = "system",
            entityType = "Order",
            entityId = order.Id.ToString()
        };
        var resp = await client.PostAsJsonAsync("/log", auditEntry);
        Console.WriteLine($"[ORDERS] Audit call result: {resp.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ORDERS] Audit call failed: {ex.Message}");
    }

    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders", () => orders);
app.MapGet("/orders/{id}", (int id) =>
{
    var order = orders.FirstOrDefault(o => o.Id == id);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

app.MapGet("/", () => "OrderService v9.0.0 — see /health, /orders");

app.Run();

public record Order
{
    public int Id { get; init; }
    public string CustomerName { get; init; } = "";
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CreateOrderRequest(string CustomerName, decimal TotalAmount);
