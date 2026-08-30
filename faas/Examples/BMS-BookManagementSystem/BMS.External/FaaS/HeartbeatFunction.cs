using System.Text.Json;
using Artichoke.FaaS.Core.Base;
using Artichoke.FaaS.Core.Interfaces;

namespace BMS.External.FaaS;

/// <summary>
/// Trivial function used for the periodic-timer-trigger benchmark. Returns "alive" + timestamp + pid.
/// </summary>
public class HeartbeatFunction : CustomFunctionBase
{
    public override string FunctionType => "BMS.Heartbeat";

    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        await OnInitializeAsync();
        return FunctionExecutionResult.Success(
            new
            {
                status = "alive",
                at = DateTime.UtcNow,
                pid = Environment.ProcessId,
                threadId = Environment.CurrentManagedThreadId,
                gcMemoryMb = GC.GetTotalMemory(false) / (1024 * 1024)
            },
            "Heartbeat",
            TimeSpan.Zero);
    }

    public override Task<ValidationResult> ValidateInputAsync(object input)
        => Task.FromResult(ValidationResult.Success());

    public override JsonDocument GetInputSchema()
        => CreateSchema(new { type = "object", properties = new { } });

    public override JsonDocument GetOutputSchema()
        => CreateSchema(new
        {
            type = "object",
            properties = new
            {
                status = new { type = "string" },
                at = new { type = "string", format = "date-time" },
                pid = new { type = "integer" },
                threadId = new { type = "integer" },
                gcMemoryMb = new { type = "number" }
            }
        });
}
