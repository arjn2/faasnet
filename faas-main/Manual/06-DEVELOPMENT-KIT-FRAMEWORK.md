# Chapter 06: Development Kit Framework
## Extending Artichoke-FaaS with Custom Components

---

## Overview

The **Development Kit Framework** is a comprehensive extension system that allows developers to create custom triggers and functions for the Artichoke-FaaS platform. This framework maintains the platform's "pure external" philosophy while providing powerful extensibility through well-defined interfaces and base classes.

### **Framework Philosophy**

- **Zero Core Modification**: Extend without touching platform internals
- **Plugin Architecture**: Load custom components dynamically
- **Type Safety**: Strong typing with compile-time validation
- **Developer Experience**: Rich base classes and utilities
- **Production Ready**: Full lifecycle management and monitoring

---

## Core Interfaces

### **ITrigger Interface**

The foundation for all trigger implementations, both built-in and custom:

```csharp
/// <summary>
/// Base interface for all triggers (built-in and custom) - Development Kit v3.3
/// </summary>
public interface ITrigger
{
    /// <summary>
    /// Unique trigger type identifier
    /// </summary>
    string TriggerType { get; }
    
    /// <summary>
    /// Display name for UI
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Trigger description
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Execute trigger with given context
    /// </summary>
    Task<TriggerExecutionResult> ExecuteAsync(TriggerExecutionContext context);
    
    /// <summary>
    /// Validate trigger configuration
    /// </summary>
    Task<ValidationResult> ValidateConfigurationAsync(JsonDocument configuration);
    
    /// <summary>
    /// Get configuration schema for this trigger
    /// </summary>
    JsonDocument GetConfigurationSchema();
    
    /// <summary>
    /// Initialize trigger with configuration
    /// </summary>
    Task InitializeAsync(JsonDocument configuration);
    
    /// <summary>
    /// Cleanup trigger resources
    /// </summary>
    Task DisposeAsync();
}
```

### **ICustomFunction Interface**

For creating custom function implementations:

```csharp
/// <summary>
/// Base interface for custom functions - Development Kit v3.3
/// </summary>
public interface ICustomFunction
{
    /// <summary>
    /// Function type identifier
    /// </summary>
    string FunctionType { get; }
    
    /// <summary>
    /// Execute function with given input
    /// </summary>
    Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context);
    
    /// <summary>
    /// Validate function input
    /// </summary>
    Task<ValidationResult> ValidateInputAsync(object input);
    
    /// <summary>
    /// Get input schema for this function
    /// </summary>
    JsonDocument GetInputSchema();
    
    /// <summary>
    /// Get output schema for this function
    /// </summary>
    JsonDocument GetOutputSchema();
}
```

---

## Factory System

### **ITriggerFactory**

Manages trigger instantiation and registration:

```csharp
/// <summary>
/// Trigger factory for creating trigger instances
/// </summary>
public interface ITriggerFactory
{
    /// <summary>
    /// Create trigger instance by type
    /// </summary>
    Task<ITrigger?> CreateTriggerAsync(string triggerType);
    
    /// <summary>
    /// Register custom trigger type
    /// </summary>
    Task RegisterCustomTriggerAsync(string triggerType, Type implementationType);
    
    /// <summary>
    /// Get all available trigger types
    /// </summary>
    Task<string[]> GetAvailableTriggerTypesAsync();
}
```

### **ICustomFunctionFactory**

Manages custom function creation:

```csharp
/// <summary>
/// Function factory for creating custom function instances
/// </summary>
public interface ICustomFunctionFactory
{
    /// <summary>
    /// Create custom function instance by type
    /// </summary>
    Task<ICustomFunction?> CreateFunctionAsync(string functionType);
    
    /// <summary>
    /// Register custom function type
    /// </summary>
    Task RegisterCustomFunctionAsync(string functionType, Type implementationType);
    
    /// <summary>
    /// Get all available custom function types
    /// </summary>
    Task<string[]> GetAvailableFunctionTypesAsync();
}
```

---

## Base Classes

### **CustomTriggerBase**

Simplified development experience for custom triggers:

```csharp
/// <summary>
/// Base class for custom trigger development - Development Kit v3.3
/// Provides common functionality and simplified development experience
/// </summary>
public abstract class CustomTriggerBase : ITrigger
{
    protected ILogger Logger { get; private set; } = null!;
    protected JsonDocument Configuration { get; private set; } = JsonDocument.Parse("{}");
    protected bool IsInitialized { get; private set; }

    public abstract string TriggerType { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }

    // Abstract methods to implement
    public abstract Task<TriggerExecutionResult> ExecuteAsync(TriggerExecutionContext context);
    public abstract JsonDocument GetConfigurationSchema();

    // Virtual methods with default implementations
    public virtual Task<ValidationResult> ValidateConfigurationAsync(JsonDocument configuration)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    // Helper methods
    protected T GetConfigValue<T>(string path, T defaultValue = default!)
    protected JsonDocument CreateConfigSchema(object schemaObject)
    protected async Task<TriggerExecutionResult> SafeExecuteAsync(
        TriggerExecutionContext context, 
        Func<TriggerExecutionContext, Task<TriggerExecutionResult>> executeFunc)
}
```

### **CustomFunctionBase**

Base class for custom function development:

```csharp
/// <summary>
/// Base class for custom function development - Development Kit v3.3
/// Provides common functionality and simplified development experience
/// </summary>
public abstract class CustomFunctionBase : ICustomFunction
{
    protected ILogger Logger { get; private set; } = null!;
    protected bool IsInitialized { get; private set; }

    public abstract string FunctionType { get; }
    public abstract Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context);
    public abstract JsonDocument GetInputSchema();
    public abstract JsonDocument GetOutputSchema();

    // Helper methods
    protected T GetParameter<T>(FunctionExecutionContext context, string name, T defaultValue = default!)
    protected JsonDocument CreateSchema(object schemaObject)
    protected async Task<FunctionExecutionResult> SafeExecuteAsync(
        FunctionExecutionContext context, 
        Func<FunctionExecutionContext, Task<FunctionExecutionResult>> executeFunc)
}
```

---

## Context Objects

### **TriggerExecutionContext**

Provides complete context for trigger execution:

```csharp
/// <summary>
/// Trigger execution context
/// </summary>
public record TriggerExecutionContext
{
    public string ProjectNamespace { get; init; } = string.Empty;
    public string FunctionName { get; init; } = string.Empty;
    public JsonDocument TriggerData { get; init; } = JsonDocument.Parse("{}");
    public JsonDocument Configuration { get; init; } = JsonDocument.Parse("{}");
    public Dictionary<string, object> Metadata { get; init; } = new();
    public CancellationToken CancellationToken { get; init; }
    public IServiceProvider ServiceProvider { get; init; } = null!;
    public string? TriggerSource { get; init; } // HTTP, CLI, Schedule, etc.
}
```

### **FunctionExecutionContext**

Context for custom function execution:

```csharp
/// <summary>
/// Function execution context
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
    public string? ExecutionSource { get; init; } // Trigger, Manual, CLI, etc.
}
```

---

## Result Types

### **TriggerExecutionResult**

Comprehensive result information from trigger execution:

```csharp
/// <summary>
/// Trigger execution result
/// </summary>
public record TriggerExecutionResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public object? Output { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
    public string? ErrorDetails { get; init; }
    
    public static TriggerExecutionResult Success(string message, object? output = null, TimeSpan duration = default)
        => new() { IsSuccess = true, Message = message, Output = output, Duration = duration };
    
    public static TriggerExecutionResult Failure(string message, string? errorDetails = null, TimeSpan duration = default)
        => new() { IsSuccess = false, Message = message, ErrorDetails = errorDetails, Duration = duration };
}
```

### **FunctionExecutionResult**

Result information from custom function execution:

```csharp
/// <summary>
/// Function execution result
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
```

### **ValidationResult**

Result of configuration or input validation:

```csharp
/// <summary>
/// Validation result
/// </summary>
public record ValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    
    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(params string[] errors) => new() { IsValid = false, Errors = errors.ToList() };
    public static ValidationResult Warning(string[] warnings) => new() { IsValid = true, Warnings = warnings.ToList() };
}
```

---

## Built-in Implementations

### **HttpTrigger Example**

Complete implementation showing best practices:

```csharp
/// <summary>
/// Built-in HTTP Trigger - Example implementation
/// </summary>
public class HttpTrigger : CustomTriggerBase
{
    public override string TriggerType => "HttpTrigger";
    public override string DisplayName => "HTTP Trigger";
    public override string Description => "Triggers function when HTTP requests are received";

    public override async Task<TriggerExecutionResult> ExecuteAsync(TriggerExecutionContext context)
    {
        return await SafeExecuteAsync(context, async (ctx) =>
        {
            var method = GetConfigValue<string>("method", "POST");
            var route = GetConfigValue<string>("route", "/");
            
            Logger.LogInformation("Processing HTTP trigger for {Route} with method {Method}", route, method);
            
            // Simulate HTTP processing
            await Task.Delay(50);
            
            return TriggerExecutionResult.Success(
                "HTTP trigger processed successfully",
                new
                {
                    Method = method,
                    Route = route,
                    ProcessedAt = DateTime.UtcNow,
                    StatusCode = 200
                }
            );
        });
    }

    public override JsonDocument GetConfigurationSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                method = new { 
                    type = "string", 
                    description = "HTTP method", 
                    @default = "POST", 
                    @enum = new[] { "GET", "POST", "PUT", "DELETE" } 
                },
                route = new { 
                    type = "string", 
                    description = "Route template", 
                    @default = "/" 
                },
                authLevel = new { 
                    type = "string", 
                    description = "Authorization level", 
                    @default = "function", 
                    @enum = new[] { "anonymous", "function", "admin" } 
                }
            },
            required = new[] { "method", "route" }
        };

        return CreateConfigSchema(schema);
    }
}
```

### **TimerTrigger Example**

Scheduled execution trigger:

```csharp
/// <summary>
/// Built-in Timer Trigger - Example implementation
/// </summary>
public class TimerTrigger : CustomTriggerBase
{
    public override string TriggerType => "TimerTrigger";
    public override string DisplayName => "Timer Trigger";
    public override string Description => "Triggers function on a schedule using cron expressions";

    public override async Task<TriggerExecutionResult> ExecuteAsync(TriggerExecutionContext context)
    {
        return await SafeExecuteAsync(context, async (ctx) =>
        {
            var schedule = GetConfigValue<string>("schedule", "0 */5 * * * *");
            var isPastDue = GetConfigValue<bool>("isPastDue", false);
            
            Logger.LogInformation("Processing timer trigger with schedule {Schedule}, IsPastDue: {IsPastDue}", 
                schedule, isPastDue);
            
            // Simulate timer processing
            await Task.Delay(25);
            
            return TriggerExecutionResult.Success(
                "Timer trigger executed successfully",
                new
                {
                    Schedule = schedule,
                    IsPastDue = isPastDue,
                    NextRun = DateTime.UtcNow.AddMinutes(5),
                    ProcessedAt = DateTime.UtcNow
                }
            );
        });
    }

    public override JsonDocument GetConfigurationSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                schedule = new { 
                    type = "string", 
                    description = "Cron expression for schedule", 
                    @default = "0 */5 * * * *" 
                },
                isPastDue = new { 
                    type = "boolean", 
                    description = "Whether the trigger is past due", 
                    @default = false 
                },
                runOnStartup = new { 
                    type = "boolean", 
                    description = "Run immediately on startup", 
                    @default = false 
                }
            },
            required = new[] { "schedule" }
        };

        return CreateConfigSchema(schema);
    }
}
```

---

## Service Layer

### **DevelopmentKitService**

Core service managing custom components:

```csharp
/// <summary>
/// Development Kit Service - Manages custom triggers and functions
/// This service integrates with the existing platform without modifying core functionality
/// </summary>
public class DevelopmentKitService
{
    private readonly DevelopmentKitDbContext _devKitContext;
    private readonly FaasPlatformDbContext _platformContext;
    private readonly ILogger<DevelopmentKitService> _logger;
    private readonly ITriggerFactory _triggerFactory;
    private readonly ICustomFunctionFactory _functionFactory;

    // Trigger Management
    public async Task<CustomTriggerDefinitionEntity> RegisterCustomTriggerAsync(
        string assemblyPath, string typeName, string createdBy)

    public async Task<bool> UnregisterCustomTriggerAsync(string triggerType)

    public async Task<List<CustomTriggerDefinitionEntity>> GetCustomTriggersAsync()

    public async Task<ITrigger?> CreateTriggerInstanceAsync(string triggerType)

    // Function Management
    public async Task<CustomFunctionDefinitionEntity> RegisterCustomFunctionAsync(
        string assemblyPath, string typeName, string createdBy)

    public async Task<bool> UnregisterCustomFunctionAsync(string functionType)

    public async Task<List<CustomFunctionDefinitionEntity>> GetCustomFunctionsAsync()

    public async Task<ICustomFunction?> CreateFunctionInstanceAsync(string functionType)

    // Health and Monitoring
    public async Task<List<TriggerUsageStatsEntity>> GetTriggerUsageStatsAsync()

    public async Task<List<FunctionUsageStatsEntity>> GetFunctionUsageStatsAsync()
}
```

---

## REST API

### **DevelopmentKitController**

RESTful API for managing development kit components:

```csharp
/// <summary>
/// Development Kit Controller - Manages custom triggers and functions
/// Separate controller to keep existing controllers clean
/// </summary>
[ApiController]
[Route("api/v1/dev-kit")]
[Tags("Development Kit")]
[Authorize(Roles = "PlatformAdmin,ProjectOwner,Developer")]
public class DevelopmentKitController : ControllerBase
{
    // GET /api/v1/dev-kit/triggers/types
    [HttpGet("triggers/types")]
    public async Task<IActionResult> GetTriggerTypes()

    // POST /api/v1/dev-kit/triggers/register
    [HttpPost("triggers/register")]
    public async Task<IActionResult> RegisterCustomTrigger([FromBody] RegisterTriggerRequest request)

    // DELETE /api/v1/dev-kit/triggers/{triggerType}
    [HttpDelete("triggers/{triggerType}")]
    public async Task<IActionResult> UnregisterCustomTrigger(string triggerType)

    // GET /api/v1/dev-kit/triggers
    [HttpGet("triggers")]
    public async Task<IActionResult> GetCustomTriggers()

    // GET /api/v1/dev-kit/triggers/{triggerType}/schema
    [HttpGet("triggers/{triggerType}/schema")]
    public async Task<IActionResult> GetTriggerSchema(string triggerType)

    // POST /api/v1/dev-kit/triggers/{triggerType}/test
    [HttpPost("triggers/{triggerType}/test")]
    public async Task<IActionResult> TestTrigger(string triggerType, [FromBody] JsonDocument configuration)

    // Function endpoints follow similar pattern...
}
```

---

## Database Schema

### **CustomTriggerDefinitionEntity**

Stores custom trigger metadata:

```csharp
public class CustomTriggerDefinitionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;          // Trigger type identifier
    public string Version { get; set; } = string.Empty;       // Assembly version
    public string DisplayName { get; set; } = string.Empty;   // Human-readable name
    public string Description { get; set; } = string.Empty;   // Detailed description
    public string AssemblyPath { get; set; } = string.Empty;  // Path to assembly
    public string TypeName { get; set; } = string.Empty;      // Full type name
    public string ConfigurationSchema { get; set; } = string.Empty; // JSON schema
    public bool IsBuiltIn { get; set; }                       // Built-in vs custom
    public bool IsActive { get; set; } = true;               // Enable/disable
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; }

    // Navigation properties
    public List<TriggerUsageStatsEntity> UsageStats { get; set; } = new();
}
```

### **CustomFunctionDefinitionEntity**

Stores custom function metadata:

```csharp
public class CustomFunctionDefinitionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;          // Function type identifier
    public string Version { get; set; } = string.Empty;       // Assembly version
    public string DisplayName { get; set; } = string.Empty;   // Human-readable name
    public string Description { get; set; } = string.Empty;   // Detailed description
    public string AssemblyPath { get; set; } = string.Empty;  // Path to assembly
    public string TypeName { get; set; } = string.Empty;      // Full type name
    public string InputSchema { get; set; } = string.Empty;   // Input JSON schema
    public string OutputSchema { get; set; } = string.Empty;  // Output JSON schema
    public bool IsActive { get; set; } = true;               // Enable/disable
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; }

    // Navigation properties
    public List<FunctionUsageStatsEntity> UsageStats { get; set; } = new();
}
```

---

## Creating Custom Triggers

### **Step-by-Step Guide**

#### **1. Create Trigger Class**

```csharp
using Artichoke.FaaS.Core.Base;
using Artichoke.FaaS.Core.Interfaces;
using System.Text.Json;

public class DatabaseTrigger : CustomTriggerBase
{
    public override string TriggerType => "DatabaseTrigger";
    public override string DisplayName => "Database Change Trigger";
    public override string Description => "Triggers when database records change";

    public override async Task<TriggerExecutionResult> ExecuteAsync(TriggerExecutionContext context)
    {
        return await SafeExecuteAsync(context, async (ctx) =>
        {
            var connectionString = GetConfigValue<string>("connectionString");
            var tableName = GetConfigValue<string>("tableName");
            var operation = GetConfigValue<string>("operation", "INSERT");

            Logger.LogInformation("Processing database trigger for table {Table}, operation {Operation}", 
                tableName, operation);

            // Your custom database change detection logic here
            var changes = await DetectDatabaseChanges(connectionString, tableName, operation);

            return TriggerExecutionResult.Success(
                $"Database trigger processed {changes.Count} changes",
                new { TableName = tableName, Operation = operation, Changes = changes }
            );
        });
    }

    public override JsonDocument GetConfigurationSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                connectionString = new
                {
                    type = "string",
                    description = "Database connection string",
                    format = "connection-string"
                },
                tableName = new
                {
                    type = "string",
                    description = "Table name to monitor"
                },
                operation = new
                {
                    type = "string",
                    description = "Database operation to monitor",
                    @default = "INSERT",
                    @enum = new[] { "INSERT", "UPDATE", "DELETE" }
                }
            },
            required = new[] { "connectionString", "tableName" }
        };

        return CreateConfigSchema(schema);
    }

    private async Task<List<object>> DetectDatabaseChanges(string connectionString, string tableName, string operation)
    {
        // Implement your database change detection logic
        await Task.Delay(100); // Simulate processing
        return new List<object> { new { Id = 1, Action = operation, Timestamp = DateTime.UtcNow } };
    }
}
```

#### **2. Register Trigger**

```bash
# Via REST API
curl -X POST "https://localhost:7297/api/v1/dev-kit/triggers/register" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "assemblyPath": "path/to/your/CustomTriggers.dll",
    "typeName": "YourNamespace.DatabaseTrigger"
  }'
```

#### **3. Use in Function Configuration**

```json
{
  "triggers": [
    {
      "type": "DatabaseTrigger",
      "configuration": {
        "connectionString": "Server=localhost;Database=MyDB;Trusted_Connection=true;",
        "tableName": "Orders",
        "operation": "INSERT"
      }
    }
  ]
}
```

---

## Creating Custom Functions

### **Step-by-Step Guide**

#### **1. Create Function Class**

```csharp
using Artichoke.FaaS.Core.Base;
using Artichoke.FaaS.Core.Interfaces;
using System.Text.Json;

public class DataTransformFunction : CustomFunctionBase
{
    public override string FunctionType => "DataTransformFunction";

    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        return await SafeExecuteAsync(context, async (ctx) =>
        {
            var transformType = GetParameter<string>(ctx, "transformType", "normalize");
            var data = ctx.Input;

            Logger.LogInformation("Processing data transform: {TransformType}", transformType);

            // Your custom transformation logic here
            var transformedData = await TransformData(data, transformType);

            return FunctionExecutionResult.Success(
                transformedData, 
                $"Data transformed using {transformType}"
            );
        });
    }

    public override JsonDocument GetInputSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                data = new { type = "object", description = "Data to transform" },
                transformType = new
                {
                    type = "string",
                    description = "Type of transformation",
                    @enum = new[] { "normalize", "aggregate", "filter" }
                }
            },
            required = new[] { "data" }
        };

        return CreateSchema(schema);
    }

    public override JsonDocument GetOutputSchema()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                transformedData = new { type = "object", description = "Transformed data result" },
                transformType = new { type = "string", description = "Applied transformation type" },
                processedAt = new { type = "string", format = "date-time" }
            }
        };

        return CreateSchema(schema);
    }

    private async Task<object> TransformData(object data, string transformType)
    {
        // Implement your transformation logic
        await Task.Delay(50); // Simulate processing
        
        return transformType switch
        {
            "normalize" => new { NormalizedData = data, ProcessedAt = DateTime.UtcNow },
            "aggregate" => new { AggregatedData = data, Count = 1, ProcessedAt = DateTime.UtcNow },
            "filter" => new { FilteredData = data, ItemsRemoved = 0, ProcessedAt = DateTime.UtcNow },
            _ => throw new ArgumentException($"Unknown transform type: {transformType}")
        };
    }
}
```

#### **2. Register Function**

```bash
# Via REST API
curl -X POST "https://localhost:7297/api/v1/dev-kit/functions/register" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "assemblyPath": "path/to/your/CustomFunctions.dll",
    "typeName": "YourNamespace.DataTransformFunction"
  }'
```

---

## Best Practices

### **Development Guidelines**

1. **Inherit from Base Classes**
   ```csharp
   // ✅ Good - Use base classes
   public class MyTrigger : CustomTriggerBase
   
   // ❌ Avoid - Direct interface implementation
   public class MyTrigger : ITrigger
   ```

2. **Use SafeExecuteAsync**
   ```csharp
   public override async Task<TriggerExecutionResult> ExecuteAsync(TriggerExecutionContext context)
   {
       return await SafeExecuteAsync(context, async (ctx) =>
       {
           // Your logic here - exceptions handled automatically
           return TriggerExecutionResult.Success("Completed");
       });
   }
   ```

3. **Provide Comprehensive Schemas**
   ```csharp
   public override JsonDocument GetConfigurationSchema()
   {
       var schema = new
       {
           type = "object",
           properties = new
           {
               // Include descriptions, defaults, enums, validation rules
               timeout = new { 
                   type = "integer", 
                   description = "Timeout in seconds",
                   @default = 30,
                   minimum = 1,
                   maximum = 300
               }
           }
       };
       return CreateConfigSchema(schema);
   }
   ```

4. **Log Appropriately**
   ```csharp
   Logger.LogInformation("Processing {TriggerType} with config {Config}", TriggerType, configSummary);
   Logger.LogWarning("Non-fatal issue: {Issue}", issue);
   Logger.LogError(ex, "Failed to process {TriggerType}", TriggerType);
   ```

### **Performance Considerations**

- **Async All The Way**: Never block async operations
- **Resource Management**: Implement proper disposal
- **Configuration Caching**: Cache parsed configuration values
- **Bulk Operations**: Process multiple items when possible

### **Security Guidelines**

- **Input Validation**: Always validate configuration and input
- **Sandboxing**: Assume untrusted input
- **Resource Limits**: Implement timeouts and limits
- **Logging**: Don't log sensitive information

---

## Testing Framework

### **Unit Testing Custom Components**

```csharp
[Test]
public async Task DatabaseTrigger_ExecuteAsync_ReturnsSuccess()
{
    // Arrange
    var trigger = new DatabaseTrigger();
    var config = JsonDocument.Parse(@"{
        ""connectionString"": ""test-connection"",
        ""tableName"": ""TestTable"",
        ""operation"": ""INSERT""
    }");
    
    await trigger.InitializeAsync(config);
    
    var context = new TriggerExecutionContext
    {
        ProjectNamespace = "test",
        FunctionName = "test-function",
        Configuration = config
    };

    // Act
    var result = await trigger.ExecuteAsync(context);

    // Assert
    Assert.IsTrue(result.IsSuccess);
    Assert.AreEqual("Database trigger processed 1 changes", result.Message);
}
```

### **Integration Testing**

```csharp
[Test]
public async Task DevelopmentKitService_RegisterTrigger_Success()
{
    // Arrange
    var service = GetDevelopmentKitService();
    var assemblyPath = "path/to/test-triggers.dll";
    var typeName = "TestTriggers.DatabaseTrigger";

    // Act
    var result = await service.RegisterCustomTriggerAsync(assemblyPath, typeName, "test-user");

    // Assert
    Assert.IsNotNull(result);
    Assert.AreEqual("DatabaseTrigger", result.Name);
    Assert.IsTrue(result.IsActive);
}
```

---

## Monitoring and Diagnostics

### **Built-in Monitoring**

The Development Kit provides comprehensive monitoring:

- **Execution Metrics**: Duration, success/failure rates
- **Usage Statistics**: Frequency, patterns
- **Error Tracking**: Exception details, stack traces
- **Performance Profiling**: Resource usage, bottlenecks

### **Health Checks**

```csharp
// GET /health/dev-kit
{
  "status": "Healthy",
  "customTriggers": {
    "total": 5,
    "active": 4,
    "healthy": 4
  },
  "customFunctions": {
    "total": 3,
    "active": 3,
    "healthy": 3
  },
  "recentExecutions": {
    "success": 152,
    "failures": 3,
    "averageDuration": "00:00:01.245"
  }
}
```

---

## Migration and Versioning

### **Version Compatibility**

- **Semantic Versioning**: MAJOR.MINOR.PATCH
- **Breaking Changes**: Major version increments
- **Backward Compatibility**: Minor/patch versions
- **Migration Scripts**: Automated upgrade paths

### **Assembly Management**

```csharp
// Automatic assembly loading with version checking
public async Task<bool> ValidateAssemblyCompatibilityAsync(string assemblyPath)
{
    var assembly = Assembly.LoadFrom(assemblyPath);
    var targetVersion = new Version("3.3.0");
    
    // Check framework version compatibility
    var frameworkAttribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
    // Validate against platform requirements
    
    return true; // or false with detailed error messages
}
```

---

## Conclusion

The Development Kit Framework provides a powerful, flexible way to extend Artichoke-FaaS without compromising its core philosophy. By following the patterns and guidelines in this chapter, developers can create robust, maintainable extensions that integrate seamlessly with the platform.

Key benefits:
- **Zero Impact**: Extend without modifying core platform
- **Type Safety**: Strong contracts prevent runtime errors  
- **Developer Experience**: Rich base classes and utilities
- **Production Ready**: Full monitoring and lifecycle management
- **Scalable**: Plugin architecture supports any number of extensions

The framework supports the full development lifecycle from creation through testing, deployment, monitoring, and maintenance.
