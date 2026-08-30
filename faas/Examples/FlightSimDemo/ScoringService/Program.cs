// ScoringService — tracks player scores, multipliers, achievements.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var scoresByPlayer = new Dictionary<string, PlayerScore>();
var achievements = new List<Achievement>();
var lockObj = new object();

app.MapGet("/health", () => new { status = "alive", service = "scoring", at = DateTime.UtcNow, pid = Environment.ProcessId, playersTracked = scoresByPlayer.Count });

// Start a flight multiplier (called on takeoff)
app.MapPost("/multiplier/start", (MultiplierStart req) =>
{
    lock (lockObj)
    {
        if (!scoresByPlayer.ContainsKey(req.PlayerId))
            scoresByPlayer[req.PlayerId] = new PlayerScore { PlayerId = req.PlayerId, TotalScore = 0 };

        scoresByPlayer[req.PlayerId].ActiveMultiplier = req.Multiplier;
        scoresByPlayer[req.PlayerId].MultiplierStartedAt = DateTime.UtcNow;
        Console.WriteLine($"[SCORING] {req.PlayerId} started multiplier x{req.Multiplier}");
        return Results.Ok(new { player = req.PlayerId, multiplier = req.Multiplier, startedAt = scoresByPlayer[req.PlayerId].MultiplierStartedAt });
    }
});

// Add score (called on region enter, landing, etc.)
app.MapPost("/score", (ScoreAdd req) =>
{
    lock (lockObj)
    {
        if (!scoresByPlayer.ContainsKey(req.PlayerId))
            scoresByPlayer[req.PlayerId] = new PlayerScore { PlayerId = req.PlayerId, TotalScore = 0 };

        var ps = scoresByPlayer[req.PlayerId];
        var earned = req.Points * ps.ActiveMultiplier;
        ps.TotalScore += earned;
        ps.FlightsCompleted++;

        // Achievement check
        if (ps.TotalScore >= 10000 && !achievements.Any(a => a.PlayerId == req.PlayerId && a.Type == "10000"))
        {
            achievements.Add(new Achievement { PlayerId = req.PlayerId, Type = "10000", EarnedAt = DateTime.UtcNow, Description = "Reached 10,000 points" });
            Console.WriteLine($"[SCORING] {req.PlayerId} earned achievement: 10000 points!");
        }

        Console.WriteLine($"[SCORING] {req.PlayerId} +{earned} ({req.Points} × {ps.ActiveMultiplier}) → total {ps.TotalScore}");
        return Results.Ok(new { player = req.PlayerId, earned, total = ps.TotalScore, multiplier = ps.ActiveMultiplier });
    }
});

// Penalty (called on crash)
app.MapPost("/penalty", (PenaltyRequest req) =>
{
    lock (lockObj)
    {
        if (!scoresByPlayer.ContainsKey(req.PlayerId))
            scoresByPlayer[req.PlayerId] = new PlayerScore { PlayerId = req.PlayerId, TotalScore = 0 };

        var ps = scoresByPlayer[req.PlayerId];
        ps.TotalScore -= req.Penalty;
        ps.ActiveMultiplier = 1;
        Console.WriteLine($"[SCORING] {req.PlayerId} -{req.Penalty} (penalty: {req.Reason}) → total {ps.TotalScore}");
        return Results.Ok(new { player = req.PlayerId, penalty = req.Penalty, total = ps.TotalScore });
    }
});

app.MapGet("/scores", () => Results.Ok(new { count = scoresByPlayer.Count, scores = scoresByPlayer.Values, achievements }));
app.MapGet("/scores/{playerId}", (string playerId) =>
{
    lock (lockObj)
    {
        if (!scoresByPlayer.TryGetValue(playerId, out var ps))
            return Results.NotFound();
        return Results.Ok(new { score = ps, achievements = achievements.Where(a => a.PlayerId == playerId) });
    }
});

app.MapGet("/", () => "ScoringService v9.1.0 — /health, /multiplier/start, /score, /penalty, /scores");

app.Run();

public class PlayerScore
{
    public string PlayerId { get; init; } = "";
    public long TotalScore { get; set; }
    public int ActiveMultiplier { get; set; } = 1;
    public DateTime? MultiplierStartedAt { get; set; }
    public int FlightsCompleted { get; set; }
}

public record Achievement
{
    public string PlayerId { get; init; } = "";
    public string Type { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime EarnedAt { get; init; }
}

public record MultiplierStart(string PlayerId, int Multiplier);
public record ScoreAdd(string PlayerId, int Points, string Reason);
public record PenaltyRequest(string PlayerId, int Penalty, string Reason);
