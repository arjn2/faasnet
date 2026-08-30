# Artichoke-FaaS Platform: Actions, Triggers & Event System

Complete reference of all actions, event triggers, and execution capabilities
built into the Artichoke-FaaS Platform v3.3.

---

## Table of Contents

1. [Built-in Triggers](#1-built-in-triggers)
2. [Custom Triggers (DevKit)](#2-custom-triggers-devkit)
3. [Function Actions](#3-function-actions)
4. [Platform Management Actions](#4-platform-management-actions)
5. [Development Kit Actions](#5-development-kit-actions)
6. [Real-Time Streaming Events (SignalR)](#6-real-time-streaming-events-signalr)
7. [Function Lifecycle Commands](#7-function-lifecycle-commands)
8. [System Actions](#8-system-actions)
9. [Function Categories](#9-function-categories)
10. [Workflow Orchestration](#10-workflow-orchestration)

---

## 1. Built-in Triggers

Triggers that ship with the platform out of the box. No plugins required.

### 1.1 HttpTrigger

| Property | Value |
|----------|-------|
| **Type** | `HttpTrigger` |
| **Category** | Web |
| **Base Class** | `CustomTriggerBase` |
| **Source** | `Artichoke.FaaS.Core` |

Fires a function when an HTTP request is received.

**Configuration Schema:**
```json
{
  "type": "object",
  "properties": {
    "method": { "type": "string", "enum": ["GET", "POST", "PUT", "DELETE"], "default": "POST" },
    "route": { "type": "string", "default": "/" },
    "authLevel": { "type": "string", "enum": ["anonymous", "function", "admin"], "default": "function" }
  },
  "required": ["method", "route"]
}
```

**Use Case:** Expose any function as a REST endpoint without writing controller code.

---

### 1.2 TimerTrigger

| Property | Value |
|----------|-------|
| **Type** | `TimerTrigger` |
| **Category** | Scheduled |
| **Base Class** | `CustomTriggerBase` |
| **Source** | `Artichoke.FaaS.Core` |

Fires a function on a cron schedule.

**Configuration Schema:**
```json
{
  "type": "object",
  "properties": {
    "schedule": { "type": "string", "default": "0 */5 * * * *", "description": "Cron expression" },
    "isPastDue": { "type": "boolean", "default": false },
    "runOnStartup": { "type": "boolean", "default": false }
  },
  "required": ["schedule"]
}
```

**Use Case:** Scheduled cleanup, health checks, batch processing, report generation.

---

### 1.3 QueueTrigger (Registered, Stub)

| Property | Value |
|----------|-------|
| **Type** | `QueueTrigger` |
| **Category** | Messaging |
| **Source** | `Artichoke.FaaS.Platform` |

Registered in the built-in trigger list. Fires when a message arrives on a queue.

**Use Case:** Async processing of queued work items, event-driven pipelines.

---

### 1.4 BlobTrigger (Registered, Stub)

| Property | Value |
|----------|-------|
| **Type** | `BlobTrigger` |
| **Category** | Storage |
| **Source** | `Artichoke.FaaS.Platform` |

Registered in the built-in trigger list. Fires when a file/blob is created or modified.

**Use Case:** File processing, image resizing, log parsing, data imports.

---

## 2. Custom Triggers (DevKit)

Build your own triggers using the Development Kit. No external plugins needed.

### 2.1 Creating a Custom Trigger

Extend `CustomTriggerBase` from `Artichoke.FaaS.Core.Base`:

```csharp
public class DomainEventTrigger : CustomTriggerBase
{
    public override string TriggerType => "DomainEventTrigger";
    public override string DisplayName => "Domain Event Trigger";
    public override string Description => "Fires when a domain event is published";

    public override async Task<TriggerExecutionResult> ExecuteAsync(TriggerExecutionContext context)
    {
        return await SafeExecuteAsync(context, async (ctx) =>
        {
            var eventType = GetConfigValue<string>("eventType");
            // ... handle event
            return TriggerExecutionResult.Success($"Event {eventType} processed");
        });
    }

    public override JsonDocument GetConfigurationSchema() => /* ... */;
}
```

### 2.2 Registering Custom Triggers

**API:** `POST /api/v1/dev-kit/triggers/register`
```json
{
  "assemblyPath": "/path/to/assembly.dll",
  "typeName": "MyApp.Triggers.DomainEventTrigger"
}
```

### 2.3 Attaching Triggers to Functions

**API:** `POST /api/v1/dev-kit/triggers/add`
```json
{
  "projectNamespace": "BMS",
  "functionName": "AuditLogger",
  "triggerType": "DomainEventTrigger",
  "configuration": "{\"eventType\": \"BookCreatedEvent\"}",
  "priority": 0
}
```

### 2.4 Executing a Trigger

**API:** `POST /api/v1/dev-kit/triggers/{triggerInstanceId}/execute`
```json
{
  "triggerData": "{\"bookId\": 42, \"title\": \"Clean Code\"}",
  "triggerSource": "DomainEvent"
}
```

---

## 3. Function Actions

Actions that operate on individual functions within a project.

### 3.1 Register Function

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/functions` | PlatformAdmin, ProjectOwner, Developer |

```json
{
  "projectNamespace": "BMS",
  "name": "BookProcessor",
  "description": "Processes book operations in background queue",
  "version": "1.0.0",
  "category": "Business"
}
```

### 3.2 Start Function

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/functions/{project}/{function}/start` | PlatformAdmin, ProjectOwner, Developer |

Starts a function's scheduled execution loop.

### 3.3 Stop Function

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/functions/{project}/{function}/stop` | PlatformAdmin, ProjectOwner, Developer |

Stops a running function.

### 3.4 Execute Function (One-Shot)

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/functions/{project}/{function}/execute` | PlatformAdmin, ProjectOwner, Developer |

Executes a function immediately, outside its schedule.

### 3.5 Get Function Details

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/functions/{project}/{function}` | PlatformAdmin, ProjectOwner, Developer |

Returns function info, configuration, and last 10 execution records.

### 3.6 List Functions

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/functions?project={project}` | PlatformAdmin, ProjectOwner, Developer |

List all functions, optionally filtered by project.

---

## 4. Platform Management Actions

### 4.1 Platform Status

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/platform/status` | Anonymous |

Returns health, version, database status, registered project/function counts.

### 4.2 Platform Statistics

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/platform/stats` | PlatformAdmin, ProjectOwner |

Returns 30-day execution trends, success/failure rates, performance metrics.

### 4.3 System Health

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/system/health` | PlatformAdmin, ProjectOwner, Developer |

Returns overall system health, per-project health, alerts, and recommendations.

### 4.4 System Heal

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/system/heal` | PlatformAdmin |

Auto-heals failed functions, restarts crashed processes, recovers system.

### 4.5 System Optimize

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/system/optimize` | PlatformAdmin |

Optimizes function scheduling, memory usage, and execution order.

### 4.6 System Diagnose

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/system/diagnose` | PlatformAdmin, ProjectOwner, Developer |

Runs diagnostics across all projects and functions.

---

## 5. Development Kit Actions

### 5.1 Get Trigger Types

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/dev-kit/triggers/types` | PlatformAdmin, ProjectOwner, Developer |

Lists all available trigger types (built-in + custom registered).

### 5.2 Register Custom Trigger

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/dev-kit/triggers/register` | PlatformAdmin, ProjectOwner, Developer |

### 5.3 Add Trigger to Function

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/dev-kit/triggers/add` | PlatformAdmin, ProjectOwner, Developer |

### 5.4 Execute Trigger

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/dev-kit/triggers/{id}/execute` | PlatformAdmin, ProjectOwner, Developer |

### 5.5 Get Function Triggers

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/dev-kit/triggers/{project}/{function}` | PlatformAdmin, ProjectOwner, Developer |

### 5.6 Get Function Types

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/dev-kit/functions/types` | PlatformAdmin, ProjectOwner, Developer |

### 5.7 Register Custom Function

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/dev-kit/functions/register` | PlatformAdmin, ProjectOwner, Developer |

### 5.8 Create Package

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/dev-kit/packages` | PlatformAdmin, ProjectOwner, Developer |

```json
{
  "name": "bms-event-triggers",
  "displayName": "BMS Event Triggers",
  "description": "Domain event triggers for BMS",
  "tags": "events,bms,book-management"
}
```

### 5.9 DevKit Status

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/dev-kit/status` | PlatformAdmin, ProjectOwner, Developer |

---

## 6. Real-Time Streaming Events (SignalR)

WebSocket hub at: `/hubs/function-execution`

### 6.1 Client -> Server Methods

| Method | Description |
|--------|-------------|
| `SubscribeToTask(taskId)` | Subscribe to progress updates for a task |
| `UnsubscribeFromTask(taskId)` | Unsubscribe from task updates |
| `SubscribeToProject(projectNamespace)` | Subscribe to all events for a project |
| `SubscribeToSystem()` | Subscribe to platform-wide system events |
| `GetHubStats()` | Get connection and task statistics |

### 6.2 Server -> Client Events

| Event | Description |
|-------|-------------|
| `Connected` | Connection established confirmation |
| `TaskSubscribed` | Confirmed subscription to a task |
| `TaskUnsubscribed` | Confirmed unsubscription from a task |
| `TaskProgress` | Live progress update for a running task |
| `TaskCompleted` | Task finished successfully with results |
| `TaskError` | Task failed with error details |
| `ProjectSubscribed` | Confirmed subscription to a project |
| `SystemSubscribed` | Confirmed subscription to system events |
| `SystemEvent` | Platform-wide system event |
| `ProjectEvent` | Project-specific event |
| `HubStats` | Hub statistics response |

### 6.3 Streaming Actions

| Method | Endpoint | Auth |
|--------|----------|------|
| `POST` | `/api/v1/streaming/execute` | PlatformAdmin, ProjectOwner, Developer |
| `GET` | `/api/v1/streaming/tasks/{taskId}/status` | PlatformAdmin, ProjectOwner, Developer |
| `POST` | `/api/v1/streaming/tasks/{taskId}/cancel` | PlatformAdmin, ProjectOwner, Developer |
| `GET` | `/api/v1/streaming/tasks/active` | PlatformAdmin, ProjectOwner, Developer |
| `GET` | `/api/v1/streaming/tasks` | PlatformAdmin, ProjectOwner, Developer |
| `GET` | `/api/v1/streaming/capabilities` | Anonymous |

---

## 7. Function Lifecycle Commands

Commands sent to functions via the `FunctionCommand` system.

### 7.1 Command Types

| Command | Enum | Description |
|---------|------|-------------|
| **Execute** | `FunctionCommandType.Execute` | Run the function once |
| **HealthCheck** | `FunctionCommandType.HealthCheck` | Check function health |
| **Stop** | `FunctionCommandType.Stop` | Stop a running function |
| **Restart** | `FunctionCommandType.Restart` | Stop and re-start a function |
| **Configure** | `FunctionCommandType.Configure` | Update function configuration at runtime |

### 7.2 Function Status Lifecycle

``n```
Registered -> Scheduled -> Running -> Completed
                              |-> Failed
                              |-> Stopped
                              |-> Queued
                              |-> Disabled
``n```

---

## 8. System Actions

### 8.1 Project Management

| Action | Interface | Description |
|--------|-----------|-------------|
| Register Project | `IProjectManager.RegisterProjectAsync()` | Register a new project namespace |
| Unregister Project | `IProjectManager.UnregisterProjectAsync()` | Remove a project and its functions |
| Get Projects | `IProjectManager.GetProjectsAsync()` | List all registered projects |
| Get Project | `IProjectManager.GetProjectAsync()` | Get project details |
| Check Registration | `IProjectManager.IsProjectRegisteredAsync()` | Check if project exists |

### 8.2 Project API Endpoints

| Method | Endpoint | Auth |
|--------|----------|------|
| `GET` | `/api/v1/projects` | PlatformAdmin, ProjectOwner, Developer |
| `GET` | `/api/v1/projects/{namespace}` | PlatformAdmin, ProjectOwner, Developer |
| `POST` | `/api/v1/projects` | PlatformAdmin, ProjectOwner |
| `DELETE` | `/api/v1/projects/{namespace}` | PlatformAdmin |

### 8.3 Health Management

| Action | Interface | Description |
|--------|-----------|-------------|
| Overall Health | `IHealthManager.GetOverallHealthAsync()` | System-wide health across all projects |
| Project Health | `IHealthManager.GetProjectHealthAsync()` | Per-project health status |
| Function Health | `IHealthManager.GetFunctionHealthAsync()` | Individual function health |
| Heal Project | `IHealthManager.HealProjectAsync()` | Auto-recover a project's functions |
| Heal Function | `IHealthManager.HealFunctionAsync()` | Auto-recover a single function |

### 8.4 Process Management

| Action | Interface | Description |
|--------|-----------|-------------|
| Get Active Processes | `IProcessManager.GetActiveProcessesAsync()` | List all running function host processes |
| Get Process | `IProcessManager.GetProcessAsync()` | Get specific process info |
| Start Process | `IProcessManager.StartProcessAsync()` | Launch function host process |
| Stop Process | `IProcessManager.StopProcessAsync()` | Terminate function host process |
| Restart Process | `IProcessManager.RestartProcessAsync()` | Restart function host process |

---

## 9. Function Categories

Functions are organized into categories for management and filtering.

| Category | Enum Value | Description | Example |
|----------|-----------|-------------|---------|
| **Business** | `FunctionCategory.Business` | Core business logic | BookProcessor, OrderHandler |
| **System** | `FunctionCategory.System` | System maintenance | HealthMonitor, DatabaseCleaner |
| **Integration** | `FunctionCategory.Integration` | External system integration | EmailSender, WebhookRelay |
| **Security** | `FunctionCategory.Security` | Security and compliance | AuditLogger, AccessReviewer |
| **Analytics** | `FunctionCategory.Analytics` | Data analysis and reporting | UsageReporter, TrendAnalyzer |
| **Automation** | `FunctionCategory.Automation` | Workflow automation | OnboardingFlow, ReportScheduler |
| **Monitoring** | `FunctionCategory.Monitoring` | System monitoring | UptimeChecker, AlertRouter |
| **Custom** | `FunctionCategory.Custom` | Project-specific | Any user-defined category |

---

## 10. Workflow Orchestration

Chain multiple functions into automated workflows.

### 10.1 Workflow Definition

```csharp
var workflow = new WorkflowDefinition(
    Name: "BookProcessingPipeline",
    Steps: new[]
    {
        new WorkflowStep("ValidateBook"),
        new WorkflowStep("EnrichBookData"),
        new WorkflowStep("IndexForSearch"),
        new WorkflowStep("SendNotification"),
    },
    StopOnFailure: true
);

await orchestrator.ExecuteWorkflowAsync("BMS", workflow);
```

### 10.2 Workflow Step Options

```csharp
new WorkflowStep(
    FunctionName: "EnrichBookData",
    Parameters: new Dictionary<string, object> { { "source", "openlibrary" } },
    Dependencies: new[] { new WorkflowStep("ValidateBook") }
)
```

---

## Quick Reference: Total Actions & Triggers

| Category | Count | Items |
|----------|-------|-------|
| **Built-in Triggers** | 4 | HttpTrigger, TimerTrigger, QueueTrigger, BlobTrigger |
| **Custom Trigger Base** | 1 | CustomTriggerBase (DevKit) |
| **Custom Function Base** | 1 | CustomFunctionBase (DevKit) |
| **Function Actions (API)** | 6 | Register, Start, Stop, Execute, Get, List |
| **Platform Actions (API)** | 6 | Status, Stats, Health, Heal, Optimize, Diagnose |
| **DevKit Actions (API)** | 9 | Trigger types, Register trigger, Add trigger, Execute trigger, Get triggers, Function types, Register function, Create package, Status |
| **Streaming Actions (API)** | 6 | Execute, Status, Cancel, Active tasks, All tasks, Capabilities |
| **SignalR Hub Events** | 11 | Connected, TaskSubscribed, TaskUnsubscribed, TaskProgress, TaskCompleted, TaskError, ProjectSubscribed, SystemSubscribed, SystemEvent, ProjectEvent, HubStats |
| **SignalR Hub Methods** | 5 | SubscribeToTask, UnsubscribeFromTask, SubscribeToProject, SubscribeToSystem, GetHubStats |
| **Lifecycle Commands** | 5 | Execute, HealthCheck, Stop, Restart, Configure |
| **Function Categories** | 8 | Business, System, Integration, Security, Analytics, Automation, Monitoring, Custom |
| **Function States** | 7 | Registered, Scheduled, Running, Completed, Failed, Stopped, Disabled |
| **Health Statuses** | 6 | Unknown, Failed, Critical, Warning, Good, Excellent |
| **Alert Severities** | 4 | Info, Warning, Critical, Emergency |
| **Orchestration** | 2 | WorkflowDefinition, WorkflowStep |
| **Project Actions** | 5 | Register, Unregister, Get, GetAll, CheckRegistration |
| **Health Actions** | 5 | OverallHealth, ProjectHealth, FunctionHealth, HealProject, HealFunction |
| **Process Actions** | 5 | GetActive, Get, Start, Stop, Restart |
| **TOTAL** | **86** | |
