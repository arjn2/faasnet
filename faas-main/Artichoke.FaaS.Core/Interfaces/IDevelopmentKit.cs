using System.Text.Json;

namespace Artichoke.FaaS.Core.Interfaces;

// ============================================================================
// v8.0.6 — Core interfaces for user-written functions.
//
// Removed in v8.0.6 (replaced by cleaner abstractions in IFunctionHost.cs and ITrigger.cs):
//   - ITrigger / ITriggerFactory         → see ITrigger.cs
//   - ICustomFunctionFactory             → use IFunctionRegistry.Register() / DI registration
//
// Kept: ICustomFunction + the execution context/result types (used by CustomFunctionBase
// and by user functions everywhere).
// ============================================================================

/// <summary>
/// Interface for user-written functions. Implement this (or extend CustomFunctionBase) and
/// register with the framework via services.AddArtichokeFaaS(b => b.RegisterFunction&lt;MyFunc&gt;()).
/// </summary>
public interface ICustomFunction
{
    /// <summary>Unique function type identifier (e.g. "BMS.Audit").</summary>
    string FunctionType { get; }

    /// <summary>Execute the function with the given context.</summary>
    Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context);

    /// <summary>Validate the input before execution. Return Failure to skip execution.</summary>
    Task<ValidationResult> ValidateInputAsync(object input);

    /// <summary>JSON schema for the input payload (for documentation / UI).</summary>
    JsonDocument GetInputSchema();

    /// <summary>JSON schema for the output payload.</summary>
    JsonDocument GetOutputSchema();
}

/// <summary>
/// Execution context passed to functions. Carries input, parameters, metadata, cancellation,
/// and the DI service provider (so functions can resolve scoped services).
/// </summary>
public record FunctionExecutionContext
{
    public string ProjectNamespace { get; init; } = string.Empty;
    public string FunctionName { get; init; } = string.Empty;
    public object Input { get; init; } = new();
    public Dictionary<string, object> Parameters { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public CancellationToken CancellationToken { get; init; }
    public IServiceProvider ServiceProvider { get; init; } = null!;
    public string? ExecutionSource { get; init; } // HTTP, CLI, Trigger, Manual, etc.
}

/// <summary>
/// Result of function execution.
/// </summary>
public record FunctionExecutionResult
{
    public bool IsSuccess { get; init; }
    public object? Output { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public string? ErrorDetails { get; init; }

    public static FunctionExecutionResult Success(object? output, string message = "Success", TimeSpan duration = default)
        => new() { IsSuccess = true, Output = output, Message = message, Duration = duration };

    public static FunctionExecutionResult Failure(string message, string? errorDetails = null, TimeSpan duration = default)
        => new() { IsSuccess = false, Message = message, ErrorDetails = errorDetails, Duration = duration };
}

/// <summary>
/// Validation result returned by ICustomFunction.ValidateInputAsync.
/// </summary>
public record ValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
}
