using System.Text.Json;

namespace Artichoke.FaaS.Core.Interfaces;

// ============================================================================
// v8.0.6 — Function hosting contracts.
//
// Split into three focused interfaces so callers pay only for what they use:
//   - IFunctionRegistry : discovery (List, Get, IsRegistered, Register)
//   - IFunctionInvoker  : invocation (Execute by reference — zero host overhead)
//   - IFunctionHost     : combines both, plus Execute-by-name (lookup + invoke)
//
// The "fast path" is IFunctionInvoker.ExecuteAsync(ICustomFunction, context):
// no Dictionary lookup, no logging, no timing — just a try/catch around the
// function's ExecuteAsync. Use this when the caller already has the function
// reference (e.g. EventPublisher, which knows at compile time which functions
// it cares about).
//
// The "slow path" is IFunctionHost.ExecuteAsync(functionType, input, options):
// looks up the function by type, optionally logs/times/captures errors based
// on FunctionExecutionOptions. Use this for HTTP endpoints and any caller
// that discovers functions dynamically.
// ============================================================================

/// <summary>
/// Registry of ICustomFunction instances, keyed by FunctionType.
/// Pure discovery — no invocation.
/// </summary>
public interface IFunctionRegistry
{
    /// <summary>Get all registered function types.</summary>
    IReadOnlyCollection<string> List();

    /// <summary>Look up a function by type. Returns null if not registered.</summary>
    ICustomFunction? Get(string functionType);

    /// <summary>Check whether a function type is registered.</summary>
    bool IsRegistered(string functionType);

    /// <summary>Register a function instance.</summary>
    void Register(ICustomFunction function);

    /// <summary>Register a function by type (instantiated via ActivatorUtilities).</summary>
    void Register<TFunction>() where TFunction : class, ICustomFunction;

    /// <summary>Register a function by Type (instantiated via ActivatorUtilities).</summary>
    void Register(Type functionType);
}

/// <summary>
/// Invoker for ICustomFunction instances. The fast path — caller already has the function.
/// Zero host overhead: no lookup, no logging, no timing (unless options request it).
/// </summary>
public interface IFunctionInvoker
{
    /// <summary>
    /// Execute a function the caller already has a reference to. The fastest path.
    /// Default behavior: try/catch around function.ExecuteAsync, returns Failure on exception.
    /// Pass options to enable logging/timing/error-capture.
    /// </summary>
    Task<FunctionExecutionResult> ExecuteAsync(
        ICustomFunction function,
        FunctionExecutionContext context,
        FunctionExecutionOptions? options = null);
}

/// <summary>
/// Combined host: registry + invoker + execute-by-name.
/// Use this when you need discovery (HTTP endpoints, CLI tools, dynamic dispatch).
/// </summary>
public interface IFunctionHost : IFunctionRegistry, IFunctionInvoker
{
    /// <summary>
    /// Look up a function by type and execute it. The "slow path" — pays for Dictionary
    /// lookup + (optional) logging/timing/error-capture. Use IFunctionInvoker.ExecuteAsync
    /// directly if you already have the function reference.
    /// </summary>
    Task<FunctionExecutionResult> ExecuteAsync(
        string functionType,
        object? input = null,
        FunctionExecutionOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Opt-in cross-cutting concerns for function execution.
/// All defaults are <c>false</c> (zero overhead) — opt in per call site.
/// </summary>
public sealed class FunctionExecutionOptions
{
    /// <summary>Log execution start/finish via ILogger&lt;FunctionHost&gt;.</summary>
    public bool EnableLogging { get; set; }

    /// <summary>Measure execution duration with Stopwatch and attach to result.Duration.</summary>
    public bool EnableTiming { get; set; }

    /// <summary>Catch exceptions and convert to FunctionExecutionResult.Failure.</summary>
    /// <remarks>Default is <c>true</c> because letting exceptions bubble is rarely what you want.</remarks>
    public bool EnableErrorCapture { get; set; } = true;

    /// <summary>Project namespace to attach to the context (for multi-tenant hosts).</summary>
    public string? ProjectNamespace { get; set; }

    /// <summary>Source label for the execution (HTTP, CLI, Trigger, etc.) — used in logging.</summary>
    public string? ExecutionSource { get; set; }

    /// <summary>Pre-built context (advanced — bypasses the input→context conversion).</summary>
    public FunctionExecutionContext? Context { get; set; }

    /// <summary>Default options: zero overhead (only error capture, since that's almost always wanted).</summary>
    public static FunctionExecutionOptions Default => new();

    /// <summary>Full observability: logging + timing + error capture.</summary>
    public static FunctionExecutionOptions FullObservability => new()
    {
        EnableLogging = true,
        EnableTiming = true,
        EnableErrorCapture = true
    };

    /// <summary>Bare metal: no logging, no timing, no error capture. Fastest possible path.</summary>
    public static FunctionExecutionOptions None => new() { EnableErrorCapture = false };
}
