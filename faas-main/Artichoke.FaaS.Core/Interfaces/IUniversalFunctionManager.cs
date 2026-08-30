using Microsoft.Extensions.Logging;

namespace Artichoke.FaaS.Core.Interfaces;

/// <summary>
/// Universal Function Interface - Can be implemented by ANY .NET project
/// This is the contract that all functions must follow regardless of project
/// </summary>
public interface IUniversalFunction
{
    /// <summary>
    /// Unique function name within the project
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this function does
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Function version for deployment tracking
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Project namespace this function belongs to (e.g., "BMS", "ECommerce", "CRM")
    /// </summary>
    string ProjectNamespace { get; }

    /// <summary>
    /// Function category for organization
    /// </summary>
    FunctionCategory Category { get; }

    /// <summary>
    /// Execution interval (null for on-demand functions)
    /// </summary>
    TimeSpan? Interval { get; }

    /// <summary>
    /// Execute the function logic
    /// </summary>
    Task<FunctionResult> ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the function can execute (dependency validation)
    /// </summary>
    Task<bool> CanExecuteAsync();

    /// <summary>
    /// Get function health status
    /// </summary>
    Task<FunctionHealth> CheckHealthAsync();

    /// <summary>
    /// Get function configuration metadata
    /// </summary>
    IDictionary<string, object> GetConfiguration();

    /// <summary>
    /// Initialize function with project-specific services
    /// </summary>
    Task InitializeAsync(IServiceProvider serviceProvider);

    /// <summary>
    /// Cleanup resources when function is stopped
    /// </summary>
    Task DisposeAsync();
}

/// <summary>
/// Universal Function Manager - Manages functions across ALL projects
/// </summary>
public interface IUniversalFunctionManager
{
    // Project Management
    Task<ProjectInfo[]> GetProjectsAsync();
    Task<ProjectInfo?> GetProjectAsync(string projectNamespace);
    Task RegisterProjectAsync(ProjectInfo project);
    Task UnregisterProjectAsync(string projectNamespace);

    // Function Management (Multi-Project)
    Task<FunctionInfo[]> GetAllFunctionsAsync();
    Task<FunctionInfo[]> GetProjectFunctionsAsync(string projectNamespace);
    Task<FunctionInfo?> GetFunctionAsync(string projectNamespace, string functionName);
    
    // Function Lifecycle
    Task<FunctionResult> StartFunctionAsync(string projectNamespace, string functionName, string adminUser);
    Task<FunctionResult> StopFunctionAsync(string projectNamespace, string functionName, string adminUser);
    Task<FunctionResult> RestartFunctionAsync(string projectNamespace, string functionName, string adminUser);
    Task<FunctionResult> ExecuteFunctionAsync(string projectNamespace, string functionName, string adminUser);

    // System Management
    Task<SystemHealthStatus> GetSystemHealthAsync();
    Task<FunctionResult> HealSystemAsync(string adminUser);
    Task<FunctionResult> OptimizeSystemAsync(string adminUser);
    Task<FunctionResult> DiagnoseSystemAsync();
}

/// <summary>
/// Project Manager - Handles multi-project isolation
/// </summary>
public interface IProjectManager
{
    Task<ProjectInfo[]> GetProjectsAsync();
    Task<ProjectInfo?> GetProjectAsync(string projectNamespace);
    Task<ProjectInfo> RegisterProjectAsync(string projectNamespace, string displayName, string description, string contactEmail);
    Task UnregisterProjectAsync(string projectNamespace);
    Task<bool> IsProjectRegisteredAsync(string projectNamespace);
}

/// <summary>
/// Function Orchestrator - Coordinates function execution across projects
/// </summary>
public interface IFunctionOrchestrator
{
    Task<FunctionResult> OrchestrateExecutionAsync(string projectNamespace, string functionName, FunctionCommand command);
    Task<FunctionResult[]> ExecuteWorkflowAsync(string projectNamespace, WorkflowDefinition workflow);
    Task<bool> CanExecuteFunctionAsync(string projectNamespace, string functionName);
}

/// <summary>
/// Process Manager - Manages function host processes
/// </summary>
public interface IProcessManager
{
    Task<ProcessInfo[]> GetActiveProcessesAsync();
    Task<ProcessInfo?> GetProcessAsync(string projectNamespace, string functionName);
    Task<FunctionResult> StartProcessAsync(string projectNamespace, string functionName, ProcessStartOptions options);
    Task<FunctionResult> StopProcessAsync(string projectNamespace, string functionName);
    Task<FunctionResult> RestartProcessAsync(string projectNamespace, string functionName);
}

/// <summary>
/// Health Manager - Monitors health across all projects
/// </summary>
public interface IHealthManager
{
    Task<SystemHealthStatus> GetOverallHealthAsync();
    Task<ProjectHealthStatus> GetProjectHealthAsync(string projectNamespace);
    Task<FunctionHealth> GetFunctionHealthAsync(string projectNamespace, string functionName);
    Task<FunctionResult> HealProjectAsync(string projectNamespace, string adminUser);
    Task<FunctionResult> HealFunctionAsync(string projectNamespace, string functionName, string adminUser);
}

/// <summary>
/// Function categories for organization
/// </summary>
public enum FunctionCategory
{
    Business,      // Business logic functions
    System,        // System maintenance functions
    Integration,   // External system integration
    Security,      // Security and compliance
    Analytics,     // Data analysis and reporting
    Automation,    // Workflow automation
    Monitoring,    // System monitoring
    Custom         // Custom project-specific categories
}

/// <summary>
/// Function execution result
/// </summary>
public record FunctionResult(
    bool IsSuccess,
    string Message,
    TimeSpan ExecutionDuration,
    DateTime ExecutedAt,
    object? Data = null,
    string? ErrorDetails = null
)
{
    public static FunctionResult Success(string message, TimeSpan duration, object? data = null) =>
        new(true, message, duration, DateTime.UtcNow, data);

    public static FunctionResult Failure(string message, TimeSpan duration, string? errorDetails = null) =>
        new(false, message, duration, DateTime.UtcNow, null, errorDetails);
}

/// <summary>
/// Function health status
/// </summary>
public record FunctionHealth(
    HealthStatus Status,
    string Message,
    DateTime CheckedAt,
    object? Metadata = null
);

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    Unknown,
    Failed,
    Critical,
    Warning,
    Good,
    Excellent
}

/// <summary>
/// Project information
/// </summary>
public record ProjectInfo(
    string ProjectNamespace,
    string DisplayName,
    string Description,
    string ContactEmail,
    DateTime RegisteredAt,
    bool IsActive,
    int FunctionCount,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Function information with project context
/// </summary>
public record FunctionInfo(
    string ProjectNamespace,
    string Name,
    string Description,
    string Version,
    FunctionCategory Category,
    FunctionStatus Status,
    DateTime? LastExecuted,
    DateTime? NextExecution,
    int ExecutionCount,
    TimeSpan AverageExecutionTime,
    string? Schedule,
    IDictionary<string, object> Configuration
);

/// <summary>
/// Function status
/// </summary>
public enum FunctionStatus
{
    Registered,
    Scheduled,
    Running,
    Completed,
    Failed,
    Stopped,
    Queued,
    Disabled
}

/// <summary>
/// Process information
/// </summary>
public record ProcessInfo(
    string ProjectNamespace,
    string FunctionName,
    int ProcessId,
    string HostName,
    DateTime StartTime,
    ProcessStatus Status,
    Dictionary<string, object> Metadata
);

/// <summary>
/// Process status
/// </summary>
public enum ProcessStatus
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Unknown
}

/// <summary>
/// System health status across all projects
/// </summary>
public record SystemHealthStatus(
    HealthStatus OverallStatus,
    DateTime CheckedAt,
    ProjectHealthStatus[] Projects,
    SystemMetrics Metrics,
    string[] Recommendations,
    SystemAlert[] Alerts
);

/// <summary>
/// Project-specific health status
/// </summary>
public record ProjectHealthStatus(
    string ProjectNamespace,
    HealthStatus Status,
    FunctionHealth[] Functions,
    DateTime CheckedAt
);

/// <summary>
/// System metrics
/// </summary>
public record SystemMetrics(
    double CpuUsage,
    double MemoryUsage,
    double DiskUsage,
    int ActiveProjects,
    int ActiveFunctions,
    int QueuedFunctions,
    TimeSpan SystemUptime,
    long DatabaseSize,
    int ActiveConnections,
    double RequestsPerSecond
);

/// <summary>
/// System alert
/// </summary>
public record SystemAlert(
    string Source,
    AlertSeverity Severity,
    string Message,
    DateTime Timestamp,
    string? RecommendedAction = null
);

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
    Emergency
}

/// <summary>
/// Function command for external communication
/// </summary>
public record FunctionCommand(
    string ProjectNamespace,
    string FunctionName,
    FunctionCommandType Type,
    string? AdminUser = null,
    Dictionary<string, object>? Parameters = null
);

/// <summary>
/// Function command types
/// </summary>
public enum FunctionCommandType
{
    Execute,
    HealthCheck,
    Stop,
    Restart,
    Configure
}

/// <summary>
/// Process start options
/// </summary>
public record ProcessStartOptions(
    string WorkingDirectory,
    Dictionary<string, string>? EnvironmentVariables = null,
    string? Arguments = null,
    bool RedirectOutput = true,
    int TimeoutSeconds = 300
);

/// <summary>
/// Workflow definition for function chains
/// </summary>
public record WorkflowDefinition(
    string Name,
    WorkflowStep[] Steps,
    bool StopOnFailure = true
);

/// <summary>
/// Workflow step
/// </summary>
public record WorkflowStep(
    string FunctionName,
    Dictionary<string, object>? Parameters = null,
    WorkflowStep[]? Dependencies = null
);

/// <summary>
/// Function log entry
/// </summary>
public record FunctionLogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    string FunctionName,
    string CorrelationId,
    string? Exception = null
);

/// <summary>
/// Function metrics
/// </summary>
public record FunctionMetrics(
    string FunctionName,
    TimeSpan Window,
    int TotalExecutions,
    int SuccessfulExecutions,
    int FailedExecutions,
    TimeSpan AverageExecutionTime,
    TimeSpan FastestExecution,
    TimeSpan SlowestExecution,
    DateTime LastExecution,
    double SuccessRate,
    int QueueDepth,
    double ThroughputPerMinute
);

/// <summary>
/// Queued function
/// </summary>
public record QueuedFunction(
    string ProjectNamespace,
    string FunctionName,
    DateTime QueuedAt,
    int Priority,
    Dictionary<string, object>? Parameters = null
);