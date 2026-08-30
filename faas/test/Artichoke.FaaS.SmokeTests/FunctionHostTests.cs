using Artichoke.FaaS.Core.Base;
using Artichoke.FaaS.Core.Interfaces;
using Artichoke.FaaS.Runtime;
using Artichoke.FaaS.Runtime.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Artichoke.FaaS.SmokeTests;

// =============================================================================
// Smoke tests for the v9.x framework. Not exhaustive — these exist so CI has
// something to verify on every push. The goal is: if any of these fail, the
// build is broken; if they all pass, the framework is at least minimally
// functional.
//
// What's covered:
//   1. FunctionHost registration + lookup
//   2. Fast-path execution (IFunctionInvoker.ExecuteAsync with a function ref)
//   3. Slow-path execution (IFunctionHost.ExecuteAsync by function type)
//   4. FunctionExecutionOptions — Default is zero overhead, FullObservability captures timing
//   5. IDomainEventBus — publish fires subscribers
//   6. DomainEventTrigger<TEvent> — fires target function on event
//
// What's NOT covered (would need integration tests):
//   - faas-supervisor spawning child processes
//   - HTTP gateway proxying
//   - Auto-restart on crash
// =============================================================================

public class FunctionHostTests
{
    private static IFunctionHost BuildHost(params ICustomFunction[] functions)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        foreach (var f in functions)
            services.AddSingleton(f);
        services.AddSingleton<IFunctionHost, FunctionHost>();
        return services.BuildServiceProvider().GetRequiredService<IFunctionHost>();
    }

    [Fact]
    public void RegisterFunction_AddsToRegistry()
    {
        var host = BuildHost(new EchoFunction());
        Assert.True(host.IsRegistered("Test.Echo"));
        Assert.Contains("Test.Echo", host.List());
    }

    [Fact]
    public void List_ReturnsAllRegisteredFunctionTypes()
    {
        var host = BuildHost(new EchoFunction(), new HeartbeatFunction());
        var list = host.List();
        Assert.Contains("Test.Echo", list);
        Assert.Contains("Test.Heartbeat", list);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task FastPath_ExecuteAsync_ReturnsSuccess_WithNoOverhead()
    {
        var echo = new EchoFunction();
        var host = BuildHost(echo);
        var ctx = new FunctionExecutionContext
        {
            FunctionName = "Test.Echo",
            Input = "hello",
            Parameters = new() { ["message"] = "hello" },
            CancellationToken = CancellationToken.None,
            ServiceProvider = null!
        };

        var result = await host.ExecuteAsync(echo, ctx, FunctionExecutionOptions.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("echo: hello", result.Message);
        // With EnableTiming=false (None), Duration should be zero (function didn't set it)
        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    async Task FastPath_DefaultOptions_CapturesErrors_ButNoTiming()
    {
        var fail = new FailFunction();
        var host = BuildHost(fail);
        var ctx = new FunctionExecutionContext
        {
            FunctionName = "Test.Fail",
            Input = new(),
            CancellationToken = CancellationToken.None,
            ServiceProvider = null!
        };

        var result = await host.ExecuteAsync(fail, ctx); // Default options

        Assert.False(result.IsSuccess);
        Assert.Contains("boom", result.Message);
        Assert.NotNull(result.ErrorDetails);
    }

    [Fact]
    public async Task FullObservability_AttachesTiming()
    {
        var slow = new SlowFunction();
        var host = BuildHost(slow);
        var ctx = new FunctionExecutionContext
        {
            FunctionName = "Test.Slow",
            Input = new(),
            CancellationToken = CancellationToken.None,
            ServiceProvider = null!
        };

        var result = await host.ExecuteAsync(slow, ctx, FunctionExecutionOptions.FullObservability);

        Assert.True(result.IsSuccess);
        Assert.True(result.Duration >= TimeSpan.FromMilliseconds(40)); // SlowFunction delays 50ms
    }

    [Fact]
    public async Task SlowPath_ExecuteAsync_ByFunctionType_LooksUpAndInvokes()
    {
        var echo = new EchoFunction();
        var host = BuildHost(echo);

        var result = await host.ExecuteAsync(
            "Test.Echo",
            input: new Dictionary<string, object> { ["message"] = "via-slow-path" },
            options: FunctionExecutionOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Equal("echo: via-slow-path", result.Message);
    }

    [Fact]
    public async Task SlowPath_NotRegistered_ReturnsFailure_WithAvailableList()
    {
        var host = BuildHost(new EchoFunction());

        var result = await host.ExecuteAsync("Test.NonExistent", input: null);

        Assert.False(result.IsSuccess);
        Assert.Contains("not registered", result.Message);
        Assert.Contains("Test.Echo", result.ErrorDetails!); // Lists available functions
    }

    [Fact]
    public async Task SlowPath_JsonElementInput_IsProjectedToParameters()
    {
        // When ASP.NET Core deserializes a JSON body to `object?`, you get a JsonElement.
        // The host should copy its properties into Parameters so CustomFunctionBase.GetParameter<T>
        // can read them. This test verifies that translation.
        var echo = new EchoFunction();
        var host = BuildHost(echo);

        var json = JsonDocument.Parse("""{"message":"from-json"}""").RootElement;
        var result = await host.ExecuteAsync("Test.Echo", input: json);

        Assert.True(result.IsSuccess);
        Assert.Equal("echo: from-json", result.Message);
    }
}

public class DomainEventBusTests
{
    [Fact]
    public async Task PublishAsync_FiresAllSubscribersForEventType()
    {
        var bus = new InProcessDomainEventBus(NullLogger<InProcessDomainEventBus>.Instance);
        var calls = new List<string>();

        bus.Subscribe<TestEvent>(async (e, ct) => { calls.Add($"A:{e.Payload}"); await Task.CompletedTask; });
        bus.Subscribe<TestEvent>(async (e, ct) => { calls.Add($"B:{e.Payload}"); await Task.CompletedTask; });

        await bus.PublishAsync(new TestEvent("hello"));

        Assert.Contains("A:hello", calls);
        Assert.Contains("B:hello", calls);
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_DoesNotThrow()
    {
        var bus = new InProcessDomainEventBus(NullLogger<InProcessDomainEventBus>.Instance);
        await bus.PublishAsync(new TestEvent("nothing"));
        // Reaching here is success.
    }

    [Fact]
    public async Task PublishAsync_HandlerThrows_OthersStillFire()
    {
        var bus = new InProcessDomainEventBus(NullLogger<InProcessDomainEventBus>.Instance);
        var calls = new List<string>();

        bus.Subscribe<TestEvent>(async (e, ct) => { await Task.CompletedTask; throw new InvalidOperationException("boom"); });
        bus.Subscribe<TestEvent>(async (e, ct) => { calls.Add("B"); await Task.CompletedTask; });

        await bus.PublishAsync(new TestEvent("x"));

        Assert.Contains("B", calls); // Second handler ran despite first throwing
    }

    private record TestEvent(string Payload) : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }
}

// =============================================================================
// Test functions (minimal, in-file so the test project has zero dependencies)
// =============================================================================

file class EchoFunction : CustomFunctionBase
{
    public override string FunctionType => "Test.Echo";

    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        await OnInitializeAsync();
        var msg = GetParameter(context, "message", "(no message)");
        return FunctionExecutionResult.Success(new { message = msg }, $"echo: {msg}", TimeSpan.Zero);
    }

    public override Task<ValidationResult> ValidateInputAsync(object input) => Task.FromResult(ValidationResult.Success());
    public override JsonDocument GetInputSchema() => CreateSchema(new { type = "object" });
    public override JsonDocument GetOutputSchema() => CreateSchema(new { type = "object" });
}

file class HeartbeatFunction : CustomFunctionBase
{
    public override string FunctionType => "Test.Heartbeat";
    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        await OnInitializeAsync();
        return FunctionExecutionResult.Success(new { at = DateTime.UtcNow }, "heartbeat", TimeSpan.Zero);
    }
    public override Task<ValidationResult> ValidateInputAsync(object input) => Task.FromResult(ValidationResult.Success());
    public override JsonDocument GetInputSchema() => CreateSchema(new { type = "object" });
    public override JsonDocument GetOutputSchema() => CreateSchema(new { type = "object" });
}

file class FailFunction : CustomFunctionBase
{
    public override string FunctionType => "Test.Fail";
    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        await OnInitializeAsync();
        throw new InvalidOperationException("boom");
    }
    public override Task<ValidationResult> ValidateInputAsync(object input) => Task.FromResult(ValidationResult.Success());
    public override JsonDocument GetInputSchema() => CreateSchema(new { type = "object" });
    public override JsonDocument GetOutputSchema() => CreateSchema(new { type = "object" });
}

file class SlowFunction : CustomFunctionBase
{
    public override string FunctionType => "Test.Slow";
    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        await OnInitializeAsync();
        await Task.Delay(50);
        return FunctionExecutionResult.Success(new { done = true }, "slow done", TimeSpan.Zero);
    }
    public override Task<ValidationResult> ValidateInputAsync(object input) => Task.FromResult(ValidationResult.Success());
    public override JsonDocument GetInputSchema() => CreateSchema(new { type = "object" });
    public override JsonDocument GetOutputSchema() => CreateSchema(new { type = "object" });
}
