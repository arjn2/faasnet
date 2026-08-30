using Artichoke.FaaS.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BMS_API.Controllers.v4;

// ============================================================================
// v8.0.6 FunctionsController — uses the new IFunctionHost.
//
// The /api/v4/functions/{name}/execute endpoint is the "slow path": host.ExecuteAsync
// does Dictionary lookup + (optional) logging/timing/error-capture. We use
// FunctionExecutionOptions.FullObservability here because HTTP callers want to see
// timing in the response.
//
// For high-throughput callers (like EventPublisher), they'd use IFunctionInvoker directly
// — see BMS.External/Events/EventPublisher.cs which now publishes to IDomainEventBus.
// ============================================================================

[ApiController]
[Route("api/v4/functions")]
[Authorize(Roles = "Admin")]
public class FunctionsController : ControllerBase
{
    private readonly IFunctionHost _functionHost;
    private readonly ILogger<FunctionsController> _logger;

    public FunctionsController(IFunctionHost functionHost, ILogger<FunctionsController> logger)
    {
        _functionHost = functionHost;
        _logger = logger;
    }

    /// <summary>List all registered functions.</summary>
    [HttpGet]
    public ActionResult<object> List()
    {
        var functions = _functionHost.List();
        return Ok(new
        {
            mode = "v8.0.6 — IFunctionHost (fast path + slow path + triggers)",
            count = functions.Count,
            functions
        });
    }

    /// <summary>Execute a function by type. Slow path (lookup + observability).</summary>
    [HttpPost("{name}/execute")]
    public async Task<ActionResult<object>> Execute(string name, [FromBody] object? input = null)
    {
        var adminUser = User.Identity?.Name ?? "anonymous";
        _logger.LogInformation("Admin {User} executing function '{Function}'", adminUser, name);

        var result = await _functionHost.ExecuteAsync(
            name,
            input: input,
            options: new FunctionExecutionOptions
            {
                EnableLogging = true,
                EnableTiming = true,
                ProjectNamespace = "BMS",
                ExecutionSource = "HTTP/v4"
            });

        return Ok(new
        {
            functionType = name,
            triggeredBy = adminUser,
            isSuccess = result.IsSuccess,
            message = result.Message,
            durationMs = result.Duration.TotalMilliseconds,
            output = result.Output,
            error = result.ErrorDetails
        });
    }

    /// <summary>Check whether a function is registered.</summary>
    [HttpGet("{name}/exists")]
    public ActionResult<object> Exists(string name)
        => Ok(new { functionType = name, registered = _functionHost.IsRegistered(name) });
}
