# Chapter 02: Quick Start Guide
## Get Artichoke-FaaS Running in 5 Minutes

---

## Prerequisites Check

Before starting, ensure you have:

```powershell
# Check .NET 9 installation
dotnet --version
# Should show: 9.0.x or higher

# Check SQL Server LocalDB (optional - SQLite fallback available)
sqllocaldb info
# Should list available LocalDB instances
```

### Required Software
- ✅ **.NET 9 SDK** or higher
- ✅ **Visual Studio 2022** or **VS Code** (recommended)
- ✅ **PowerShell 5.1+** (for Windows)
- ⚠️ **SQL Server LocalDB** (optional - SQLite used as fallback)

---

## Method 1: One-Click Quick Start (Recommended)

### Step 1: Run the Demo Script

```powershell
# Navigate to the repository root
cd c:\Users\Acer\Desktop\faas

# Execute the quick start batch file
.\QUICK_START.bat
```

This script will:
1. 🔄 Build all projects
2. 🏗️ Setup databases  
3. 🚀 Start the Platform
4. 🌐 Launch the BMS API
5. 📱 Open the Admin Client
6. 🎯 Run sample functions

### Step 2: Verify Installation

You should see multiple console windows opening:

```
✅ Artichoke.FaaS.Platform (Port: 5000)
✅ BMS-API (Port: 7111) 
✅ BMS-UI (Port: 5001)
✅ Function Host Processes (Various PIDs)
```

### Step 3: Access the Platform

**Main Interfaces:**
- 🌐 **Platform Dashboard**: http://localhost:5000
- 📚 **BMS API**: http://localhost:7111/swagger
- 💻 **BMS UI**: http://localhost:5001
- 📊 **Admin Client**: Console application

---

## Method 2: Manual Step-by-Step

If you prefer to understand each step:

### Step 1: Build the Platform

```powershell
# Build the core platform
dotnet build Artichoke-FaaS-Platform.sln --configuration Release

# Build the BMS reference implementation
dotnet build BMS-API.sln --configuration Release
```

### Step 2: Setup Databases

```powershell
# Navigate to BMS-API (main database)
cd BMS-API

# Run Entity Framework migrations
dotnet ef database update

# Navigate to Platform (SQLite database)
cd ../Artichoke.FaaS.Platform

# Initialize platform database
dotnet run --setup-database
```

### Step 3: Start Platform Services

```powershell
# Terminal 1: Start the Platform
cd Artichoke.FaaS.Platform
dotnet run --urls "http://localhost:5000"

# Terminal 2: Start BMS API  
cd ../BMS-API
dotnet run --urls "http://localhost:7111"

# Terminal 3: Start BMS UI (optional)
cd ../BMS-UI  
dotnet run --urls "http://localhost:5001"
```

### Step 4: Start Function Hosts

```powershell
# Terminal 4: Book Processor Function
cd BMS.FunctionHost
dotnet run --function BookProcessor

# Terminal 5: Health Monitor Function  
cd BMS.FunctionHost
dotnet run --function HealthMonitor

# Terminal 6: Audit Logger Function
cd BMS.FunctionHost  
dotnet run --function AuditLogger
```

---

## Verify Everything is Working

### 1. Check Platform Health

```powershell
# Test platform endpoint
Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET
```

**Expected Response:**
```json
{
  "status": "Healthy",
  "totalFunctions": 3,
  "activeFunctions": 3,
  "platformVersion": "3.3.2"
}
```

### 2. Check Function Registration

```powershell
# List registered functions
Invoke-RestMethod -Uri "http://localhost:5000/api/functions" -Method GET
```

**Expected Response:**
```json
[
  {
    "name": "BookProcessor",
    "status": "Running",
    "processId": 1234,
    "lastExecution": "2025-10-25T10:30:00Z"
  },
  {
    "name": "HealthMonitor", 
    "status": "Running",
    "processId": 1235,
    "lastExecution": "2025-10-25T10:29:00Z"
  },
  {
    "name": "AuditLogger",
    "status": "Running", 
    "processId": 1236,
    "lastExecution": "2025-10-25T10:29:30Z"
  }
]
```

### 3. Test BMS API

```powershell
# Get all books (no authentication required)
Invoke-RestMethod -Uri "http://localhost:7111/api/v2.0/Test/books" -Method GET
```

**Expected Response:**
```json
[
  {
    "id": 1,
    "title": "Sherlock Holmes",
    "author": "Doyle", 
    "publishedYear": 1979
  },
  {
    "id": 2,
    "title": "Tom Holland",
    "author": "Tom",
    "publishedYear": 2001
  },
  {
    "id": 3,
    "title": "Tarzan",
    "author": "Rich Burroughs",
    "publishedYear": 2000
  }
]
```

---

## Interactive Demo

### 1. Login and Get JWT Token

```powershell
# Login as admin user
$loginData = @{
    username = "admin"
    password = "Admin@123"  
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:7111/api/v2.0/Test/login" `
    -Method POST -Body $loginData -ContentType "application/json"

$token = $response.token
Write-Host "JWT Token: $token"
```

### 2. Add a New Book (Authenticated)

```powershell
# Add a new book using the JWT token
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$bookData = @{
    title = "Artichoke-FaaS Guide"
    author = "Platform Team"
    publishedYear = 2025
} | ConvertTo-Json

$newBook = Invoke-RestMethod -Uri "http://localhost:7111/api/v2.0/Test/books" `
    -Method POST -Body $bookData -Headers $headers

Write-Host "Book added: $($newBook)"
```

### 3. Monitor Function Execution

```powershell
# Watch functions execute in real-time
for ($i = 1; $i -le 10; $i++) {
    $functions = Invoke-RestMethod -Uri "http://localhost:5000/api/functions" -Method GET
    
    Write-Host "=== Execution Check $i ===" -ForegroundColor Green
    foreach ($func in $functions) {
        Write-Host "$($func.name): Last execution $($func.lastExecution)" -ForegroundColor Yellow
    }
    
    Start-Sleep -Seconds 30
}
```

---

## Understanding What You See

### Function Execution Patterns

**BookProcessorFunction** (5-minute intervals):
- Processes book operations queue
- Simulates business logic processing
- Reports processed items count

**HealthMonitorFunction** (2-minute intervals):  
- Checks database connectivity
- Monitors application services
- Reports system health metrics

**AuditLoggerFunction** (30-second intervals):
- Processes audit event queue
- Logs security-related activities  
- Maintains compliance records

### Console Output Examples

```
🔄 BookProcessor execution #3 started
📊 Processed 7 book operations successfully
✅ BookProcessor execution #3 completed in 245ms

🏥 HealthMonitor execution #5 started  
✅ Database: Healthy (234 books accessible)
✅ Services: All systems operational
✅ HealthMonitor execution #5 completed in 89ms

🔒 AuditLogger execution #12 started
📝 Processed 3 audit events successfully
✅ AuditLogger execution #12 completed in 56ms
```

---

## Explore the Admin Interface

### Platform Dashboard (http://localhost:5000)

Navigate through the dashboard to see:
- 📊 **Function Status**: Real-time execution status
- 📈 **Metrics**: Performance and health metrics  
- 🔧 **Management**: Start/stop/restart functions
- 📋 **Logs**: Execution history and errors

### BMS UI (http://localhost:5001)

The Book Management System UI demonstrates:
- 📚 **Book Library**: Browse and search books
- 👤 **User Authentication**: Login/logout functionality  
- ➕ **CRUD Operations**: Add, edit, delete books (Admin only)
- 🔒 **Role-based Access**: Different permissions per user role

### API Documentation (http://localhost:7111/swagger)

Explore the comprehensive API documentation:
- 📖 **Version 1.0**: Read-only XML API
- 📖 **Version 2.0**: Full CRUD JSON API
- 🔐 **Authentication**: JWT bearer token examples
- 🧪 **Try It Out**: Interactive API testing

---

## Troubleshooting Quick Issues

### Port Conflicts

```powershell
# Check what's using your ports
netstat -an | findstr ":5000 :7111 :5001"

# Kill processes if needed
Get-Process | Where-Object {$_.ProcessName -eq "dotnet"} | Stop-Process -Force
```

### Database Issues

```powershell
# Reset databases
cd BMS-API
dotnet ef database drop -f
dotnet ef database update

cd ../Artichoke.FaaS.Platform  
Remove-Item ArtichokeFaaSPlatform.db -Force
dotnet run --setup-database
```

### Build Errors

```powershell
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build --configuration Release
```

---

## What's Next?

Now that you have the platform running, you can:

1. **📖 Learn the Architecture**: [Chapter 04: Platform Architecture](04-PLATFORM-ARCHITECTURE.md)
2. **🛠️ Build Your First Function**: [Chapter 07: Function Interface & Implementation](07-FUNCTION-INTERFACE-IMPLEMENTATION.md)
3. **🔧 Setup Development Environment**: [Chapter 03: Installation & Setup](03-INSTALLATION-AND-SETUP.md)

---

## Success Checklist

- ✅ Platform running on port 5000
- ✅ BMS API running on port 7111  
- ✅ 3 functions registered and executing
- ✅ Can authenticate and get JWT token
- ✅ Can perform CRUD operations on books
- ✅ Functions show regular execution in logs
- ✅ All health checks passing

**🎉 Congratulations! You now have a fully functional Artichoke-FaaS platform running with real distributed functions.**

Continue to: [Chapter 03: Installation & Setup](03-INSTALLATION-AND-SETUP.md)
