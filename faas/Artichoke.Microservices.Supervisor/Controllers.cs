using Microsoft.AspNetCore.Mvc;

namespace Artichoke.Microservices.Supervisor;

/// <summary>
/// Admin endpoints for inspecting the supervisor itself.
/// </summary>
[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly ServiceManager _manager;

    public AdminController(ServiceManager manager)
    {
        _manager = manager;
    }

    /// <summary>List all running service instances.</summary>
    [HttpGet("status")]
    public ActionResult<object> Status()
    {
        var instances = _manager.GetAllInstances().Select(p => p.ToStatus()).ToList();
        return Ok(new
        {
            supervisor = "Artichoke.Microservices.Supervisor v9.0.0",
            startedAt = DateTime.UtcNow,
            services = instances
        });
    }

    /// <summary>List service names (for the gateway router).</summary>
    [HttpGet("services")]
    public ActionResult<object> Services()
    {
        var instances = _manager.GetAllInstances();
        return Ok(instances.GroupBy(p => p.Name).Select(g => new
        {
            name = g.Key,
            instances = g.Count(),
            running = g.Count(p => p.IsRunning)
        }));
    }
}

/// <summary>
/// The HTTP gateway. Any request to /api/{serviceName}/* gets proxied to that service's port.
/// This is the single external entry point — clients never talk to child services directly.
/// </summary>
[ApiController]
[Route("api/{serviceName}")]
public class GatewayController : ControllerBase
{
    private readonly ServiceManager _manager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GatewayController> _logger;

    public GatewayController(ServiceManager manager, IHttpClientFactory httpClientFactory, ILogger<GatewayController> logger)
    {
        _manager = manager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("{**path}")]
    public Task<IActionResult> Get(string serviceName, string? path)
        => ProxyAsync(serviceName, path, HttpMethod.Get);

    [HttpPost("{**path}")]
    public Task<IActionResult> Post(string serviceName, string? path)
        => ProxyAsync(serviceName, path, HttpMethod.Post);

    [HttpPut("{**path}")]
    public Task<IActionResult> Put(string serviceName, string? path)
        => ProxyAsync(serviceName, path, HttpMethod.Put);

    [HttpDelete("{**path}")]
    public Task<IActionResult> Delete(string serviceName, string? path)
        => ProxyAsync(serviceName, path, HttpMethod.Delete);

    [HttpPatch("{**path}")]
    public Task<IActionResult> Patch(string serviceName, string? path)
        => ProxyAsync(serviceName, path, HttpMethod.Patch);

    private async Task<IActionResult> ProxyAsync(string serviceName, string? path, HttpMethod method)
    {
        var instance = _manager.GetInstance(serviceName);
        if (instance is null)
        {
            return NotFound(new
            {
                error = $"Service '{serviceName}' not registered",
                available = _manager.GetAllInstances().Select(p => p.Name).Distinct()
            });
        }

        if (!instance.IsRunning)
        {
            return ServiceUnavailable(new
            {
                error = $"Service '{serviceName}' is not currently running (restart in progress?)",
                instanceId = instance.InstanceId,
                restartCount = instance.RestartCount
            });
        }

        // Buffer the request body so we can re-read it (model binding may have consumed it).
        Request.EnableBuffering();
        var bodyBytes = await ReadBodyAsync(Request.Body);

        // Build the upstream URL
        var upstreamUrl = $"http://localhost:{instance.Port}/{path ?? ""}";
        if (Request.QueryString.HasValue) upstreamUrl += Request.QueryString.Value;

        // Build the upstream request
        using var upstreamReq = new HttpRequestMessage(method, upstreamUrl);
        // Copy request body (for POST/PUT/PATCH)
        if (method != HttpMethod.Get && bodyBytes.Length > 0)
        {
            upstreamReq.Content = new ByteArrayContent(bodyBytes);
            if (Request.ContentType is not null)
            {
                // MediaTypeHeaderValue doesn't accept "application/json; charset=utf-8" directly;
                // use TryParse to handle the full Content-Type header including charset.
                if (System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(Request.ContentType, out var mtv))
                    upstreamReq.Content.Headers.ContentType = mtv;
                else
                    upstreamReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }
        }
        // Copy selected headers
        foreach (var header in Request.Headers.Where(h => h.Key.StartsWith("X-") || h.Key == "Authorization" || h.Key == "Accept"))
        {
            upstreamReq.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        var client = _httpClientFactory.CreateClient("gateway");
        client.Timeout = TimeSpan.FromMinutes(5);

        _logger.LogInformation("Proxy {Method} /api/{Service}/{Path} → {Upstream} (instance #{InstanceId}, body={BodyLen}B)",
            method, serviceName, path, upstreamUrl, instance.InstanceId, bodyBytes.Length);

        try
        {
            using var upstreamResp = await client.SendAsync(upstreamReq);
            var respBody = await upstreamResp.Content.ReadAsStringAsync();
            return StatusCode((int)upstreamResp.StatusCode, respBody);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { error = "Upstream service timed out" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Proxy error to {Service} (port {Port})", serviceName, instance.Port);
            return StatusCode(502, new { error = $"Upstream connection failed: {ex.Message}" });
        }
    }

    private static async Task<byte[]> ReadBodyAsync(Stream body)
    {
        if (body is null) return Array.Empty<byte>();
        if (body.CanSeek) body.Position = 0;
        using var ms = new MemoryStream();
        await body.CopyToAsync(ms);
        return ms.ToArray();
    }

    private IActionResult ServiceUnavailable(object obj) => StatusCode(503, obj);
}
