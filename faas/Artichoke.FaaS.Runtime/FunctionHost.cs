using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Artichoke.FaaS.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artichoke.FaaS.Runtime;

/// <summary>
/// Default IFunctionHost implementation: in-process registry + fast-path invoker.
///
/// Two execution paths:
///   - <see cref="ExecuteAsync(ICustomFunction, FunctionExecutionContext, FunctionExecutionOptions?)"/>
///     (the fast path — caller already has the function). Zero host overhead by default.
///   - <see cref="ExecuteAsync(string, object?, FunctionExecutionOptions?, CancellationToken)"/>
///     (the slow path — looks up by type, then calls the fast path).
///
/// Functions are registered via:
///   - DI: register ICustomFunction implementations as services.AddSingleton&lt;ICustomFunction, MyFunc&gt;()
///     and the host picks them up via IEnumerable&lt;ICustomFunction&gt;.
///   - Explicit Register(...) calls.
/// </summary>
public class FunctionHost : IFunctionHost
{
    private readonly ConcurrentDictionary<string, ICustomFunction> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FunctionHost>? _logger;

    public FunctionHost(
        IEnumerable<ICustomFunction> registeredFunctions,
        IServiceProvider serviceProvider,
        ILogger<FunctionHost>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;

        foreach (var function in registeredFunctions ?? Enumerable.Empty<ICustomFunction>())
        {
            Register(function);
        }
    }

    // ===== IFunctionRegistry =====

    public IReadOnlyCollection<string> List() => _functions.Keys.OrderBy(k => k).ToList();

    public ICustomFunction? Get(string functionType)
        => string.IsNullOrWhiteSpace(functionType) ? null : _functions.GetValueOrDefault(functionType);

    public bool IsRegistered(string functionType)
        => !string.IsNullOrWhiteSpace(functionType) && _functions.ContainsKey(functionType);

    public void Register(ICustomFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var key = function.FunctionType;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException($"Function of type {function.GetType().FullName} returned an empty FunctionType.");

        if (_functions.TryAdd(key, function))
        {
            _logger?.LogInformation("Registered function '{FunctionType}' ({ImplementationType})", key, function.GetType().Name);
        }
        else
        {
            _logger?.LogWarning("Function '{FunctionType}' already registered; replacing.", key);
            _functions[key] = function;
        }
    }

    public void Register<TFunction>() where TFunction : class, ICustomFunction
        => Register(typeof(TFunction));

    public void Register(Type functionType)
    {
        ArgumentNullException.ThrowIfNull(functionType);
        if (!typeof(ICustomFunction).IsAssignableFrom(functionType))
            throw new ArgumentException($"{functionType.FullName} does not implement ICustomFunction.");
        if (functionType.IsAbstract || functionType.IsInterface)
            throw new ArgumentException($"{functionType.FullName} is abstract; cannot instantiate.");

        var instance = (ICustomFunction)ActivatorUtilities.CreateInstance(_serviceProvider, functionType);
        Register(instance);
    }

    // ===== IFunctionInvoker (fast path) =====

    public Task<FunctionExecutionResult> ExecuteAsync(
        ICustomFunction function,
        FunctionExecutionContext context,
        FunctionExecutionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(context);
        options ??= FunctionExecutionOptions.Default;

        return ExecuteInternalAsync(function, context, options);
    }

    // ===== IFunctionHost (slow path: lookup + invoke) =====

    public Task<FunctionExecutionResult> ExecuteAsync(
        string functionType,
        object? input = null,
        FunctionExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(functionType))
        {
            return Task.FromResult(FunctionExecutionResult.Failure("Function type is required."));
        }

        var function = Get(functionType);
        if (function is null)
        {
            var available = _functions.Count == 0 ? "(none)" : string.Join(", ", _functions.Keys);
            _logger?.LogWarning("Function '{FunctionType}' not registered. Available: {Available}", functionType, available);
            return Task.FromResult(FunctionExecutionResult.Failure(
                $"Function '{functionType}' is not registered.",
                $"Available: {available}"));
        }

        options ??= FunctionExecutionOptions.Default;
        var context = options.Context ?? BuildContext(functionType, input, options, cancellationToken);
        return ExecuteInternalAsync(function, context, options);
    }

    // ===== internals =====

    private async Task<FunctionExecutionResult> ExecuteInternalAsync(
        ICustomFunction function,
        FunctionExecutionContext context,
        FunctionExecutionOptions options)
    {
        var sw = options.EnableTiming ? Stopwatch.StartNew() : null;

        if (options.EnableLogging)
        {
            _logger?.LogInformation("Executing function '{FunctionType}' (source={Source})",
                function.FunctionType, options.ExecutionSource ?? context.ExecutionSource ?? "Manual");
        }

        // Best-effort input validation. Skip if disabled for max speed.
        if (options.EnableErrorCapture)
        {
            try
            {
                var validation = await function.ValidateInputAsync(context.Input);
                if (validation is { IsValid: false, Errors.Count: > 0 })
                {
                    var msg = string.Join("; ", validation.Errors);
                    sw?.Stop();
                    return FunctionExecutionResult.Failure($"Input validation failed: {msg}");
                }
            }
            catch (Exception vex)
            {
                // Validation threw — log and continue with execution.
                _logger?.LogWarning(vex, "Function '{FunctionType}' ValidateInputAsync threw; continuing.", function.FunctionType);
            }
        }

        try
        {
            var result = await function.ExecuteAsync(context);
            sw?.Stop();

            if (options.EnableLogging)
            {
                _logger?.LogInformation("Function '{FunctionType}' completed in {Duration}ms — success={Success}",
                    function.FunctionType, sw?.Elapsed.TotalMilliseconds ?? 0, result.IsSuccess);
            }

            // Attach measured duration if timing was on and the function didn't set one.
            if (options.EnableTiming && sw is not null && result.Duration == TimeSpan.Zero)
            {
                result = result with { Duration = sw.Elapsed };
            }
            return result;
        }
        catch (OperationCanceledException oce) when (context.CancellationToken.IsCancellationRequested)
        {
            sw?.Stop();
            return FunctionExecutionResult.Failure("Execution was cancelled.", oce.Message, sw?.Elapsed ?? TimeSpan.Zero);
        }
        catch (Exception ex) when (options.EnableErrorCapture)
        {
            sw?.Stop();
            _logger?.LogError(ex, "Function '{FunctionType}' threw an unhandled exception.", function.FunctionType);
            return FunctionExecutionResult.Failure(
                $"Function '{function.FunctionType}' failed: {ex.Message}",
                ex.ToString(),
                sw?.Elapsed ?? TimeSpan.Zero);
        }
        // If error capture is disabled and an exception is thrown, it bubbles up — caller's choice.
    }

    private static FunctionExecutionContext BuildContext(
        string functionType,
        object? input,
        FunctionExecutionOptions options,
        CancellationToken cancellationToken)
    {
        // Normalize input → Parameters so CustomFunctionBase.GetParameter<T> works.
        var parameters = new Dictionary<string, object>();
        switch (input)
        {
            case IDictionary<string, object> dict:
                foreach (var kv in dict) parameters[kv.Key] = kv.Value;
                break;
            case JsonElement je when je.ValueKind == JsonValueKind.Object:
                foreach (var prop in je.EnumerateObject()) parameters[prop.Name] = prop.Value;
                break;
            case string s when s.TrimStart().StartsWith('{'):
                try
                {
                    using var doc = JsonDocument.Parse(s);
                    foreach (var prop in doc.RootElement.EnumerateObject()) parameters[prop.Name] = prop.Value;
                }
                catch (JsonException) { /* not JSON; input just stays in Input */ }
                break;
        }

        return new FunctionExecutionContext
        {
            ProjectNamespace = options.ProjectNamespace ?? "",
            FunctionName = functionType,
            Input = input ?? new(),
            Parameters = parameters,
            Metadata = new(),
            CancellationToken = cancellationToken,
            ServiceProvider = options.Context?.ServiceProvider ?? null!,
            ExecutionSource = options.ExecutionSource ?? "Manual"
        };
    }
}
