using System.Text.Json;
using Artichoke.FaaS.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Artichoke.FaaS.Core.Base;

// ============================================================================
// v8.0.6 — CustomFunctionBase.
//
// Removed in v8.0.6 (legacy stubs that did Task.Delay(50)):
//   - CustomTriggerBase (replaced by Artichoke.FaaS.Runtime.Triggers.ITrigger + TimerTrigger
//     and DomainEventTrigger<TEvent>)
//   - HttpTrigger / TimerTrigger example classes (replaced by real implementations in Runtime)
//
// Kept: CustomFunctionBase — user functions extend this. Provides:
//   - GetParameter<T>(context, "key")  — read parameters from FunctionExecutionContext
//   - CreateSchema(object)             — build a JsonDocument schema from anonymous object
//   - SafeExecuteAsync(...)            — try/catch wrapper that times execution
//   - DefaultLogger                    — fallback ILogger that writes to Console if no DI logger
// ============================================================================

/// <summary>
/// Base class for custom function development. User functions extend this and implement
/// <see cref="FunctionType"/> and <see cref="ExecuteAsync(FunctionExecutionContext)"/>.
/// </summary>
public abstract class CustomFunctionBase : ICustomFunction
{
    protected ILogger Logger { get; private set; } = null!;
    protected bool IsInitialized { get; private set; }

    public abstract string FunctionType { get; }
    public abstract Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context);

    public virtual Task<ValidationResult> ValidateInputAsync(object input)
        => Task.FromResult(ValidationResult.Success());

    public abstract JsonDocument GetInputSchema();
    public abstract JsonDocument GetOutputSchema();

    protected virtual Task OnInitializeAsync()
    {
        Logger = GetLogger();
        IsInitialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Read a parameter from the context. Handles JsonElement (from HTTP body deserialization),
    /// IDictionary<string, object> (from in-process callers), and direct casts.
    /// </summary>
    protected T GetParameter<T>(FunctionExecutionContext context, string name, T defaultValue = default!)
    {
        if (context.Parameters.TryGetValue(name, out var value))
        {
            try
            {
                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Failed to convert parameter {Name} to type {Type}", name, typeof(T).Name);
                return defaultValue;
            }
        }
        return defaultValue;
    }

    /// <summary>Build a JsonDocument schema from an anonymous object.</summary>
    protected JsonDocument CreateSchema(object schemaObject)
    {
        var json = JsonSerializer.Serialize(schemaObject, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return JsonDocument.Parse(json);
    }

    /// <summary>Try/catch wrapper that times execution. Optional — the host already does this.</summary>
    protected async Task<FunctionExecutionResult> SafeExecuteAsync(
        FunctionExecutionContext context,
        Func<FunctionExecutionContext, Task<FunctionExecutionResult>> executeFunc)
    {
        if (!IsInitialized)
        {
            await OnInitializeAsync();
        }

        try
        {
            var startTime = DateTime.UtcNow;
            var result = await executeFunc(context);
            var duration = DateTime.UtcNow - startTime;
            return result with { Duration = duration };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Function execution failed for {FunctionType}", FunctionType);
            return FunctionExecutionResult.Failure($"Execution failed: {ex.Message}", ex.ToString());
        }
    }

    private ILogger GetLogger() => new DefaultLogger(FunctionType);
}

/// <summary>
/// Fallback logger for CustomFunctionBase when no DI logger is wired up. Writes to Console.
/// </summary>
internal sealed class DefaultLogger : ILogger
{
    private readonly string _categoryName;
    public DefaultLogger(string categoryName) => _categoryName = categoryName;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"[{timestamp}] [{logLevel}] [{_categoryName}] {message}");
        if (exception != null) Console.WriteLine($"Exception: {exception}");
    }
}
