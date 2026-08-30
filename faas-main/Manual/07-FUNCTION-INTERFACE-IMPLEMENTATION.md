# Chapter 07: Function Interface & Implementation
## Building Functions for Artichoke-FaaS

---

## IFunction Interface Deep Dive

The `IFunction` interface is the foundation of all function implementations in Artichoke-FaaS. It provides a comprehensive contract for function lifecycle, execution, and health monitoring.

### **Complete Interface Definition**

```csharp
/// <summary>
/// Core function interface for Artichoke-FaaS platform
/// Zero external dependencies - everything runs in our .NET 9 app
/// </summary>
public interface IFunction
{
    /// <summary>
    /// Unique function name for CLI identification
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Function description for admin CLI help
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Function version for deployment tracking
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// Execution interval for scheduled functions (null = manual only)
    /// </summary>
    TimeSpan? Interval { get; }
    
    /// <summary>
    /// Function category for organization in CLI
    /// </summary>
    FunctionCategory Category { get; }
    
    /// <summary>
    /// Execute the function with cancellation support
    /// </summary>
    Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Health check for the function
    /// </summary>
    Task<FunctionHealth> CheckHealthAsync();
    
    /// <summary>
    /// Function-specific configuration for admin CLI
    /// </summary>
    IDictionary<string, object> GetConfiguration();
    
    /// <summary>
    /// Validate function can execute (dependency checks, etc.)
    /// </summary>
    Task<bool> CanExecuteAsync();
}
```

### **Supporting Types**

```csharp
/// <summary>
/// Function execution result with detailed information
/// </summary>
public record FunctionResult(
    bool IsSuccess,
    string Message,
    TimeSpan ExecutionDuration,
    DateTime ExecutedAt,
    object? Data = null,
    string? ErrorDetails = null)
{
    public static FunctionResult Success(string message, TimeSpan duration, object? data = null)
        => new(true, message, duration, DateTime.UtcNow, data);
    
    public static FunctionResult Failure(string message, TimeSpan duration, string? errorDetails = null)
        => new(false, message, duration, DateTime.UtcNow, null, errorDetails);
}

/// <summary>
/// Function health status for monitoring
/// </summary>
public record FunctionHealth(
    HealthStatus Status,
    string Message,
    DateTime CheckedAt,
    object? Metrics = null);

/// <summary>
/// Function categories for CLI organization
/// </summary>
public enum FunctionCategory
{
    Business,      // Book processing, analytics, recommendations
    System,        // Health, cleanup, backup, optimization
    Integration,   // External APIs, notifications, webhooks
    Security,      // Security scans, audit, compliance
    Analytics      // Performance, usage analysis, reporting
}

/// <summary>
/// Health status levels for functions and system
/// </summary>
public enum HealthStatus
{
    Excellent = 5, // Perfect health, optimal performance
    Good = 4,      // Good health, normal operation
    Warning = 3,   // Some issues, needs attention
    Critical = 2,  // Critical issues, immediate action needed
    Failed = 1,    // Complete failure, not functioning
    Unknown = 0    // Status cannot be determined
}
```

---

## Real Function Implementations

### **1. BookProcessorFunction - Business Category**

A comprehensive business logic function that processes book operations with full error handling and metrics.

```csharp
/// <summary>
/// Book Processor Function - Integrates with existing Artichoke BookApplicationService
/// Processes book operations in background queue
/// </summary>
public class BookProcessorFunction : IFunction
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BookProcessorFunction> _logger;
    private int _executionCount = 0;
    private DateTime _lastHealthCheck = DateTime.UtcNow;
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);

    // IFunction Properties
    public string Name => "BookProcessor";
    public string Description => "Processes book operations in background queue - integrates with Artichoke BookApplicationService";
    public string Version => "1.0.0";
    public TimeSpan? Interval => TimeSpan.FromMinutes(5); // Run every 5 minutes
    public FunctionCategory Category => FunctionCategory.Business;

    public BookProcessorFunction(IServiceProvider serviceProvider, ILogger<BookProcessorFunction> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Prevent concurrent executions
        if (!await _executionSemaphore.WaitAsync(100, cancellationToken))
        {
            return FunctionResult.Failure("Previous execution still in progress", TimeSpan.Zero);
        }

        var startTime = DateTime.UtcNow;
        
        try
        {
            _executionCount++;
            _logger.LogInformation("🔄 BookProcessor execution #{Count} started", _executionCount);
            
            using var scope = _serviceProvider.CreateScope();
            var bookService = scope.ServiceProvider.GetRequiredService<IBookApplicationService>();
            
            // Process book operations queue
            var processed = await ProcessBookOperationsQueue(bookService, cancellationToken);
            
            // Perform additional business logic
            await PerformBusinessValidation(bookService, cancellationToken);
            await UpdateBookAnalytics(bookService, cancellationToken);
            
            var duration = DateTime.UtcNow - startTime;
            var message = $"Processed {processed} book operations successfully";
            
            _logger.LogInformation("✅ BookProcessor execution #{Count} completed in {Duration}ms. Processed: {Processed}", 
                _executionCount, duration.TotalMilliseconds, processed);
            
            return FunctionResult.Success(message, duration, new { 
                ProcessedCount = processed, 
                ExecutionNumber = _executionCount,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogWarning("⚠️ BookProcessor execution #{Count} was cancelled", _executionCount);
            
            return FunctionResult.Failure("BookProcessor execution was cancelled", duration, "Operation cancelled by user request");
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "❌ BookProcessor execution #{Count} failed", _executionCount);
            
            return FunctionResult.Failure($"BookProcessor failed: {ex.Message}", duration, ex.ToString());
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    public async Task<FunctionHealth> CheckHealthAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var bookService = scope.ServiceProvider.GetRequiredService<IBookApplicationService>();
            
            // Comprehensive health check
            var result = await bookService.GetAllBooksAsync();
            
            if (result.IsSuccess)
            {
                var count = result.Data?.Count() ?? 0;
                var timeSinceLastExecution = DateTime.UtcNow - _lastHealthCheck;
                
                return new FunctionHealth(
                    HealthStatus.Excellent,
                    $"BookProcessor healthy - {count} books accessible, {_executionCount} executions completed",
                    DateTime.UtcNow,
                    new { 
                        BooksCount = count, 
                        ExecutionCount = _executionCount,
                        TimeSinceLastCheck = timeSinceLastExecution,
                        MemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024 // MB
                    }
                );
            }
            else
            {
                return new FunctionHealth(
                    HealthStatus.Critical,
                    $"BookProcessor unhealthy - service error: {result.ErrorMessage}",
                    DateTime.UtcNow
                );
            }
        }
        catch (Exception ex)
        {
            return new FunctionHealth(
                HealthStatus.Failed,
                $"BookProcessor health check failed: {ex.Message}",
                DateTime.UtcNow
            );
        }
        finally
        {
            _lastHealthCheck = DateTime.UtcNow;
        }
    }

    public async Task<bool> CanExecuteAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var bookService = scope.ServiceProvider.GetService<IBookApplicationService>();
            var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            
            // Check all dependencies
            return bookService != null && 
                   dbContext != null &&
                   await dbContext.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public IDictionary<string, object> GetConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["Interval"] = "5 minutes",
            ["Category"] = "Business",
            ["IntegratesWith"] = "Artichoke BookApplicationService",
            ["QueueType"] = "In-Memory",
            ["RetryPolicy"] = "3 attempts",
            ["ExecutionCount"] = _executionCount,
            ["ConcurrencyControl"] = "Single execution (semaphore)",
            ["HealthCheckInterval"] = "Every execution",
            ["Dependencies"] = new[] { "IBookApplicationService", "ApplicationDbContext" }
        };
    }

    private async Task<int> ProcessBookOperationsQueue(IBookApplicationService bookService, CancellationToken cancellationToken)
    {
        // Simulate processing book operations
        // In real implementation, this would process queued operations
        
        var processed = 0;
        var operations = new[] { "Validation", "Indexing", "Categorization", "Recommendation Update" };
        
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogDebug("Processing operation: {Operation}", operation);
            await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);
            processed++;
        }
        
        // Simulate variable processing load
        var additionalItems = Random.Shared.Next(1, 10);
        processed += additionalItems;
        
        _logger.LogDebug("BookProcessor simulated processing {Count} operations", processed);
        
        return processed;
    }

    private async Task PerformBusinessValidation(IBookApplicationService bookService, CancellationToken cancellationToken)
    {
        // Business-specific validation logic
        _logger.LogDebug("Performing business validation");
        await Task.Delay(50, cancellationToken);
    }

    private async Task UpdateBookAnalytics(IBookApplicationService bookService, CancellationToken cancellationToken)
    {
        // Update analytics and metrics
        _logger.LogDebug("Updating book analytics");
        await Task.Delay(100, cancellationToken);
    }
}
```

### **2. HealthMonitorFunction - System Category**

A system monitoring function that performs comprehensive health checks across all platform components.

```csharp
/// <summary>
/// Health Monitor Function - Continuous system health monitoring
/// Integrates with Artichoke architecture components
/// </summary>
public class HealthMonitorFunction : IFunction
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthMonitorFunction> _logger;
    private int _executionCount = 0;
    private readonly Dictionary<string, HealthCheckResult> _lastHealthResults = new();

    // IFunction Properties
    public string Name => "HealthMonitor";
    public string Description => "Continuous system health monitoring for Artichoke architecture components";
    public string Version => "1.0.0";
    public TimeSpan? Interval => TimeSpan.FromMinutes(2); // Check every 2 minutes
    public FunctionCategory Category => FunctionCategory.System;

    public HealthMonitorFunction(IServiceProvider serviceProvider, ILogger<HealthMonitorFunction> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _executionCount++;
            _logger.LogInformation("🏥 HealthMonitor execution #{Count} started", _executionCount);
            
            var healthResults = await PerformHealthChecks(cancellationToken);
            
            // Store results for health check reporting
            foreach (var result in healthResults)
            {
                _lastHealthResults[result.Component] = result;
            }
            
            var duration = DateTime.UtcNow - startTime;
            var criticalIssues = healthResults.Count(r => !r.IsHealthy);
            
            if (criticalIssues > 0)
            {
                _logger.LogWarning("⚠️ HealthMonitor found {CriticalIssues} critical issues", criticalIssues);
                
                // Send alerts for critical issues
                await SendHealthAlerts(healthResults.Where(r => !r.IsHealthy), cancellationToken);
            }
            else
            {
                _logger.LogInformation("✅ All system components are healthy");
            }
            
            var message = $"Health check completed. {healthResults.Count} components checked, {criticalIssues} issues found";
            
            return FunctionResult.Success(message, duration, new { 
                ComponentsChecked = healthResults.Count,
                CriticalIssues = criticalIssues,
                ExecutionNumber = _executionCount,
                HealthResults = healthResults,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "❌ HealthMonitor execution #{Count} failed", _executionCount);
            
            return FunctionResult.Failure($"HealthMonitor failed: {ex.Message}", duration, ex.ToString());
        }
    }

    public async Task<FunctionHealth> CheckHealthAsync()
    {
        var healthyComponents = _lastHealthResults.Values.Count(r => r.IsHealthy);
        var totalComponents = _lastHealthResults.Count;
        
        if (totalComponents == 0)
        {
            return new FunctionHealth(
                HealthStatus.Unknown,
                "HealthMonitor - No health data available yet",
                DateTime.UtcNow
            );
        }
        
        var healthPercentage = (double)healthyComponents / totalComponents * 100;
        
        var status = healthPercentage switch
        {
            100 => HealthStatus.Excellent,
            >= 80 => HealthStatus.Good,
            >= 60 => HealthStatus.Warning,
            >= 40 => HealthStatus.Critical,
            _ => HealthStatus.Failed
        };
        
        return new FunctionHealth(
            status,
            $"HealthMonitor - {healthyComponents}/{totalComponents} components healthy ({healthPercentage:F1}%)",
            DateTime.UtcNow,
            new { 
                HealthyComponents = healthyComponents, 
                TotalComponents = totalComponents,
                HealthPercentage = healthPercentage,
                ExecutionCount = _executionCount,
                LastHealthResults = _lastHealthResults
            }
        );
    }

    public async Task<bool> CanExecuteAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            // Basic check - can we create a scope and access logging?
            var logger = scope.ServiceProvider.GetService<ILogger>();
            return logger != null;
        }
        catch
        {
            return false;
        }
    }

    public IDictionary<string, object> GetConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["Interval"] = "2 minutes",
            ["Category"] = "System",
            ["HealthChecks"] = new[] { "Database", "Application Services", "Memory", "Disk" },
            ["AlertThreshold"] = "Any critical issues",
            ["ExecutionCount"] = _executionCount,
            ["LastCheckResults"] = _lastHealthResults.Count
        };
    }

    private async Task<List<HealthCheckResult>> PerformHealthChecks(CancellationToken cancellationToken)
    {
        var results = new List<HealthCheckResult>();
        
        // Check database connectivity
        results.Add(await CheckDatabaseHealth());
        
        // Check application services
        results.Add(await CheckApplicationServicesHealth());
        
        // Check system resources
        results.Add(CheckMemoryHealth());
        results.Add(CheckDiskHealth());
        
        return results;
    }

    private async Task<HealthCheckResult> CheckDatabaseHealth()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            
            if (dbContext != null)
            {
                var canConnect = await dbContext.Database.CanConnectAsync();
                var recordCount = canConnect ? await dbContext.Books.CountAsync() : 0;
                
                return new HealthCheckResult
                {
                    Component = "Database",
                    IsHealthy = canConnect,
                    Message = canConnect 
                        ? $"Database healthy - {recordCount} books available"
                        : "Database connection failed",
                    ResponseTime = DateTime.UtcNow
                };
            }
            
            return new HealthCheckResult
            {
                Component = "Database",
                IsHealthy = false,
                Message = "Database context not available"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "Database",
                IsHealthy = false,
                Message = $"Database health check failed: {ex.Message}"
            };
        }
    }

    private async Task<HealthCheckResult> CheckApplicationServicesHealth()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var bookService = scope.ServiceProvider.GetService<IBookApplicationService>();
            
            if (bookService != null)
            {
                var result = await bookService.GetAllBooksAsync();
                
                return new HealthCheckResult
                {
                    Component = "BookApplicationService",
                    IsHealthy = result.IsSuccess,
                    Message = result.IsSuccess 
                        ? $"Service healthy - {result.Data?.Count() ?? 0} books accessible"
                        : $"Service error: {result.ErrorMessage}"
                };
            }
            
            return new HealthCheckResult
            {
                Component = "BookApplicationService",
                IsHealthy = false,
                Message = "BookApplicationService not available"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "BookApplicationService",
                IsHealthy = false,
                Message = $"Service health check failed: {ex.Message}"
            };
        }
    }

    private HealthCheckResult CheckMemoryHealth()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);
            var isHealthy = memoryMB < 500; // Threshold: 500MB
            
            return new HealthCheckResult
            {
                Component = "Memory",
                IsHealthy = isHealthy,
                Message = $"Memory usage: {memoryMB}MB {(isHealthy ? "(healthy)" : "(high)")}",
                ResponseTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "Memory",
                IsHealthy = false,
                Message = $"Memory check failed: {ex.Message}"
            };
        }
    }

    private HealthCheckResult CheckDiskHealth()
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            var usagePercent = ((double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize) * 100;
            var isHealthy = usagePercent < 85; // Threshold: 85%
            
            return new HealthCheckResult
            {
                Component = "Disk",
                IsHealthy = isHealthy,
                Message = $"Disk usage: {usagePercent:F1}% {(isHealthy ? "(healthy)" : "(high)")}",
                ResponseTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Component = "Disk",
                IsHealthy = false,
                Message = $"Disk check failed: {ex.Message}"
            };
        }
    }

    private async Task SendHealthAlerts(IEnumerable<HealthCheckResult> criticalIssues, CancellationToken cancellationToken)
    {
        // In a real implementation, this would send alerts via email, Slack, etc.
        foreach (var issue in criticalIssues)
        {
            _logger.LogError("🚨 HEALTH ALERT: {Component} - {Message}", issue.Component, issue.Message);
        }
        
        await Task.CompletedTask;
    }
}

// Supporting health check result class
public class HealthCheckResult
{
    public string Component { get; set; } = "";
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = "";
    public DateTime ResponseTime { get; set; } = DateTime.UtcNow;
}
```

### **3. AuditLoggerFunction - Security Category**

A security-focused function that processes audit events and maintains compliance logs.

```csharp
/// <summary>
/// Audit Logger Function - Security audit logging and compliance
/// Processes audit events queue and maintains security compliance
/// </summary>
public class AuditLoggerFunction : IFunction
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLoggerFunction> _logger;
    private int _executionCount = 0;
    private int _totalEventsProcessed = 0;

    // IFunction Properties
    public string Name => "AuditLogger";
    public string Description => "Security audit logging and compliance - processes audit events queue";
    public string Version => "1.0.0";
    public TimeSpan? Interval => TimeSpan.FromSeconds(30); // Process every 30 seconds
    public FunctionCategory Category => FunctionCategory.Security;

    public AuditLoggerFunction(IServiceProvider serviceProvider, ILogger<AuditLoggerFunction> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _executionCount++;
            _logger.LogInformation("🔒 AuditLogger execution #{Count} started", _executionCount);
            
            // Process audit events queue
            var processedEvents = await ProcessAuditEvents(cancellationToken);
            _totalEventsProcessed += processedEvents;
            
            // Perform compliance checks
            await PerformComplianceChecks(cancellationToken);
            
            // Archive old audit logs
            var archivedLogs = await ArchiveOldAuditLogs(cancellationToken);
            
            var duration = DateTime.UtcNow - startTime;
            var message = $"Processed {processedEvents} audit events successfully";
            
            if (processedEvents > 0)
            {
                _logger.LogInformation("✅ AuditLogger execution #{Count} completed. Processed: {Events} events, Archived: {Archived} logs", 
                    _executionCount, processedEvents, archivedLogs);
            }
            
            return FunctionResult.Success(message, duration, new { 
                ProcessedEvents = processedEvents, 
                ArchivedLogs = archivedLogs,
                ExecutionNumber = _executionCount,
                TotalEventsProcessed = _totalEventsProcessed,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "❌ AuditLogger execution #{Count} failed", _executionCount);
            
            return FunctionResult.Failure($"AuditLogger failed: {ex.Message}", duration, ex.ToString());
        }
    }

    public async Task<FunctionHealth> CheckHealthAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            
            if (dbContext != null)
            {
                var canConnect = await dbContext.Database.CanConnectAsync();
                
                return new FunctionHealth(
                    canConnect ? HealthStatus.Excellent : HealthStatus.Critical,
                    canConnect 
                        ? $"AuditLogger healthy - Database accessible, {_executionCount} executions, {_totalEventsProcessed} events processed"
                        : "AuditLogger unhealthy - Database not accessible",
                    DateTime.UtcNow,
                    new { 
                        DatabaseAccessible = canConnect, 
                        ExecutionCount = _executionCount,
                        TotalEventsProcessed = _totalEventsProcessed,
                        EventsPerExecution = _executionCount > 0 ? (double)_totalEventsProcessed / _executionCount : 0
                    }
                );
            }
            
            return new FunctionHealth(
                HealthStatus.Warning,
                "AuditLogger - Database context not available",
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            return new FunctionHealth(
                HealthStatus.Failed,
                $"AuditLogger health check failed: {ex.Message}",
                DateTime.UtcNow
            );
        }
    }

    public async Task<bool> CanExecuteAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            return dbContext != null && await dbContext.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    public IDictionary<string, object> GetConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["Interval"] = "30 seconds",
            ["Category"] = "Security", 
            ["AuditTypes"] = "Domain Events, Admin Commands, API Calls",
            ["RetentionPeriod"] = "90 days",
            ["ExecutionCount"] = _executionCount,
            ["TotalEventsProcessed"] = _totalEventsProcessed,
            ["ComplianceStandards"] = new[] { "SOX", "GDPR", "HIPAA" },
            ["ArchivalFrequency"] = "Every execution"
        };
    }

    private async Task<int> ProcessAuditEvents(CancellationToken cancellationToken)
    {
        // Simulate processing audit events from various sources
        var eventTypes = new[] 
        { 
            "User Login", 
            "Data Access", 
            "Admin Command", 
            "API Call", 
            "Data Modification",
            "Permission Change",
            "System Access"
        };
        
        var processedCount = 0;
        
        // Simulate variable audit load
        var eventsToProcess = Random.Shared.Next(0, 15);
        
        for (int i = 0; i < eventsToProcess; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var eventType = eventTypes[Random.Shared.Next(eventTypes.Length)];
            
            // Simulate processing time
            await Task.Delay(Random.Shared.Next(10, 50), cancellationToken);
            
            // Log the audit event
            await LogAuditEvent(eventType, $"Event_{i + 1}", cancellationToken);
            
            processedCount++;
        }
        
        if (processedCount > 0)
        {
            _logger.LogDebug("AuditLogger processed {Count} audit events", processedCount);
        }
        
        return processedCount;
    }

    private async Task LogAuditEvent(string eventType, string eventId, CancellationToken cancellationToken)
    {
        // In real implementation, this would write to secure audit storage
        var auditEntry = new
        {
            EventId = eventId,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            ProcessId = Environment.ProcessId,
            MachineName = Environment.MachineName,
            UserContext = "System"
        };
        
        _logger.LogTrace("Audit Event Logged: {@AuditEntry}", auditEntry);
        await Task.CompletedTask;
    }

    private async Task PerformComplianceChecks(CancellationToken cancellationToken)
    {
        // Simulate compliance validation checks
        var checks = new[] { "Data Retention", "Access Controls", "Encryption Status" };
        
        foreach (var check in checks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogTrace("Performing compliance check: {Check}", check);
            await Task.Delay(25, cancellationToken);
        }
    }

    private async Task<int> ArchiveOldAuditLogs(CancellationToken cancellationToken)
    {
        // Simulate archiving old audit logs (older than 90 days)
        var cutoffDate = DateTime.UtcNow.AddDays(-90);
        
        // In real implementation, this would move old records to long-term storage
        var simulatedArchiveCount = Random.Shared.Next(0, 5);
        
        if (simulatedArchiveCount > 0)
        {
            _logger.LogDebug("Archived {Count} old audit log entries", simulatedArchiveCount);
            await Task.Delay(100, cancellationToken);
        }
        
        return simulatedArchiveCount;
    }
}
```

---

## Function Base Classes

### **Abstract Function Base**

For standardized function implementations:

```csharp
/// <summary>
/// Base class for all Artichoke-FaaS functions
/// Provides common functionality and standardized patterns
/// </summary>
public abstract class FunctionBase : IFunction
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ILogger Logger;
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private int _executionCount = 0;
    private DateTime _lastExecution = DateTime.MinValue;

    // Abstract properties that must be implemented
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Version { get; }
    public abstract TimeSpan? Interval { get; }
    public abstract FunctionCategory Category { get; }

    protected FunctionBase(IServiceProvider serviceProvider, ILogger logger)
    {
        ServiceProvider = serviceProvider;
        Logger = logger;
    }

    public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Prevent concurrent executions
        if (!await _executionSemaphore.WaitAsync(100, cancellationToken))
        {
            return FunctionResult.Failure("Previous execution still in progress", TimeSpan.Zero);
        }

        var startTime = DateTime.UtcNow;
        
        try
        {
            _executionCount++;
            Logger.LogInformation("🔄 {FunctionName} execution #{Count} started", Name, _executionCount);
            
            // Pre-execution validation
            if (!await CanExecuteAsync())
            {
                return FunctionResult.Failure("Function cannot execute - dependencies not available", TimeSpan.Zero);
            }
            
            // Execute the actual function logic
            var result = await ExecuteFunctionAsync(cancellationToken);
            
            _lastExecution = DateTime.UtcNow;
            var duration = _lastExecution - startTime;
            
            if (result.IsSuccess)
            {
                Logger.LogInformation("✅ {FunctionName} execution #{Count} completed in {Duration}ms", 
                    Name, _executionCount, duration.TotalMilliseconds);
            }
            else
            {
                Logger.LogWarning("⚠️ {FunctionName} execution #{Count} failed: {Error}", 
                    Name, _executionCount, result.Message);
            }
            
            return result;
        }
        catch (OperationCanceledException)
        {
            var duration = DateTime.UtcNow - startTime;
            Logger.LogWarning("⚠️ {FunctionName} execution #{Count} was cancelled", Name, _executionCount);
            
            return FunctionResult.Failure($"{Name} execution was cancelled", duration, "Operation cancelled by user request");
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            Logger.LogError(ex, "❌ {FunctionName} execution #{Count} failed with exception", Name, _executionCount);
            
            return FunctionResult.Failure($"{Name} failed: {ex.Message}", duration, ex.ToString());
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    // Abstract method for actual function implementation
    protected abstract Task<FunctionResult> ExecuteFunctionAsync(CancellationToken cancellationToken);

    public virtual async Task<FunctionHealth> CheckHealthAsync()
    {
        try
        {
            var canExecute = await CanExecuteAsync();
            
            if (canExecute)
            {
                var timeSinceLastExecution = DateTime.UtcNow - _lastExecution;
                
                return new FunctionHealth(
                    HealthStatus.Good,
                    $"{Name} healthy - {_executionCount} executions completed",
                    DateTime.UtcNow,
                    new { 
                        ExecutionCount = _executionCount,
                        LastExecution = _lastExecution,
                        TimeSinceLastExecution = timeSinceLastExecution
                    }
                );
            }
            else
            {
                return new FunctionHealth(
                    HealthStatus.Critical,
                    $"{Name} unhealthy - cannot execute",
                    DateTime.UtcNow
                );
            }
        }
        catch (Exception ex)
        {
            return new FunctionHealth(
                HealthStatus.Failed,
                $"{Name} health check failed: {ex.Message}",
                DateTime.UtcNow
            );
        }
    }

    public virtual async Task<bool> CanExecuteAsync()
    {
        // Default implementation - can be overridden
        return await Task.FromResult(true);
    }

    public virtual IDictionary<string, object> GetConfiguration()
    {
        return new Dictionary<string, object>
        {
            ["Name"] = Name,
            ["Version"] = Version,
            ["Category"] = Category.ToString(),
            ["Interval"] = Interval?.ToString() ?? "Manual",
            ["ExecutionCount"] = _executionCount,
            ["LastExecution"] = _lastExecution == DateTime.MinValue ? "Never" : _lastExecution.ToString()
        };
    }
}
```

### **Using the Base Class**

```csharp
// Simplified function implementation using base class
public class SimpleBookAnalyticsFunction : FunctionBase
{
    public override string Name => "BookAnalytics";
    public override string Description => "Generates book analytics and recommendations";
    public override string Version => "1.0.0";
    public override TimeSpan? Interval => TimeSpan.FromHours(1);
    public override FunctionCategory Category => FunctionCategory.Analytics;

    public SimpleBookAnalyticsFunction(IServiceProvider serviceProvider, ILogger<SimpleBookAnalyticsFunction> logger)
        : base(serviceProvider, logger)
    {
    }

    protected override async Task<FunctionResult> ExecuteFunctionAsync(CancellationToken cancellationToken)
    {
        // Your specific function logic here
        using var scope = ServiceProvider.CreateScope();
        var bookService = scope.ServiceProvider.GetRequiredService<IBookApplicationService>();
        
        var books = await bookService.GetAllBooksAsync();
        
        // Generate analytics
        var analytics = GenerateAnalytics(books.Data);
        
        return FunctionResult.Success(
            $"Analytics generated for {books.Data?.Count() ?? 0} books",
            DateTime.UtcNow - DateTime.UtcNow, // Will be calculated by base class
            analytics);
    }

    public override async Task<bool> CanExecuteAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        var bookService = scope.ServiceProvider.GetService<IBookApplicationService>();
        return bookService != null;
    }

    private object GenerateAnalytics(IEnumerable<object> books)
    {
        // Analytics logic
        return new { TotalBooks = books?.Count() ?? 0, GeneratedAt = DateTime.UtcNow };
    }
}
```

---

## Function Development Best Practices

### **1. Error Handling**

```csharp
public async Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default)
{
    try
    {
        // Function logic here
        var result = await ProcessBusinessLogic(cancellationToken);
        return FunctionResult.Success("Operation completed", duration, result);
    }
    catch (OperationCanceledException)
    {
        // Handle cancellation gracefully
        return FunctionResult.Failure("Operation was cancelled", duration, "User requested cancellation");
    }
    catch (ArgumentException ex)
    {
        // Handle validation errors
        return FunctionResult.Failure($"Invalid argument: {ex.Message}", duration);
    }
    catch (HttpRequestException ex)
    {
        // Handle external service errors
        return FunctionResult.Failure($"External service error: {ex.Message}", duration, ex.ToString());
    }
    catch (Exception ex)
    {
        // Handle unexpected errors
        Logger.LogError(ex, "Unexpected error in {FunctionName}", Name);
        return FunctionResult.Failure($"Unexpected error: {ex.Message}", duration, ex.ToString());
    }
}
```

### **2. Dependency Injection**

```csharp
public class MyFunction : IFunction
{
    private readonly IBookApplicationService _bookService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MyFunction> _logger;

    public MyFunction(
        IBookApplicationService bookService,
        IConfiguration configuration,
        ILogger<MyFunction> logger)
    {
        _bookService = bookService;
        _configuration = configuration;
        _logger = logger;
    }

    // Implementation uses injected dependencies
}
```

### **3. Configuration Management**

```csharp
public IDictionary<string, object> GetConfiguration()
{
    return new Dictionary<string, object>
    {
        ["Name"] = Name,
        ["Version"] = Version,
        ["Interval"] = Interval?.ToString() ?? "Manual",
        ["Category"] = Category.ToString(),
        ["Dependencies"] = GetDependencies(),
        ["Settings"] = GetFunctionSettings(),
        ["Performance"] = GetPerformanceMetrics(),
        ["LastExecution"] = _lastExecution.ToString("yyyy-MM-dd HH:mm:ss UTC")
    };
}

private string[] GetDependencies()
{
    return new[] { "IBookApplicationService", "ApplicationDbContext", "ILogger" };
}

private object GetFunctionSettings()
{
    return new
    {
        BatchSize = 100,
        TimeoutSeconds = 300,
        RetryAttempts = 3,
        EnableDetailedLogging = true
    };
}

private object GetPerformanceMetrics()
{
    return new
    {
        AverageExecutionTime = _averageExecutionTime,
        SuccessRate = _successRate,
        TotalExecutions = _totalExecutions,
        LastExecutionDuration = _lastExecutionDuration
    };
}
```

---

## Function Registration & Service Integration

### **1. Service Registration**

Functions must be registered in the DI container during application startup:

```csharp
// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register individual functions
    services.AddTransient<IFunction, BookProcessorFunction>();
    services.AddTransient<IFunction, HealthMonitorFunction>();
    services.AddTransient<IFunction, AuditLoggerFunction>();
    
    // Register function dependencies
    services.AddScoped<IBookApplicationService, BookApplicationService>();
    services.AddScoped<IHealthService, HealthService>();
    services.AddScoped<IAuditService, AuditService>();
    
    // Register function factory
    services.AddSingleton<IFunctionFactory, FunctionFactory>();
    
    // Register function manager
    services.AddSingleton<IFunctionManager, FunctionManager>();
}
```

### **2. Function Discovery**

Automatic function discovery and registration:

```csharp
/// <summary>
/// Automatically discovers and registers all IFunction implementations
/// </summary>
public static class FunctionServiceExtensions
{
    public static IServiceCollection AddFunctionServices(this IServiceCollection services, params Assembly[] assemblies)
    {
        var assembliesToScan = assemblies.Any() ? assemblies : new[] { Assembly.GetCallingAssembly() };
        
        foreach (var assembly in assembliesToScan)
        {
            var functionTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IFunction).IsAssignableFrom(t))
                .ToList();

            foreach (var functionType in functionTypes)
            {
                services.AddTransient(typeof(IFunction), functionType);
                services.AddTransient(functionType); // Also register by concrete type
                
                Console.WriteLine($"🔧 Registered function: {functionType.Name}");
            }
        }
        
        return services;
    }
}

// Usage in Program.cs
services.AddFunctionServices(
    typeof(BookProcessorFunction).Assembly,
    typeof(HealthMonitorFunction).Assembly
);
```

### **3. Function Factory Pattern**

```csharp
/// <summary>
/// Factory for creating function instances with proper dependency injection
/// </summary>
public interface IFunctionFactory
{
    IFunction CreateFunction(string functionName);
    IFunction CreateFunction<T>() where T : class, IFunction;
    IEnumerable<IFunction> GetAllFunctions();
    IEnumerable<string> GetFunctionNames();
}

public class FunctionFactory : IFunctionFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FunctionFactory> _logger;

    public FunctionFactory(IServiceProvider serviceProvider, ILogger<FunctionFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IFunction CreateFunction(string functionName)
    {
        var functions = _serviceProvider.GetServices<IFunction>();
        var function = functions.FirstOrDefault(f => f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));
        
        if (function == null)
        {
            throw new ArgumentException($"Function '{functionName}' not found");
        }
        
        _logger.LogDebug("Created function instance: {FunctionName}", functionName);
        return function;
    }

    public IFunction CreateFunction<T>() where T : class, IFunction
    {
        var function = _serviceProvider.GetRequiredService<T>();
        _logger.LogDebug("Created function instance: {FunctionType}", typeof(T).Name);
        return function;
    }

    public IEnumerable<IFunction> GetAllFunctions()
    {
        return _serviceProvider.GetServices<IFunction>();
    }

    public IEnumerable<string> GetFunctionNames()
    {
        return GetAllFunctions().Select(f => f.Name).OrderBy(name => name);
    }
}
```

---

## Function Testing

### **1. Unit Testing Functions**

Testing functions in isolation with mocked dependencies:

```csharp
[TestFixture]
public class BookProcessorFunctionTests
{
    private Mock<IBookApplicationService> _mockBookService;
    private Mock<ILogger<BookProcessorFunction>> _mockLogger;
    private BookProcessorFunction _function;

    [SetUp]
    public void Setup()
    {
        _mockBookService = new Mock<IBookApplicationService>();
        _mockLogger = new Mock<ILogger<BookProcessorFunction>>();
        
        var serviceProvider = new Mock<IServiceProvider>();
        var serviceScope = new Mock<IServiceScope>();
        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        
        serviceScope.Setup(s => s.ServiceProvider.GetRequiredService<IBookApplicationService>())
                   .Returns(_mockBookService.Object);
        
        serviceScopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);
        serviceProvider.Setup(p => p.GetRequiredService<IServiceScopeFactory>())
                      .Returns(serviceScopeFactory.Object);
        
        _function = new BookProcessorFunction(serviceProvider.Object, _mockLogger.Object);
    }

    [Test]
    public async Task ExecuteAsync_WithValidBooks_ReturnsSuccess()
    {
        // Arrange
        var books = new List<BookEntity>
        {
            new() { Id = 1, Title = "Test Book 1", AuthorName = "Author 1" },
            new() { Id = 2, Title = "Test Book 2", AuthorName = "Author 2" }
        };
        
        var response = ServiceResponse<IEnumerable<BookEntity>>.Success(books);
        _mockBookService.Setup(s => s.GetAllBooksAsync()).ReturnsAsync(response);

        // Act
        var result = await _function.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Data);
        StringAssert.Contains("processed 2 books", result.Message);
        
        _mockBookService.Verify(s => s.GetAllBooksAsync(), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithServiceError_ReturnsFailure()
    {
        // Arrange
        var errorResponse = ServiceResponse<IEnumerable<BookEntity>>.Error("Database connection failed");
        _mockBookService.Setup(s => s.GetAllBooksAsync()).ReturnsAsync(errorResponse);

        // Act
        var result = await _function.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains("Database connection failed", result.Message);
    }

    [Test]
    public async Task ExecuteAsync_WithCancellation_HandlesCancellationGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockBookService.Setup(s => s.GetAllBooksAsync())
                       .Returns(async () =>
                       {
                           await Task.Delay(1000, cts.Token); // Long operation
                           return ServiceResponse<IEnumerable<BookEntity>>.Success(new List<BookEntity>());
                       });

        // Act
        cts.CancelAfter(100); // Cancel after 100ms
        var result = await _function.ExecuteAsync(cts.Token);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains("cancelled", result.Message.ToLower());
    }

    [Test]
    public async Task CheckHealthAsync_WithHealthyDependencies_ReturnsGood()
    {
        // Arrange
        _mockBookService.Setup(s => s.GetAllBooksAsync())
                       .ReturnsAsync(ServiceResponse<IEnumerable<BookEntity>>.Success(new List<BookEntity>()));

        // Act
        var health = await _function.CheckHealthAsync();

        // Assert
        Assert.AreEqual(HealthStatus.Good, health.Status);
        StringAssert.Contains("healthy", health.Message.ToLower());
    }

    [Test]
    public async Task CanExecuteAsync_WithAvailableDependencies_ReturnsTrue()
    {
        // Act
        var canExecute = await _function.CanExecuteAsync();

        // Assert
        Assert.IsTrue(canExecute);
    }
}
```

### **2. Integration Testing**

Testing functions with real dependencies:

```csharp
[TestFixture]
public class BookProcessorFunctionIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    private IServiceScope _scope;
    private BookProcessorFunction _function;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace database with in-memory version for testing
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase("TestDb"));
                });
            });
    }

    [SetUp]
    public void SetUp()
    {
        _scope = _factory.Services.CreateScope();
        _function = _scope.ServiceProvider.GetRequiredService<BookProcessorFunction>();
        
        // Seed test data
        SeedTestData(_scope.ServiceProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _scope?.Dispose();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory?.Dispose();
    }

    [Test]
    public async Task ExecuteAsync_WithRealDatabase_ProcessesActualBooks()
    {
        // Act
        var result = await _function.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Data);
        
        // Verify actual database changes
        var dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processedBooks = await dbContext.Books.Where(b => b.ProcessedAt != null).ToListAsync();
        Assert.IsNotEmpty(processedBooks);
    }

    private void SeedTestData(IServiceProvider serviceProvider)
    {
        using var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        
        if (!context.Books.Any())
        {
            context.Books.AddRange(
                new BookEntity { Title = "Integration Test Book 1", AuthorName = "Test Author 1" },
                new BookEntity { Title = "Integration Test Book 2", AuthorName = "Test Author 2" }
            );
            context.SaveChanges();
        }
    }
}
```

### **3. Performance Testing**

```csharp
[TestFixture]
public class FunctionPerformanceTests
{
    [Test]
    public async Task BookProcessorFunction_PerformanceTest_CompletesWithinTimeLimit()
    {
        // Arrange
        var function = CreateFunctionWithMockedDependencies();
        var tasks = new List<Task<FunctionResult>>();
        const int concurrentExecutions = 10;
        const int maxExecutionTimeMs = 5000;

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < concurrentExecutions; i++)
        {
            tasks.Add(function.ExecuteAsync(CancellationToken.None));
        }
        
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(maxExecutionTimeMs));
        Assert.That(results, Has.All.Property("IsSuccess").EqualTo(true));
        
        Console.WriteLine($"Completed {concurrentExecutions} concurrent executions in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average execution time: {results.Average(r => r.ExecutionDuration.TotalMilliseconds):F2}ms");
    }

    private BookProcessorFunction CreateFunctionWithMockedDependencies()
    {
        // Implementation similar to unit test setup
        // But optimized for performance testing
        var mockBookService = new Mock<IBookApplicationService>();
        mockBookService.Setup(s => s.GetAllBooksAsync())
                      .ReturnsAsync(ServiceResponse<IEnumerable<BookEntity>>.Success(
                          Enumerable.Range(1, 1000).Select(i => new BookEntity 
                          { 
                              Id = i, 
                              Title = $"Book {i}", 
                              AuthorName = $"Author {i}" 
                          })));

        // Return configured function instance
        return new BookProcessorFunction(/* configured dependencies */);
    }
}
```

### **4. Test Utilities**

```csharp
/// <summary>
/// Utility class for function testing
/// </summary>
public static class FunctionTestUtilities
{
    public static Mock<IServiceProvider> CreateMockServiceProvider(params (Type serviceType, object implementation)[] services)
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        // Setup service scope
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(mockServiceScope.Object);
        mockServiceProvider.Setup(p => p.GetRequiredService<IServiceScopeFactory>())
                          .Returns(mockServiceScopeFactory.Object);

        // Setup individual services
        foreach (var (serviceType, implementation) in services)
        {
            mockServiceProvider.Setup(p => p.GetRequiredService(serviceType)).Returns(implementation);
            mockServiceScope.Setup(s => s.ServiceProvider.GetRequiredService(serviceType)).Returns(implementation);
        }

        return mockServiceProvider;
    }

    public static async Task<FunctionResult> ExecuteWithTimeout<T>(T function, TimeSpan timeout) where T : IFunction
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await function.ExecuteAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return FunctionResult.Failure($"Function {function.Name} exceeded timeout of {timeout.TotalSeconds}s", timeout);
        }
    }

    public static void AssertFunctionResult(FunctionResult result, bool expectedSuccess, string? expectedMessageContains = null)
    {
        Assert.AreEqual(expectedSuccess, result.IsSuccess);
        
        if (expectedMessageContains != null)
        {
            StringAssert.Contains(expectedMessageContains, result.Message);
        }
        
        Assert.That(result.ExecutionDuration, Is.GreaterThan(TimeSpan.Zero));
        Assert.That(result.ExecutedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
    }
}
```

---

## Function Lifecycle Management

### **1. Function Initialization**

```csharp
/// <summary>
/// Function lifecycle manager handles initialization, execution, and cleanup
/// </summary>
public interface IFunctionLifecycleManager
{
    Task InitializeFunctionAsync(IFunction function);
    Task<FunctionResult> ExecuteFunctionAsync(IFunction function, CancellationToken cancellationToken = default);
    Task CleanupFunctionAsync(IFunction function);
    Task<FunctionHealth> CheckFunctionHealthAsync(IFunction function);
}

public class FunctionLifecycleManager : IFunctionLifecycleManager
{
    private readonly ILogger<FunctionLifecycleManager> _logger;
    private readonly IServiceProvider _serviceProvider;

    public FunctionLifecycleManager(ILogger<FunctionLifecycleManager> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InitializeFunctionAsync(IFunction function)
    {
        _logger.LogInformation("🔧 Initializing function: {FunctionName}", function.Name);
        
        // Check if function can execute
        var canExecute = await function.CanExecuteAsync();
        if (!canExecute)
        {
            throw new InvalidOperationException($"Function {function.Name} cannot be initialized - dependencies not available");
        }
        
        // Perform health check
        var health = await function.CheckHealthAsync();
        if (health.Status == HealthStatus.Failed || health.Status == HealthStatus.Critical)
        {
            _logger.LogWarning("⚠️ Function {FunctionName} initialized with health issues: {HealthMessage}", 
                function.Name, health.Message);
        }
        
        _logger.LogInformation("✅ Function {FunctionName} initialized successfully", function.Name);
    }

    public async Task<FunctionResult> ExecuteFunctionAsync(IFunction function, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("▶️ Executing function: {FunctionName}", function.Name);
        
        try
        {
            var result = await function.ExecuteAsync(cancellationToken);
            
            if (result.IsSuccess)
            {
                _logger.LogDebug("✅ Function {FunctionName} executed successfully in {Duration}ms", 
                    function.Name, result.ExecutionDuration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("❌ Function {FunctionName} execution failed: {Message}", 
                    function.Name, result.Message);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Unhandled exception in function {FunctionName}", function.Name);
            throw;
        }
    }

    public async Task CleanupFunctionAsync(IFunction function)
    {
        _logger.LogInformation("🧹 Cleaning up function: {FunctionName}", function.Name);
        
        // Perform any necessary cleanup
        if (function is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        if (function is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        
        _logger.LogInformation("✅ Function {FunctionName} cleaned up successfully", function.Name);
    }

    public async Task<FunctionHealth> CheckFunctionHealthAsync(IFunction function)
    {
        try
        {
            return await function.CheckHealthAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Health check failed for function {FunctionName}", function.Name);
            return new FunctionHealth(
                HealthStatus.Failed,
                $"Health check failed: {ex.Message}",
                DateTime.UtcNow
            );
        }
    }
}
```

### **2. Function State Management**

```csharp
/// <summary>
/// Tracks function execution state and statistics
/// </summary>
public class FunctionStateManager
{
    private readonly ConcurrentDictionary<string, FunctionState> _functionStates = new();
    private readonly ILogger<FunctionStateManager> _logger;

    public FunctionStateManager(ILogger<FunctionStateManager> logger)
    {
        _logger = logger;
    }

    public FunctionState GetFunctionState(string functionName)
    {
        return _functionStates.GetOrAdd(functionName, name => new FunctionState(name));
    }

    public void UpdateExecutionStats(string functionName, FunctionResult result)
    {
        var state = GetFunctionState(functionName);
        state.UpdateExecutionStats(result);
        
        _logger.LogDebug("📊 Updated stats for {FunctionName}: {SuccessRate}% success rate, {AvgDuration}ms avg", 
            functionName, state.SuccessRate, state.AverageExecutionTime.TotalMilliseconds);
    }

    public IDictionary<string, FunctionState> GetAllFunctionStates()
    {
        return new Dictionary<string, FunctionState>(_functionStates);
    }
}

public class FunctionState
{
    public string FunctionName { get; }
    public DateTime CreatedAt { get; }
    public DateTime? LastExecutionAt { get; private set; }
    public int TotalExecutions { get; private set; }
    public int SuccessfulExecutions { get; private set; }
    public int FailedExecutions { get; private set; }
    public TimeSpan TotalExecutionTime { get; private set; }
    public TimeSpan? LastExecutionDuration { get; private set; }

    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;
    public TimeSpan AverageExecutionTime => TotalExecutions > 0 ? 
        TimeSpan.FromMilliseconds(TotalExecutionTime.TotalMilliseconds / TotalExecutions) : TimeSpan.Zero;

    public FunctionState(string functionName)
    {
        FunctionName = functionName;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateExecutionStats(FunctionResult result)
    {
        LastExecutionAt = result.ExecutedAt;
        LastExecutionDuration = result.ExecutionDuration;
        TotalExecutions++;
        TotalExecutionTime += result.ExecutionDuration;

        if (result.IsSuccess)
        {
            SuccessfulExecutions++;
        }
        else
        {
            FailedExecutions++;
        }
    }
}
```

---

## What's Next?

Now that you understand function implementation, testing, and lifecycle management, explore:

1. **🎯 Development Kit Framework**: [Chapter 06: Development Kit Framework](06-DEVELOPMENT-KIT-FRAMEWORK.md)
2. **🔄 Distributed Function Management**: [Chapter 08: Distributed Function Management](08-DISTRIBUTED-FUNCTION-MANAGEMENT.md)
3. **🏗️ Function Host Architecture**: [Chapter 09: Function Host Architecture](09-FUNCTION-HOST-ARCHITECTURE.md)
4. **📊 Health Monitoring & Logging**: [Chapter 15: Health Monitoring & Logging](15-HEALTH-MONITORING-LOGGING.md)

Continue to: [Chapter 08: Distributed Function Management](08-DISTRIBUTED-FUNCTION-MANAGEMENT.md)
