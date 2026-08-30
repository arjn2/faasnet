// WeatherService — simulates weather per region, snapshots on events.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var weatherByRegion = new Dictionary<string, Weather>();
var lockObj = new object();

// Generate initial weather for a region on demand
Weather GenerateWeather(string region)
{
    var rng = Random.Shared;
    return new Weather
    {
        Region = region,
        WindDirection = rng.Next(0, 360),
        WindSpeed = rng.Next(0, 25),
        Visibility = rng.Next(1, 10),
        CloudCeiling = rng.Next(1000, 10000),
        Temperature = rng.Next(-20, 35),
        Conditions = rng.Next(0, 4) switch
        {
            0 => "VFR",
            1 => "MVFR",
            2 => "IFR",
            _ => "LIFR"
        },
        UpdatedAt = DateTime.UtcNow
    };
}

app.MapGet("/health", () => new { status = "alive", service = "weather", at = DateTime.UtcNow, pid = Environment.ProcessId, regions = weatherByRegion.Count });

// Get/snapshot weather for a region (called on takeoff, region enter)
app.MapGet("/weather/{region}", (string region) =>
{
    lock (lockObj)
    {
        if (!weatherByRegion.TryGetValue(region, out var w))
        {
            w = GenerateWeather(region);
            weatherByRegion[region] = w;
            Console.WriteLine($"[WEATHER] Generated weather for '{region}': {w.Conditions}, wind {w.WindSpeed}kt at {w.WindDirection}°");
        }
        return Results.Ok(w);
    }
});

app.MapGet("/weather", () => Results.Ok(new { count = weatherByRegion.Count, regions = weatherByRegion }));

app.MapGet("/", () => "WeatherService v9.1.0 — /health, /weather, /weather/{region}");

app.Run();

public record Weather
{
    public string Region { get; init; } = "";
    public int WindDirection { get; init; }
    public int WindSpeed { get; init; }
    public int Visibility { get; init; }
    public int CloudCeiling { get; init; }
    public int Temperature { get; init; }
    public string Conditions { get; init; } = "";
    public DateTime UpdatedAt { get; init; }
}
