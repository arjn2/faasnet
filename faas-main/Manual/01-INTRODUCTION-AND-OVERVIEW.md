# Chapter 01: Introduction & Overview
## Understanding the Artichoke-FaaS Platform

---

## What is Artichoke-FaaS?

**Artichoke-FaaS** is a revolutionary distributed Function-as-a-Service platform built on .NET 9 that implements a **pure external architecture** where functions run as independent processes. Unlike traditional FaaS platforms, Artichoke-FaaS eliminates the confusion between internal and external functions by ensuring **everything runs externally**.

### Key Characteristics

- 🔄 **Zero Internal Functions**: All functions execute in separate processes
- 🛡️ **Process Isolation**: Complete separation between function executions
- 🌐 **HTTP Communication**: Functions communicate via HTTP-based protocols
- 🏗️ **Enterprise Architecture**: Production-ready with JWT auth, API versioning
- 📈 **Distributed by Design**: Built for scalability and fault tolerance

---

## Platform Philosophy

### The "Pure External" Approach

Traditional FaaS platforms mix internal and external function execution, leading to:
- ❌ Architectural confusion
- ❌ Resource contention  
- ❌ Difficult debugging
- ❌ Limited scalability

**Artichoke-FaaS Solution:**
```
✅ Every function = Separate process
✅ Clear architectural boundaries
✅ Independent resource management  
✅ Simplified debugging & monitoring
✅ Unlimited horizontal scaling
```

### Core Design Principles

1. **Separation of Concerns**: Platform management vs. Function execution
2. **Process Isolation**: Functions cannot interfere with each other
3. **Fault Tolerance**: Function failures don't affect the platform
4. **Scalability**: Add functions by adding processes
5. **Observability**: Clear monitoring at process level

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Artichoke-FaaS Platform                 │
├─────────────────┬─────────────────┬─────────────────────────┤
│   Platform      │   Development   │    Distributed         │
│   Services      │   Kit System    │    Function Manager    │
│                 │                 │                        │
│ • Registry      │ • ITrigger      │ • Process Management   │
│ • Scheduling    │ • IFactory      │ • Health Monitoring    │
│ • Monitoring    │ • Built-ins     │ • Communication        │
└─────────────────┴─────────────────┴─────────────────────────┘
                           │
                    HTTP Communication
                           │
┌─────────────────────────────────────────────────────────────┐
│                  External Function Processes               │
├──────────────────┬──────────────────┬─────────────────────────┤
│  BookProcessor   │  HealthMonitor   │   AuditLogger          │
│  Function        │  Function        │   Function             │
│                  │                  │                        │
│ • Business Logic │ • System Health  │ • Security Auditing   │
│ • 5min Interval  │ • 2min Interval  │ • 30sec Interval       │
│ • PID: 1234      │ • PID: 1235      │ • PID: 1236            │
└──────────────────┴──────────────────┴─────────────────────────┘
```

---

## System Components

### 1. **Artichoke.FaaS.Platform** 🏗️
- **Role**: Central management and orchestration
- **Features**: 
  - Function registry and discovery
  - Process lifecycle management
  - Health monitoring and metrics
  - Real-time communication via SignalR

### 2. **Artichoke.FaaS.Core** ⚡
- **Role**: Foundational interfaces and abstractions
- **Features**:
  - `IDevelopmentKit` interface
  - `ITrigger` and `ITriggerFactory` contracts
  - Built-in trigger implementations (HTTP, Timer)

### 3. **Artichoke.FaaS.Client** 📱
- **Role**: Administrative interface and CLI
- **Features**:
  - Function management commands
  - Real-time monitoring dashboard
  - Configuration utilities

### 4. **BMS.FunctionHost** 🚀
- **Role**: External function process host
- **Features**:
  - Independent process execution
  - HTTP-based registration and communication
  - Health checks and metrics reporting

---

## Built-in Triggers

Artichoke-FaaS comes with **2 production-ready triggers**:

### HttpTrigger 🌐
```csharp
// Responds to HTTP requests
[HttpTrigger(AuthorizationLevel.Function, "get", "post")]
public async Task<IActionResult> ProcessRequest(HttpRequest req)
{
    // Your function logic here
}
```

### TimerTrigger ⏰
```csharp
// Executes on schedule (cron expressions)
[TimerTrigger("0 */5 * * * *")] // Every 5 minutes
public async Task ProcessScheduled(TimerInfo timer)
{
    // Your scheduled function logic here  
}
```

---

## BMS Reference Implementation

The platform includes a complete **Book Management System (BMS)** that demonstrates:

### 🏢 Enterprise Architecture
- **3-Layer Design**: API, Business Logic, Data Access
- **Authentication**: JWT with Identity Framework
- **API Versioning**: v1.0 (XML) and v2.0 (JSON)
- **Event-Driven**: Loose coupling with custom event system

### 📊 Real Functions
- **BookProcessorFunction**: Processes book operations (5min intervals)
- **HealthMonitorFunction**: System health monitoring (2min intervals)  
- **AuditLoggerFunction**: Security audit logging (30sec intervals)

### 🔧 Production Features
- **Database**: SQL Server with Entity Framework migrations
- **Testing**: Complete unit test suite with Moq
- **Documentation**: Comprehensive API documentation
- **Security**: Role-based authorization (Admin, User)

---

## Technology Stack

### Core Technologies
- **.NET 9**: Modern framework with latest performance improvements
- **ASP.NET Core**: Web API and MVC architecture
- **Entity Framework Core**: Database access and migrations
- **SignalR**: Real-time communication
- **Identity Framework**: Authentication and authorization

### Development Tools
- **xUnit**: Unit testing framework
- **Moq**: Mocking framework for tests
- **Swagger/OpenAPI**: API documentation
- **Mapster**: Object mapping

### Database Support
- **SQL Server**: Primary production database
- **SQLite**: Development and testing database

---

## Use Cases

### 🏭 **Enterprise Applications**
- Microservices decomposition
- Legacy system modernization
- Event-driven architectures
- Background job processing

### 🔄 **Integration Scenarios**
- API gateway backends
- Data processing pipelines
- Notification systems
- Audit and compliance logging

### 📈 **Scalability Requirements**
- High-throughput processing
- Auto-scaling workloads
- Resource-intensive operations
- Multi-tenant applications

---

## Getting Started Path

1. **📖 Read This Manual**: Understand concepts and architecture
2. **🚀 Quick Start**: Run the demo to see it in action
3. **🛠️ Setup Environment**: Install prerequisites and tools
4. **👨‍💻 Build Functions**: Create your first function
5. **🏗️ Deploy**: Run in production environment

---

## What's Next?

The next chapter covers the **Quick Start Guide** where you'll:
- Run the platform in under 5 minutes
- See live functions executing
- Explore the administrative interface
- Understand the basic workflows

Continue to: [Chapter 02: Quick Start Guide](02-QUICK-START-GUIDE.md)

---

## Support & Resources

- 📚 **Documentation**: Complete manual (this document)
- 🐛 **Issues**: GitHub repository issues
- 💬 **Community**: Developer forums and discussions
- 📧 **Enterprise**: Commercial support available
