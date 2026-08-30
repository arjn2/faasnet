# Chapter 05: Built-in Triggers System
## Understanding Artichoke-FaaS Trigger Architecture

---

## Trigger System Overview

Artichoke-FaaS includes **2 production-ready built-in triggers** that handle the most common function execution scenarios. The trigger system is designed for extensibility, allowing custom triggers to be easily added.

### **Built-in Triggers**

1. **🌐 HttpTrigger** - HTTP request-based function execution
2. **⏰ TimerTrigger** - Schedule-based function execution

### **Trigger Architecture**

```
┌─────────────────────────────────────────────────────────────┐
│                    TRIGGER FACTORY SYSTEM                  │
├─────────────────┬─────────────────┬─────────────────────────┤
│  DevelopmentKit │  Trigger        │    Built-in Triggers   │
│  Base           │  Factories      │                        │
│                 │                 │                        │
│ • Registration  │ • HttpFactory   │ • HttpTrigger          │
│ • Discovery     │ • TimerFactory  │ • TimerTrigger         │
│ • Lifecycle     │ • Custom...     │ • Custom...            │
└─────────────────┴─────────────────┴─────────────────────────┘
                           │
                    Thread-Safe Creation
                           │
┌─────────────────────────────────────────────────────────────┐
│                  TRIGGER EXECUTION ENGINE                  │
├──────────────────┬──────────────────┬─────────────────────────┤
│  HTTP Processor  │  Timer Scheduler │    Extension Points   │
│                  │                  │                        │
│ • Route Matching │ • Cron Parsing   │ • Custom Logic         │
│ • Auth Handling  │ • Interval Calc  │ • Plugin System        │
│ • Response Format│ • Past-due Detect│ • Middleware Chain     │
└──────────────────┴──────────────────┴─────────────────────────┘
```

---

## HttpTrigger Deep Dive

### **Core Implementation**

```csharp
// HttpTrigger Class Definition
public class HttpTrigger : ITrigger
{
    public string TriggerType => "HttpTrigger";
    public TriggerConfiguration Configuration { get; private set; }
    
    // HTTP-specific properties
    public HttpMethod[] AllowedMethods { get; set; }
    public string Route { get; set; }
    public AuthorizationLevel AuthorizationLevel { get; set; }
    public bool RequireHttps { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    
    public HttpTrigger(HttpTriggerConfiguration config)
    {
        Configuration = config;
        AllowedMethods = config.Methods ?? new[] { HttpMethod.Get, HttpMethod.Post };
        Route = config.Route ?? "";
        AuthorizationLevel = config.AuthLevel;
        RequireHttps = config.RequireHttps;
        Headers = config.CustomHeaders ?? new Dictionary<string, string>();
    }
}
```

### **Configuration Options**

```csharp
// HttpTrigger Configuration
public class HttpTriggerConfiguration : TriggerConfiguration
{
    [Required]
    public HttpMethod[] Methods { get; set; }
    
    public string Route { get; set; } = "";
    
    public AuthorizationLevel AuthLevel { get; set; } = AuthorizationLevel.Function;
    
    public bool RequireHttps { get; set; } = true;
    
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
    
    public int TimeoutSeconds { get; set; } = 30;
    
    public bool EnableCors { get; set; } = true;
    
    public string[] AllowedOrigins { get; set; } = { "*" };
}

// Authorization Levels
public enum AuthorizationLevel
{
    Anonymous,    // No authentication required
    Function,     // Function-level key required
    Admin,        // Admin-level key required  
    System        // System-level authentication
}
```

### **Usage Examples**

#### **Basic HTTP GET Trigger**

```csharp
// Simple GET endpoint
[HttpTrigger(AuthorizationLevel.Anonymous, "get")]
public class GetBooksFunction : IFunction
{
    public string Name => "GetBooks";
    public string Description => "Retrieve all books from the library";
    public string Version => "1.0.0";
    public TimeSpan? Interval => null; // HTTP only, no scheduling
    public FunctionCategory Category => FunctionCategory.Business;
    
    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Access HTTP context
        var httpContext = HttpContextAccessor.Current;
        var queryParams = httpContext.Request.Query;
        
        // Your business logic here
        var books = await _bookService.GetAllBooksAsync();
        
        return FunctionResult.Success(
            "Books retrieved successfully", 
            DateTime.UtcNow - startTime, 
            books);
    }
}
```

#### **REST API with Multiple Methods**

```csharp
// Full REST API endpoint
[HttpTrigger(AuthorizationLevel.Function, "get", "post", "put", "delete", Route = "books/{id?}")]
public class BooksApiFunction : IFunction
{
    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = HttpContextAccessor.Current;
        var method = httpContext.Request.Method;
        var route = httpContext.Request.RouteValues;
        
        return method.ToUpper() switch
        {
            "GET" => await HandleGetAsync(route),
            "POST" => await HandlePostAsync(httpContext.Request),
            "PUT" => await HandlePutAsync(route, httpContext.Request),
            "DELETE" => await HandleDeleteAsync(route),
            _ => FunctionResult.Failure($"Method {method} not supported", TimeSpan.Zero)
        };
    }
    
    private async Task<FunctionResult> HandleGetAsync(RouteValueDictionary route)
    {
        var id = route["id"]?.ToString();
        
        if (string.IsNullOrEmpty(id))
        {
            // Get all books
            var books = await _bookService.GetAllBooksAsync();
            return FunctionResult.Success("All books retrieved", executionTime, books);
        }
        else
        {
            // Get specific book
            var book = await _bookService.GetBookAsync(int.Parse(id));
            return FunctionResult.Success($"Book {id} retrieved", executionTime, book);
        }
    }
    
    private async Task<FunctionResult> HandlePostAsync(HttpRequest request)
    {
        var bookData = await JsonSerializer.DeserializeAsync<Book>(request.Body);
        var newBook = await _bookService.CreateBookAsync(bookData);
        
        return FunctionResult.Success("Book created successfully", executionTime, newBook);
    }
}
```

#### **Secured Admin Endpoint**

```csharp
// Admin-only endpoint with custom authentication
[HttpTrigger(AuthorizationLevel.Admin, "post", "delete", Route = "admin/books")]
public class AdminBooksFunction : IFunction
{
    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = HttpContextAccessor.Current;
        
        // Verify admin privileges
        if (!await IsUserAdmin(httpContext.User))
        {
            throw new UnauthorizedAccessException("Admin privileges required");
        }
        
        // Admin-specific logic here
        var result = await ProcessAdminRequest(httpContext);
        
        return FunctionResult.Success("Admin operation completed", executionTime, result);
    }
    
    private async Task<bool> IsUserAdmin(ClaimsPrincipal user)
    {
        return user.IsInRole("Admin") && 
               user.HasClaim("permission", "books:manage");
    }
}
```

### **HTTP Response Formatting**

```csharp
// Custom HTTP Response Builder
public class HttpResponseBuilder
{
    public static IActionResult BuildResponse(FunctionResult result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(new
            {
                success = true,
                data = result.Data,
                executionTime = result.ExecutionDuration.TotalMilliseconds,
                timestamp = result.ExecutedAt
            });
        }
        else
        {
            return new BadRequestObjectResult(new
            {
                success = false,
                error = result.Message,
                details = result.ErrorDetails,
                timestamp = result.ExecutedAt
            });
        }
    }
}
```

---

## TimerTrigger Deep Dive

### **Core Implementation**

```csharp
// TimerTrigger Class Definition
public class TimerTrigger : ITrigger
{
    public string TriggerType => "TimerTrigger";
    public TriggerConfiguration Configuration { get; private set; }
    
    // Timer-specific properties
    public string CronExpression { get; set; }
    public bool RunOnStartup { get; set; }
    public bool UseMonitor { get; set; }
    public TimeSpan? MaxExecutionTime { get; set; }
    public bool EnablePastDueChecking { get; set; }
    
    private readonly CronExpression _cronSchedule;
    private DateTime? _lastExecution;
    private readonly object _lockObject = new object();
    
    public TimerTrigger(TimerTriggerConfiguration config)
    {
        Configuration = config;
        CronExpression = config.Schedule;
        RunOnStartup = config.RunOnStartup;
        UseMonitor = config.UseMonitor;
        MaxExecutionTime = config.MaxExecutionTime;
        EnablePastDueChecking = config.EnablePastDueChecking;
        
        _cronSchedule = Cronos.CronExpression.Parse(CronExpression);
    }
    
    public bool ShouldExecute()
    {
        lock (_lockObject)
        {
            var now = DateTime.UtcNow;
            var nextOccurrence = _cronSchedule.GetNextOccurrence(
                _lastExecution ?? now.AddSeconds(-1));
                
            if (nextOccurrence <= now)
            {
                _lastExecution = now;
                return true;
            }
            
            return false;
        }
    }
}
```

### **Configuration Options**

```csharp
// TimerTrigger Configuration
public class TimerTriggerConfiguration : TriggerConfiguration
{
    [Required]
    public string Schedule { get; set; }
    
    public bool RunOnStartup { get; set; } = false;
    
    public bool UseMonitor { get; set; } = true;
    
    public TimeSpan? MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);
    
    public bool EnablePastDueChecking { get; set; } = true;
    
    public int MaxPastDueCount { get; set; } = 5;
    
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
    
    public bool EnableJitter { get; set; } = false;
    
    public TimeSpan? JitterRange { get; set; } = TimeSpan.FromSeconds(30);
}
```

### **Cron Expression Examples**

```csharp
// Common Cron Patterns
public static class CronPatterns
{
    // Every minute
    public const string EveryMinute = "0 * * * * *";
    
    // Every 5 minutes
    public const string Every5Minutes = "0 */5 * * * *";
    
    // Every hour at minute 0
    public const string Hourly = "0 0 * * * *";
    
    // Every day at 2:30 AM
    public const string Daily = "0 30 2 * * *";
    
    // Every Monday at 9:00 AM  
    public const string WeeklyMonday = "0 0 9 * * MON";
    
    // First day of every month at midnight
    public const string Monthly = "0 0 0 1 * *";
    
    // Every 30 seconds
    public const string Every30Seconds = "*/30 * * * * *";
    
    // Business hours (9 AM to 5 PM, Monday to Friday)
    public const string BusinessHours = "0 0 9-17 * * MON-FRI";
}
```

### **Usage Examples**

#### **Simple Scheduled Function**

```csharp
// Function that runs every 5 minutes
[TimerTrigger("0 */5 * * * *")]
public class BookProcessorFunction : IFunction
{
    public string Name => "BookProcessor";
    public string Description => "Processes book operations queue";
    public string Version => "1.0.0";
    public TimeSpan? Interval => TimeSpan.FromMinutes(5);
    public FunctionCategory Category => FunctionCategory.Business;
    
    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("🔄 BookProcessor execution started");
            
            // Process pending book operations
            var processed = await ProcessBookOperationsQueue(cancellationToken);
            
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("✅ BookProcessor completed. Processed: {Count} items in {Duration}ms", 
                processed, duration.TotalMilliseconds);
                
            return FunctionResult.Success(
                $"Processed {processed} book operations successfully", 
                duration, 
                new { ProcessedCount = processed });
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "❌ BookProcessor execution failed");
            
            return FunctionResult.Failure(
                $"BookProcessor failed: {ex.Message}", 
                duration, 
                ex.ToString());
        }
    }
}
```

#### **Health Monitoring Function**

```csharp
// System health check every 2 minutes
[TimerTrigger("0 */2 * * * *", RunOnStartup = true, UseMonitor = true)]
public class HealthMonitorFunction : IFunction
{
    public string Name => "HealthMonitor";
    public string Description => "Continuous system health monitoring";
    public string Version => "1.0.0";
    public TimeSpan? Interval => TimeSpan.FromMinutes(2);
    public FunctionCategory Category => FunctionCategory.System;
    
    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var healthResults = new List<HealthCheckResult>();
        
        try
        {
            _logger.LogInformation("🏥 HealthMonitor execution started");
            
            // Check database health
            healthResults.Add(await CheckDatabaseHealth());
            
            // Check application services
            healthResults.Add(await CheckApplicationServicesHealth());
            
            // Check system resources
            healthResults.Add(await CheckSystemResourcesHealth());
            
            var criticalIssues = healthResults.Count(r => !r.IsHealthy);
            var duration = DateTime.UtcNow - startTime;
            
            if (criticalIssues > 0)
            {
                _logger.LogWarning("⚠️ HealthMonitor found {Issues} critical issues", criticalIssues);
                
                // Send alerts for critical issues
                await SendHealthAlerts(healthResults.Where(r => !r.IsHealthy));
            }
            
            return FunctionResult.Success(
                $"Health check completed. {healthResults.Count} components checked, {criticalIssues} issues found",
                duration,
                new { 
                    ComponentsChecked = healthResults.Count,
                    CriticalIssues = criticalIssues,
                    HealthResults = healthResults
                });
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "❌ HealthMonitor execution failed");
            
            return FunctionResult.Failure(
                $"HealthMonitor failed: {ex.Message}", 
                duration, 
                ex.ToString());
        }
    }
}
```

#### **Daily Maintenance Function**

```csharp
// Runs every day at 2:30 AM for maintenance tasks
[TimerTrigger("0 30 2 * * *", UseMonitor = true, MaxExecutionTime = "01:00:00")]
public class DailyMaintenanceFunction : IFunction
{
    public string Name => "DailyMaintenance";
    public string Description => "Daily system maintenance and cleanup";
    public string Version => "1.0.0";
    public TimeSpan? Interval => null; // Cron-based, not interval
    public FunctionCategory Category => FunctionCategory.System;
    
    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var maintenanceTasks = new List<string>();
        
        try
        {
            _logger.LogInformation("🔧 Daily maintenance started");
            
            // Clean up old log files
            var deletedLogs = await CleanupOldLogs(TimeSpan.FromDays(30));
            maintenanceTasks.Add($"Deleted {deletedLogs} old log files");
            
            // Archive old execution history
            var archivedRecords = await ArchiveExecutionHistory(TimeSpan.FromDays(90));
            maintenanceTasks.Add($"Archived {archivedRecords} execution records");
            
            // Optimize database
            await OptimizeDatabase();
            maintenanceTasks.Add("Database optimization completed");
            
            // Update system metrics
            await UpdateSystemMetrics();
            maintenanceTasks.Add("System metrics updated");
            
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation("✅ Daily maintenance completed in {Duration}ms", 
                duration.TotalMilliseconds);
                
            return FunctionResult.Success(
                "Daily maintenance completed successfully",
                duration,
                new { Tasks = maintenanceTasks, TaskCount = maintenanceTasks.Count });
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "❌ Daily maintenance failed");
            
            return FunctionResult.Failure(
                $"Daily maintenance failed: {ex.Message}",
                duration,
                ex.ToString());
        }
    }
}
```

### **Past-Due Execution Handling**

```csharp
// Timer Trigger with Past-Due Detection
public class TimerInfo
{
    public DateTime ScheduledTime { get; set; }
    public DateTime ActualExecutionTime { get; set; }
    public bool IsPastDue => ActualExecutionTime > ScheduledTime.AddMinutes(1);
    public TimeSpan Delay => ActualExecutionTime - ScheduledTime;
    public int PastDueCount { get; set; }
}

// Usage in function
public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
{
    var timerInfo = TimerContext.Current;
    
    if (timerInfo.IsPastDue)
    {
        _logger.LogWarning("⏰ Function execution is past due by {Delay}", timerInfo.Delay);
        
        // Handle past-due execution
        if (timerInfo.PastDueCount > 5)
        {
            _logger.LogError("❌ Too many past-due executions, skipping this run");
            return FunctionResult.Failure("Skipped due to excessive past-due count", TimeSpan.Zero);
        }
    }
    
    // Normal execution continues...
}
```

---

## Trigger Factory System

### **Factory Implementation**

```csharp
// Thread-safe trigger factory
public class DevelopmentKitFactories
{
    private readonly ConcurrentDictionary<string, ITriggerFactory> _triggerFactories = new();
    private readonly ILogger<DevelopmentKitFactories> _logger;
    
    public DevelopmentKitFactories(ILogger<DevelopmentKitFactories> logger)
    {
        _logger = logger;
        RegisterBuiltInFactories();
    }
    
    private void RegisterBuiltInFactories()
    {
        // Register built-in trigger factories
        _triggerFactories.TryAdd("HttpTrigger", new HttpTriggerFactory());
        _triggerFactories.TryAdd("TimerTrigger", new TimerTriggerFactory());
        
        _logger.LogInformation("Registered {Count} built-in trigger factories", _triggerFactories.Count);
    }
    
    public ITrigger CreateTrigger(string triggerType, TriggerConfiguration configuration)
    {
        if (!_triggerFactories.TryGetValue(triggerType, out var factory))
        {
            throw new NotSupportedException($"Trigger type '{triggerType}' is not supported");
        }
        
        return factory.CreateTrigger(configuration);
    }
    
    public void RegisterCustomFactory(string triggerType, ITriggerFactory factory)
    {
        _triggerFactories.TryAdd(triggerType, factory);
        _logger.LogInformation("Registered custom trigger factory: {TriggerType}", triggerType);
    }
}

// HttpTrigger Factory
public class HttpTriggerFactory : ITriggerFactory
{
    public string TriggerType => "HttpTrigger";
    
    public ITrigger CreateTrigger(TriggerConfiguration configuration)
    {
        if (configuration is not HttpTriggerConfiguration httpConfig)
        {
            throw new ArgumentException($"Invalid configuration type for HttpTrigger");
        }
        
        return new HttpTrigger(httpConfig);
    }
}

// TimerTrigger Factory
public class TimerTriggerFactory : ITriggerFactory
{
    public string TriggerType => "TimerTrigger";
    
    public ITrigger CreateTrigger(TriggerConfiguration configuration)
    {
        if (configuration is not TimerTriggerConfiguration timerConfig)
        {
            throw new ArgumentException($"Invalid configuration type for TimerTrigger");
        }
        
        return new TimerTrigger(timerConfig);
    }
}
```

---

## Custom Trigger Development

### **Creating Custom Triggers**

```csharp
// Example: File System Watcher Trigger
public class FileWatcherTrigger : ITrigger
{
    public string TriggerType => "FileWatcherTrigger";
    public TriggerConfiguration Configuration { get; private set; }
    
    private readonly FileSystemWatcher _watcher;
    private readonly string _watchPath;
    private readonly string _filePattern;
    
    public FileWatcherTrigger(FileWatcherConfiguration config)
    {
        Configuration = config;
        _watchPath = config.WatchPath;
        _filePattern = config.FilePattern ?? "*.*";
        
        _watcher = new FileSystemWatcher(_watchPath, _filePattern)
        {
            IncludeSubdirectories = config.IncludeSubdirectories,
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite
        };
        
        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged;
    }
    
    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        await TriggerFunction(new { EventType = "Created", FilePath = e.FullPath });
    }
    
    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        await TriggerFunction(new { EventType = "Changed", FilePath = e.FullPath });
    }
}

// File Watcher Configuration
public class FileWatcherConfiguration : TriggerConfiguration
{
    [Required]
    public string WatchPath { get; set; }
    
    public string FilePattern { get; set; } = "*.*";
    
    public bool IncludeSubdirectories { get; set; } = false;
    
    public TimeSpan DebounceDelay { get; set; } = TimeSpan.FromSeconds(1);
}
```

### **Registering Custom Triggers**

```csharp
// Register custom trigger in DI container
public void RegisterCustomTriggers(IServiceCollection services)
{
    services.AddSingleton<ITriggerFactory, FileWatcherTriggerFactory>();
    
    // Register with development kit
    var developmentKit = services.GetService<IDevelopmentKit>();
    developmentKit.RegisterTriggerFactory("FileWatcherTrigger", new FileWatcherTriggerFactory());
}
```

---

## Trigger Best Practices

### **Performance Optimization**

```csharp
// Efficient timer trigger implementation
public class OptimizedTimerTrigger : TimerTrigger
{
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    
    public override async Task<TriggerResult> ExecuteAsync(ICustomFunction function)
    {
        // Prevent concurrent executions
        if (!await _executionSemaphore.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            return new TriggerResult
            {
                IsSuccess = false,
                Message = "Previous execution still in progress",
                ExecutionTime = TimeSpan.Zero
            };
        }
        
        try
        {
            return await base.ExecuteAsync(function);
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }
}
```

### **Error Handling**

```csharp
// Robust trigger error handling
public abstract class ResilientTriggerBase : ITrigger
{
    protected async Task<TriggerResult> SafeExecuteAsync(Func<Task<TriggerResult>> execution)
    {
        var retryPolicy = new RetryPolicy(3, TimeSpan.FromSeconds(5));
        
        for (int attempt = 1; attempt <= retryPolicy.MaxRetries; attempt++)
        {
            try
            {
                return await execution();
            }
            catch (Exception ex) when (attempt < retryPolicy.MaxRetries)
            {
                _logger.LogWarning(ex, "Trigger execution attempt {Attempt} failed, retrying in {Delay}", 
                    attempt, retryPolicy.Delay);
                    
                await Task.Delay(retryPolicy.Delay);
            }
        }
        
        throw new TriggerExecutionException("All retry attempts failed");
    }
}
```

---

## What's Next?

Now that you understand the built-in triggers, explore:

1. **🛠️ Development Kit Framework**: [Chapter 06: Development Kit Framework](06-DEVELOPMENT-KIT-FRAMEWORK.md)
2. **⚡ Function Implementation**: [Chapter 07: Function Interface & Implementation](07-FUNCTION-INTERFACE-IMPLEMENTATION.md)
3. **🏗️ Function Host Architecture**: [Chapter 09: Function Host Architecture](09-FUNCTION-HOST-ARCHITECTURE.md)

Continue to: [Chapter 06: Development Kit Framework](06-DEVELOPMENT-KIT-FRAMEWORK.md)
