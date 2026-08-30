# Chapter 04: Platform Architecture
## Understanding the Artichoke-FaaS Core Design

---

## Architectural Overview

Artichoke-FaaS implements a **distributed microservices architecture** with complete process isolation between the platform and functions. This design ensures maximum reliability, scalability, and maintainability.

```
┌─────────────────────────────────────────────────────────────────┐
│                    ARTICHOKE-FAAS PLATFORM                     │
│                        (Port: 5000)                            │
├─────────────────┬─────────────────┬─────────────────────────────┤
│   Core Services │  Management     │    Communication Layer     │
│                 │  Services       │                            │
│ • Registry      │ • Lifecycle     │ • HTTP API                 │
│ • Discovery     │ • Health Check  │ • SignalR Hub              │
│ • Scheduling    │ • Metrics       │ • Command Queue            │
│ • Persistence   │ • Logging       │ • Event Broadcasting       │
└─────────────────┴─────────────────┴─────────────────────────────┘
                           │
                    HTTP/JSON Communication
                           │
┌─────────────────────────────────────────────────────────────────┐
│                  EXTERNAL FUNCTION ECOSYSTEM                   │
├──────────────────┬──────────────────┬─────────────────────────────┤
│  Function Host   │  Function Host   │    Function Host          │
│  Process A       │  Process B       │    Process C              │
│  (PID: 1234)     │  (PID: 1235)     │    (PID: 1236)            │
│                  │                  │                           │
│ BookProcessor    │ HealthMonitor    │ AuditLogger               │
│ • Business       │ • System Health  │ • Security Events         │
│ • 5min Schedule  │ • 2min Schedule  │ • 30sec Schedule           │
│ • Auto-Recovery  │ • Critical Alerts│ • Compliance Logging      │
└──────────────────┴──────────────────┴─────────────────────────────┘
```

---

## Core Components Deep Dive

### 1. **Artichoke.FaaS.Platform** 🏗️

The central orchestration service that manages the entire function ecosystem.

#### **Key Responsibilities:**
- **Function Registry**: Maintains catalog of all available functions
- **Process Management**: Lifecycle control of function host processes  
- **Health Monitoring**: Continuous monitoring of function and system health
- **Scheduling**: Cron-based and interval-based function execution
- **Communication Hub**: Manages all platform-to-function communication

#### **Core Services Architecture:**

```csharp
// Platform Services Structure
public class PlatformServices
{
    public IFunctionRegistry Registry { get; }
    public IProcessManager ProcessManager { get; }
    public IHealthMonitor HealthMonitor { get; }
    public ISchedulingService Scheduler { get; }
    public ICommunicationHub CommunicationHub { get; }
}
```

#### **Database Schema:**
```sql
-- Function Registry Table
CREATE TABLE Functions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Version NVARCHAR(20) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    ProcessId INT NULL,
    LastExecution DATETIME2 NULL,
    Configuration NVARCHAR(MAX) NULL
);

-- Execution History Table  
CREATE TABLE ExecutionHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    FunctionId UNIQUEIDENTIFIER NOT NULL,
    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2 NULL,
    Status NVARCHAR(20) NOT NULL,
    Result NVARCHAR(MAX) NULL,
    ErrorDetails NVARCHAR(MAX) NULL
);
```

### 2. **Artichoke.FaaS.Core** ⚡

The foundational library containing all interfaces, abstractions, and built-in implementations.

#### **Interface Hierarchy:**

```csharp
// Core Development Kit Interface
public interface IDevelopmentKit
{
    // Trigger Management
    ITrigger CreateTrigger(string triggerType, TriggerConfiguration config);
    ITriggerFactory GetTriggerFactory(string triggerType);
    IEnumerable<string> GetAvailableTriggers();
    
    // Function Management  
    IEnumerable<ICustomFunction> GetRegisteredFunctions();
    void RegisterFunction(ICustomFunction function);
    void UnregisterFunction(string functionName);
    
    // Execution Control
    Task<ExecutionResult> ExecuteFunctionAsync(string functionName, object parameters);
    Task<HealthStatus> CheckFunctionHealthAsync(string functionName);
}
```

#### **Built-in Trigger System:**

```csharp
// Base Trigger Implementation
public abstract class TriggerBase : ITrigger
{
    public abstract string TriggerType { get; }
    public abstract TriggerConfiguration Configuration { get; }
    
    public abstract Task<bool> ShouldExecuteAsync();
    public abstract Task<TriggerResult> ExecuteAsync(ICustomFunction function);
    
    protected virtual void OnTriggerFired(TriggerEventArgs args) =>
        TriggerFired?.Invoke(this, args);
        
    public event EventHandler<TriggerEventArgs> TriggerFired;
}

// HTTP Trigger Implementation
public class HttpTrigger : TriggerBase
{
    public override string TriggerType => "HttpTrigger";
    
    public HttpMethod[] AllowedMethods { get; set; }
    public string Route { get; set; }
    public AuthorizationLevel AuthLevel { get; set; }
    
    public override async Task<TriggerResult> ExecuteAsync(ICustomFunction function)
    {
        // HTTP request processing logic
        var httpContext = GetCurrentHttpContext();
        var parameters = ExtractParameters(httpContext.Request);
        
        var result = await function.ExecuteAsync(parameters);
        
        return new TriggerResult
        {
            IsSuccess = result.IsSuccess,
            Response = FormatHttpResponse(result),
            ExecutionTime = result.ExecutionDuration
        };
    }
}

// Timer Trigger Implementation  
public class TimerTrigger : TriggerBase
{
    public override string TriggerType => "TimerTrigger";
    
    public string CronExpression { get; set; }
    public bool RunOnStartup { get; set; }
    public bool UseMonitor { get; set; }
    
    public override async Task<bool> ShouldExecuteAsync()
    {
        var cronSchedule = CronExpression.Parse(CronExpression);
        var nextOccurrence = cronSchedule.GetNextOccurrence(DateTimeOffset.Now);
        
        return nextOccurrence <= DateTimeOffset.Now.AddSeconds(1);
    }
}
```

### 3. **Artichoke.FaaS.Client** 📱

Administrative interface providing both CLI and web-based management capabilities.

#### **Command Structure:**

```csharp
// CLI Command Interface
public interface IAdminCommand
{
    string CommandName { get; }
    string Description { get; }
    Task<CommandResult> ExecuteAsync(string[] args);
}

// Available Commands
public class AdminCommands
{
    public ListFunctionsCommand ListFunctions { get; }      // list
    public StartFunctionCommand StartFunction { get; }      // start <name>
    public StopFunctionCommand StopFunction { get; }        // stop <name>
    public RestartFunctionCommand RestartFunction { get; }  // restart <name>
    public HealthCheckCommand HealthCheck { get; }          // health [name]
    public LogsCommand Logs { get; }                        // logs <name> [lines]
    public MonitorCommand Monitor { get; }                  // monitor [interval]
}
```

#### **Real-time Dashboard:**
- 📊 **Live Metrics**: Function execution statistics and performance data
- 🔍 **Process Monitor**: Real-time view of all function host processes
- 📋 **Execution Logs**: Streaming logs from all functions
- ⚡ **Quick Actions**: Start/stop/restart functions with one click

---

## Function Host Architecture

### **BMS.FunctionHost Process Model**

Each function runs in its own isolated process using the FunctionHost architecture:

```csharp
// Function Host Service Generic Implementation
public class FunctionHostService<TFunction> : BackgroundService
    where TFunction : class, IFunction
{
    private readonly TFunction _function;
    private readonly ICommunicationService _communicationService;
    private readonly ILogger<FunctionHostService<TFunction>> _logger;
    
    public FunctionHostService(
        TFunction function,
        ICommunicationService communicationService,
        ILogger<FunctionHostService<TFunction>> logger)
    {
        _function = function;
        _communicationService = communicationService;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Register with platform
        await _communicationService.RegisterFunctionAsync(
            _function.Name, 
            Environment.ProcessId);
            
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check for pending commands
                var command = await _communicationService.GetPendingCommandAsync(_function.Name);
                
                if (command != null)
                {
                    await ProcessCommandAsync(command);
                }
                
                // Check if function should execute based on interval
                if (ShouldExecuteNow())
                {
                    await ExecuteFunctionAsync();
                }
                
                // Health check
                await ReportHealthAsync();
                
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in function host execution loop");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
    
    private async Task ExecuteFunctionAsync()
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("🔄 {FunctionName} execution started", _function.Name);
            
            var result = await _function.ExecuteAsync(CancellationToken.None);
            
            var duration = DateTime.UtcNow - startTime;
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("✅ {FunctionName} execution completed in {Duration}ms", 
                    _function.Name, duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("⚠️ {FunctionName} execution failed: {Error}", 
                    _function.Name, result.Message);
            }
            
            // Report results back to platform
            await _communicationService.ReportExecutionResultAsync(_function.Name, result);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "❌ {FunctionName} execution failed with exception", _function.Name);
            
            var failureResult = FunctionResult.Failure(
                $"Function failed: {ex.Message}", 
                duration, 
                ex.ToString());
                
            await _communicationService.ReportExecutionResultAsync(_function.Name, failureResult);
        }
    }
}
```

---

## Communication Architecture

### **HTTP-Based Communication Protocol**

The platform and functions communicate using a well-defined HTTP API:

```csharp
// Platform API Endpoints
[ApiController]
[Route("api/[controller]")]
public class FunctionManagementController : ControllerBase
{
    // Function Registration
    [HttpPost("register")]
    public async Task<IActionResult> RegisterFunction([FromBody] FunctionRegistration registration)
    {
        // Register function in platform registry
        await _functionRegistry.RegisterAsync(registration);
        return Ok(new { Status = "Registered", FunctionId = registration.Id });
    }
    
    // Command Distribution  
    [HttpGet("commands/{functionName}")]
    public async Task<IActionResult> GetPendingCommands(string functionName)
    {
        var commands = await _commandQueue.GetCommandsAsync(functionName);
        return Ok(commands);
    }
    
    // Result Collection
    [HttpPost("results")]
    public async Task<IActionResult> ReportExecutionResult([FromBody] ExecutionResult result)
    {
        await _executionHistory.SaveAsync(result);
        await _metricsCollector.RecordExecutionAsync(result);
        return Ok();
    }
    
    // Health Monitoring
    [HttpPost("health")]
    public async Task<IActionResult> ReportHealth([FromBody] HealthReport health)
    {
        await _healthMonitor.UpdateHealthAsync(health);
        return Ok();
    }
}
```

### **Real-time Communication with SignalR**

For real-time updates and monitoring:

```csharp
// SignalR Hub for Real-time Communication
public class FunctionMonitoringHub : Hub
{
    // Clients can join specific function groups
    public async Task JoinFunctionGroup(string functionName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"function_{functionName}");
    }
    
    // Broadcast execution updates
    public async Task BroadcastExecutionUpdate(string functionName, ExecutionUpdate update)
    {
        await Clients.Group($"function_{functionName}").SendAsync("ExecutionUpdate", update);
    }
    
    // Broadcast health updates
    public async Task BroadcastHealthUpdate(string functionName, HealthUpdate update)
    {
        await Clients.Group($"function_{functionName}").SendAsync("HealthUpdate", update);
    }
}
```

---

## Scalability & Performance Design

### **Horizontal Scaling**

Functions can be scaled independently by starting multiple host processes:

```powershell
# Scale BookProcessor to 3 instances
dotnet run --project BMS.FunctionHost --function BookProcessor --instance 1
dotnet run --project BMS.FunctionHost --function BookProcessor --instance 2  
dotnet run --project BMS.FunctionHost --function BookProcessor --instance 3
```

### **Load Distribution**

The platform automatically distributes work across function instances:

```csharp
public class LoadBalancedFunctionManager
{
    private readonly Dictionary<string, List<FunctionInstance>> _functionInstances = new();
    private readonly RoundRobinLoadBalancer _loadBalancer = new();
    
    public async Task<ExecutionResult> ExecuteFunctionAsync(string functionName, object parameters)
    {
        var instances = _functionInstances[functionName];
        var selectedInstance = _loadBalancer.SelectInstance(instances);
        
        return await selectedInstance.ExecuteAsync(parameters);
    }
}
```

### **Resource Management**

Each function process can be configured with resource limits:

```csharp
// Function Host Resource Configuration
public class ResourceConfiguration
{
    public int MaxMemoryMB { get; set; } = 512;
    public double MaxCpuPercent { get; set; } = 50.0;
    public int MaxExecutionTimeSeconds { get; set; } = 300;
    public int MaxConcurrentExecutions { get; set; } = 1;
}
```

---

## Fault Tolerance & Recovery

### **Process Monitoring**

The platform continuously monitors function host processes:

```csharp
public class ProcessHealthMonitor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var function in await _registry.GetAllFunctionsAsync())
            {
                if (function.ProcessId.HasValue)
                {
                    var process = Process.GetProcessById(function.ProcessId.Value);
                    
                    if (process.HasExited)
                    {
                        _logger.LogWarning("Function {FunctionName} process {ProcessId} has exited", 
                            function.Name, function.ProcessId);
                            
                        // Attempt automatic restart
                        await _processManager.RestartFunctionAsync(function.Name);
                    }
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

### **Automatic Recovery**

Functions are automatically restarted on failure:

```csharp
public class AutoRecoveryService
{
    public async Task HandleFunctionFailure(string functionName, Exception exception)
    {
        var retryPolicy = await _configurationService.GetRetryPolicyAsync(functionName);
        
        for (int attempt = 1; attempt <= retryPolicy.MaxRetries; attempt++)
        {
            try
            {
                await _processManager.RestartFunctionAsync(functionName);
                
                // Wait for function to become healthy
                var isHealthy = await WaitForHealthyStatus(functionName, TimeSpan.FromMinutes(2));
                
                if (isHealthy)
                {
                    _logger.LogInformation("Function {FunctionName} successfully recovered on attempt {Attempt}", 
                        functionName, attempt);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Function {FunctionName} restart attempt {Attempt} failed", 
                    functionName, attempt);
            }
            
            await Task.Delay(TimeSpan.FromSeconds(retryPolicy.DelaySeconds * attempt));
        }
        
        // Mark function as failed after all retry attempts
        await _registry.MarkFunctionAsFailedAsync(functionName);
    }
}
```

---

## Security Architecture

### **Process Isolation**

Each function runs in its own security context:

```csharp
public class SecurityConfiguration
{
    public string RunAsUser { get; set; } = "NETWORK SERVICE";
    public List<string> RequiredPrivileges { get; set; } = new();
    public bool EnableSandbox { get; set; } = true;
    public List<string> AllowedNetworkEndpoints { get; set; } = new();
}
```

### **API Security**

All platform APIs are secured with JWT authentication:

```csharp
[Authorize(Roles = "FunctionHost")]
[HttpPost("register")]
public async Task<IActionResult> RegisterFunction([FromBody] FunctionRegistration registration)
{
    // Only authenticated function hosts can register
    var hostClaims = User.Claims;
    // Validate host identity and permissions
}
```

---

## What's Next?

Now that you understand the platform architecture, explore:

1. **🔧 Built-in Triggers**: [Chapter 05: Built-in Triggers System](05-BUILTIN-TRIGGERS-SYSTEM.md)
2. **🛠️ Development Kit**: [Chapter 06: Development Kit Framework](06-DEVELOPMENT-KIT-FRAMEWORK.md)
3. **⚡ Function Implementation**: [Chapter 07: Function Interface & Implementation](07-FUNCTION-INTERFACE-IMPLEMENTATION.md)

Continue to: [Chapter 05: Built-in Triggers System](05-BUILTIN-TRIGGERS-SYSTEM.md)
