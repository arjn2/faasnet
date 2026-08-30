# Chapter 03: Installation & Setup

## Table of Contents
- [System Requirements](#system-requirements)
- [Installation Methods](#installation-methods)
- [Environment Configuration](#environment-configuration)
- [Database Setup](#database-setup)
- [Service Configuration](#service-configuration)
- [SSL/TLS Configuration](#ssl-tls-configuration)
- [Production Deployment](#production-deployment)
- [Verification & Testing](#verification-testing)
- [Troubleshooting](#troubleshooting)

---

## System Requirements

### Hardware Requirements

**Minimum Configuration:**
- **CPU**: 2 cores (x64 architecture)
- **RAM**: 4 GB
- **Storage**: 10 GB available space
- **Network**: HTTP/HTTPS access

**Recommended Configuration:**
- **CPU**: 4+ cores (x64 architecture)  
- **RAM**: 8+ GB
- **Storage**: 50+ GB SSD
- **Network**: Dedicated network interface

**Production Configuration:**
- **CPU**: 8+ cores (x64 architecture)
- **RAM**: 16+ GB
- **Storage**: 100+ GB NVMe SSD
- **Network**: Load balancer ready

### Software Prerequisites

**Operating System:**
- Windows 10/11 (x64)
- Windows Server 2019/2022
- Linux (Ubuntu 20.04+, CentOS 8+)
- macOS 12+ (development only)

**Runtime Dependencies:**
```bash
# .NET 8.0 Runtime (Required)
.NET 8.0 Runtime or SDK

# SQL Server (Required)
SQL Server 2019+ or SQL Server Express
# OR
SQL Server LocalDB (development)

# Optional but Recommended
IIS 10+ (Windows)
Nginx/Apache (Linux)
Redis (caching)
```

---

## Installation Methods

### Method 1: Quick Installation (Recommended)

**Step 1: Download Release Package**
```bash
# Download latest release
curl -L https://github.com/your-org/artichoke-faas/releases/latest/download/artichoke-faas.zip -o artichoke-faas.zip

# Extract package
unzip artichoke-faas.zip -d /opt/artichoke-faas
cd /opt/artichoke-faas
```

**Step 2: Run Installation Script**
```bash
# Windows
.\install.ps1

# Linux/macOS
chmod +x install.sh
./install.sh
```

### Method 2: Source Installation

**Step 1: Clone Repository**
```bash
git clone https://github.com/your-org/artichoke-faas.git
cd artichoke-faas
```

**Step 2: Build Solution**
```bash
# Restore packages
dotnet restore

# Build solution
dotnet build --configuration Release

# Publish applications
dotnet publish src/Artichoke.Platform/Artichoke.Platform.csproj -c Release -o publish/platform
dotnet publish src/Artichoke.Runtime/Artichoke.Runtime.csproj -c Release -o publish/runtime
```

### Method 3: Docker Installation

**Step 1: Docker Compose Setup**
```yaml
# docker-compose.yml
version: '3.8'

services:
  artichoke-platform:
    image: artichoke/platform:latest
    ports:
      - "5000:5000"
      - "5001:5001"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=ArtichokeFaaS;User Id=sa;Password=YourStrong@Password;
    depends_on:
      - sql-server
    volumes:
      - ./config:/app/config
      - ./logs:/app/logs

  artichoke-runtime:
    image: artichoke/runtime:latest
    ports:
      - "5100:5100"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - PlatformUrl=http://artichoke-platform:5000
    depends_on:
      - artichoke-platform

  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Password
    ports:
      - "1433:1433"
    volumes:
      - sql-data:/var/opt/mssql

volumes:
  sql-data:
```

**Step 2: Start Services**
```bash
docker-compose up -d
```

---

## Environment Configuration

### Configuration Files Structure

```
/config/
├── appsettings.json                 # Main configuration
├── appsettings.Production.json      # Production overrides
├── appsettings.Development.json     # Development overrides
├── logging.json                     # Logging configuration
├── security.json                    # Security settings
└── functions/                       # Function configurations
    ├── http-triggers.json
    ├── timer-triggers.json
    └── custom-configs/
```

### Main Configuration (appsettings.json)

```json
{
  "Artichoke": {
    "Platform": {
      "BaseUrl": "https://localhost:5001",
      "ApiVersion": "v2.0",
      "MaxConcurrentFunctions": 100,
      "FunctionTimeout": "00:05:00",
      "HealthCheckInterval": "00:00:30"
    },
    "Runtime": {
      "BaseUrl": "https://localhost:5101",
      "ProcessIsolation": true,
      "MaxProcesses": 50,
      "ProcessRecycleThreshold": 1000,
      "WarmupEnabled": true
    },
    "Storage": {
      "Provider": "FileSystem",
      "BasePath": "/var/artichoke/storage",
      "MaxFileSize": "100MB",
      "RetentionDays": 30
    },
    "Monitoring": {
      "MetricsEnabled": true,
      "TracingEnabled": true,
      "LogLevel": "Information",
      "HealthChecks": {
        "Enabled": true,
        "Port": 5050,
        "Path": "/health"
      }
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ArtichokeFaaS;Integrated Security=true;TrustServerCertificate=true;",
    "Redis": "localhost:6379"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Artichoke": "Debug"
    }
  }
}
```

### Environment Variables

**Platform Variables:**
```bash
# Core Settings
ARTICHOKE_ENVIRONMENT=Production
ARTICHOKE_BASE_URL=https://your-domain.com
ARTICHOKE_API_VERSION=v2.0

# Database
ARTICHOKE_DB_CONNECTION="Server=prod-sql;Database=ArtichokeFaaS;User Id=artichoke_user;Password=SecurePassword123;"

# Security
ARTICHOKE_JWT_SECRET="your-256-bit-secret-key-here"
ARTICHOKE_JWT_ISSUER="artichoke-faas"
ARTICHOKE_JWT_AUDIENCE="artichoke-api"

# Performance
ARTICHOKE_MAX_CONCURRENT_FUNCTIONS=200
ARTICHOKE_FUNCTION_TIMEOUT=300
ARTICHOKE_PROCESS_RECYCLE_THRESHOLD=2000

# Monitoring
ARTICHOKE_METRICS_ENABLED=true
ARTICHOKE_TRACING_ENABLED=true
ARTICHOKE_LOG_LEVEL=Information
```

---

## Database Setup

### SQL Server Configuration

**Step 1: Create Database**
```sql
-- Create database
CREATE DATABASE ArtichokeFaaS
GO

-- Use database
USE ArtichokeFaaS
GO

-- Create application user
CREATE LOGIN artichoke_user WITH PASSWORD = 'SecurePassword123!'
CREATE USER artichoke_user FOR LOGIN artichoke_user
ALTER ROLE db_owner ADD MEMBER artichoke_user
GO
```

**Step 2: Run Migration Scripts**
```bash
# Navigate to database scripts
cd database/migrations

# Run initial schema
sqlcmd -S localhost -d ArtichokeFaaS -i 001_InitialSchema.sql

# Run subsequent migrations
sqlcmd -S localhost -d ArtichokeFaaS -i 002_Functions.sql
sqlcmd -S localhost -d ArtichokeFaaS -i 003_Triggers.sql
sqlcmd -S localhost -d ArtichokeFaaS -i 004_Monitoring.sql
```

**Step 3: Verify Database Setup**
```sql
-- Check tables
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME

-- Expected tables:
-- Functions
-- FunctionExecutions
-- Triggers
-- TriggerSchedules
-- SystemMetrics
-- AuditLogs
-- Configurations
```

### Database Schema Overview

**Core Tables:**
```sql
-- Functions table
CREATE TABLE Functions (
    Id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),
    Name nvarchar(255) NOT NULL UNIQUE,
    Description nvarchar(max),
    AssemblyPath nvarchar(500) NOT NULL,
    ClassName nvarchar(255) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
)

-- Triggers table
CREATE TABLE Triggers (
    Id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),
    FunctionId uniqueidentifier NOT NULL,
    TriggerType nvarchar(50) NOT NULL, -- 'Http', 'Timer', 'Custom'
    Configuration nvarchar(max) NOT NULL, -- JSON configuration
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (FunctionId) REFERENCES Functions(Id)
)

-- Execution logs
CREATE TABLE FunctionExecutions (
    Id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),
    FunctionId uniqueidentifier NOT NULL,
    TriggerId uniqueidentifier,
    StartTime datetime2 NOT NULL,
    EndTime datetime2,
    Status nvarchar(50) NOT NULL, -- 'Running', 'Completed', 'Failed'
    Input nvarchar(max),
    Output nvarchar(max),
    ErrorMessage nvarchar(max),
    ExecutionTimeMs int,
    FOREIGN KEY (FunctionId) REFERENCES Functions(Id),
    FOREIGN KEY (TriggerId) REFERENCES Triggers(Id)
)
```

---

## Service Configuration

### Windows Service Installation

**Step 1: Install Platform Service**
```powershell
# Install as Windows Service
sc create "Artichoke Platform" binPath="C:\Program Files\Artichoke\Platform\Artichoke.Platform.exe" start=auto
sc description "Artichoke Platform" "Artichoke FaaS Platform Service"

# Install Runtime Service
sc create "Artichoke Runtime" binPath="C:\Program Files\Artichoke\Runtime\Artichoke.Runtime.exe" start=auto
sc description "Artichoke Runtime" "Artichoke FaaS Runtime Service"

# Start services
sc start "Artichoke Platform"
sc start "Artichoke Runtime"
```

**Step 2: Service Configuration Files**
```xml
<!-- Artichoke.Platform.exe.config -->
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <add key="Environment" value="Production" />
    <add key="ServiceName" value="Artichoke Platform" />
    <add key="LogPath" value="C:\Logs\Artichoke\Platform" />
  </appSettings>
</configuration>
```

### Linux Systemd Service

**Step 1: Create Service Files**
```bash
# Create platform service
sudo tee /etc/systemd/system/artichoke-platform.service > /dev/null <<EOF
[Unit]
Description=Artichoke FaaS Platform
After=network.target

[Service]
Type=notify
ExecStart=/opt/artichoke/platform/Artichoke.Platform
Restart=always
RestartSec=10
User=artichoke
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=/opt/dotnet
WorkingDirectory=/opt/artichoke/platform

[Install]
WantedBy=multi-user.target
EOF

# Create runtime service
sudo tee /etc/systemd/system/artichoke-runtime.service > /dev/null <<EOF
[Unit]
Description=Artichoke FaaS Runtime
After=network.target artichoke-platform.service
Requires=artichoke-platform.service

[Service]
Type=notify
ExecStart=/opt/artichoke/runtime/Artichoke.Runtime
Restart=always
RestartSec=10
User=artichoke
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=/opt/dotnet
WorkingDirectory=/opt/artichoke/runtime

[Install]
WantedBy=multi-user.target
EOF
```

**Step 2: Enable and Start Services**
```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable services
sudo systemctl enable artichoke-platform
sudo systemctl enable artichoke-runtime

# Start services
sudo systemctl start artichoke-platform
sudo systemctl start artichoke-runtime

# Check status
sudo systemctl status artichoke-platform
sudo systemctl status artichoke-runtime
```

---

## SSL/TLS Configuration

### Certificate Generation

**Step 1: Generate Self-Signed Certificate (Development)**
```bash
# Generate private key
openssl genrsa -out artichoke.key 2048

# Generate certificate signing request
openssl req -new -key artichoke.key -out artichoke.csr -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"

# Generate self-signed certificate
openssl x509 -req -days 365 -in artichoke.csr -signkey artichoke.key -out artichoke.crt

# Convert to PFX format
openssl pkcs12 -export -out artichoke.pfx -inkey artichoke.key -in artichoke.crt -password pass:YourCertPassword
```

**Step 2: Configure HTTPS in appsettings.json**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      },
      "Https": {
        "Url": "https://0.0.0.0:5001",
        "Certificate": {
          "Path": "/etc/ssl/certs/artichoke.pfx",
          "Password": "YourCertPassword"
        }
      }
    }
  }
}
```

### Production SSL with Let's Encrypt

**Step 1: Install Certbot**
```bash
# Ubuntu/Debian
sudo apt install certbot

# CentOS/RHEL
sudo yum install certbot
```

**Step 2: Generate Certificate**
```bash
# Generate certificate
sudo certbot certonly --standalone -d your-domain.com -d api.your-domain.com

# Certificate files will be at:
# /etc/letsencrypt/live/your-domain.com/fullchain.pem
# /etc/letsencrypt/live/your-domain.com/privkey.pem
```

**Step 3: Configure Auto-Renewal**
```bash
# Add to crontab
sudo crontab -e

# Add line for auto-renewal
0 12 * * * /usr/bin/certbot renew --quiet && systemctl restart artichoke-platform
```

---

## Production Deployment

### Load Balancer Configuration

**Nginx Configuration:**
```nginx
# /etc/nginx/sites-available/artichoke
upstream artichoke_platform {
    server 127.0.0.1:5000;
    server 127.0.0.1:5002; # Scale horizontally
}

upstream artichoke_runtime {
    server 127.0.0.1:5100;
    server 127.0.0.1:5102; # Scale horizontally
}

server {
    listen 80;
    server_name your-domain.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name your-domain.com;

    ssl_certificate /etc/letsencrypt/live/your-domain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;

    # Platform API
    location /api/ {
        proxy_pass http://artichoke_platform;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # WebSocket support
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }

    # Function execution
    location /functions/ {
        proxy_pass http://artichoke_runtime;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Increase timeouts for long-running functions
        proxy_connect_timeout 60s;
        proxy_send_timeout 300s;
        proxy_read_timeout 300s;
    }
}
```

### High Availability Setup

**Multi-Instance Configuration:**
```yaml
# docker-compose.prod.yml
version: '3.8'

services:
  # Platform instances
  artichoke-platform-1:
    image: artichoke/platform:latest
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - INSTANCE_ID=platform-1
    deploy:
      replicas: 2
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G

  artichoke-platform-2:
    image: artichoke/platform:latest
    ports:
      - "5002:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - INSTANCE_ID=platform-2

  # Runtime instances
  artichoke-runtime-1:
    image: artichoke/runtime:latest
    ports:
      - "5100:5100"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - INSTANCE_ID=runtime-1
    deploy:
      replicas: 3
      resources:
        limits:
          memory: 4G
        reservations:
          memory: 2G

  # Database cluster
  sql-server-primary:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Password
    volumes:
      - sql-primary-data:/var/opt/mssql

  # Redis for caching and session
  redis:
    image: redis:7-alpine
    command: redis-server --appendonly yes
    volumes:
      - redis-data:/data

volumes:
  sql-primary-data:
  redis-data:
```

---

## Verification & Testing

### Post-Installation Verification

**Step 1: Health Check Verification**
```bash
# Check platform health
curl -k https://localhost:5001/health

# Expected response:
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "storage": "Healthy",
    "runtime": "Healthy"
  },
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Step 2: API Endpoint Testing**
```bash
# Test platform API
curl -k https://localhost:5001/api/v2/functions

# Test runtime API
curl -k https://localhost:5101/api/status

# Test function execution
curl -X POST -k https://localhost:5101/functions/hello-world \
  -H "Content-Type: application/json" \
  -d '{"name": "Test User"}'
```

**Step 3: Service Status Verification**
```bash
# Windows
sc query "Artichoke Platform"
sc query "Artichoke Runtime"

# Linux
systemctl status artichoke-platform
systemctl status artichoke-runtime

# Docker
docker-compose ps
```

### Load Testing

**Step 1: Install Testing Tools**
```bash
# Install Apache Bench
sudo apt install apache2-utils

# Or install wrk
sudo apt install wrk
```

**Step 2: Run Load Tests**
```bash
# Test platform endpoints
ab -n 1000 -c 10 https://localhost:5001/api/v2/functions

# Test function execution
wrk -t4 -c100 -d30s --script=load-test.lua https://localhost:5101/functions/hello-world
```

**Load Test Script (load-test.lua):**
```lua
wrk.method = "POST"
wrk.body = '{"name": "Load Test User", "timestamp": "' .. os.date("%Y-%m-%dT%H:%M:%SZ") .. '"}'
wrk.headers["Content-Type"] = "application/json"

function response(status, headers, body)
    if status ~= 200 then
        print("Error: " .. status .. " - " .. body)
    end
end
```

---

## Troubleshooting

### Common Installation Issues

**Issue 1: Database Connection Failed**
```bash
# Symptoms
System.Data.SqlClient.SqlException: Cannot open database

# Solutions
1. Verify SQL Server is running
2. Check connection string
3. Verify user permissions
4. Test connection manually

# Manual connection test
sqlcmd -S localhost -d ArtichokeFaaS -U artichoke_user -P SecurePassword123!
```

**Issue 2: Service Won't Start**
```bash
# Windows - Check Event Log
Get-EventLog -LogName Application -Source "Artichoke Platform" -Newest 10

# Linux - Check journal
sudo journalctl -u artichoke-platform -n 50

# Common fixes
1. Check file permissions
2. Verify .NET runtime installation
3. Check configuration file syntax
4. Verify port availability
```

**Issue 3: SSL Certificate Issues**
```bash
# Symptoms
SSL handshake failed / Certificate validation error

# Solutions
1. Verify certificate validity
   openssl x509 -in artichoke.crt -text -noout

2. Check certificate permissions
   chmod 644 /etc/ssl/certs/artichoke.crt
   chmod 600 /etc/ssl/private/artichoke.key

3. Verify certificate chain
   openssl verify -CAfile /etc/ssl/certs/ca-certificates.crt artichoke.crt
```

### Performance Issues

**Issue 1: High Memory Usage**
```json
// Adjust configuration
{
  "Artichoke": {
    "Runtime": {
      "MaxProcesses": 25,          // Reduce from 50
      "ProcessRecycleThreshold": 500, // Reduce from 1000
      "GarbageCollectionMode": "Server"
    }
  }
}
```

**Issue 2: Slow Function Execution**
```json
// Enable performance profiling
{
  "Artichoke": {
    "Monitoring": {
      "PerformanceCounters": true,
      "DetailedTiming": true,
      "MemoryProfiling": true
    }
  }
}
```

### Diagnostic Commands

**Collect System Information:**
```bash
# System info
uname -a
free -h
df -h
netstat -tlnp

# .NET info
dotnet --info
dotnet --list-runtimes

# Service logs
tail -f /var/log/artichoke/platform.log
tail -f /var/log/artichoke/runtime.log
```

**Database Diagnostics:**
```sql
-- Check database size
SELECT 
    DB_NAME() as DatabaseName,
    (SELECT SUM(size * 8.0 / 1024) FROM sys.master_files WHERE database_id = DB_ID()) as SizeMB

-- Check active connections
SELECT 
    session_id, 
    login_name, 
    program_name, 
    client_interface_name,
    login_time
FROM sys.dm_exec_sessions 
WHERE is_user_process = 1

-- Check long-running queries
SELECT 
    r.session_id,
    r.start_time,
    r.status,
    r.command,
    r.total_elapsed_time,
    t.text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.total_elapsed_time > 5000
```

### Log Analysis

**Key Log Locations:**
```bash
# Platform logs
/var/log/artichoke/platform/
├── application.log      # Main application log
├── performance.log      # Performance metrics
├── security.log         # Security events
└── errors.log          # Error details

# Runtime logs
/var/log/artichoke/runtime/
├── execution.log        # Function executions
├── process.log         # Process management
└── system.log          # System events
```

**Important Log Patterns:**
```bash
# Search for errors
grep -i "error\|exception\|failed" /var/log/artichoke/platform/application.log

# Monitor function executions
tail -f /var/log/artichoke/runtime/execution.log | grep "EXECUTION_"

# Performance issues
grep "SLOW_QUERY\|TIMEOUT\|HIGH_MEMORY" /var/log/artichoke/platform/performance.log
```

---

## Next Steps

After completing the installation and setup:

1. **Review Chapter 04: Platform Architecture** to understand the system components
2. **Explore Chapter 05: Built-in Triggers System** to configure triggers
3. **Read Chapter 07: Function Interface & Implementation** to start developing functions
4. **Configure monitoring and alerting** (covered in later chapters)
5. **Set up backup and disaster recovery** procedures

For production deployments, ensure you review the security and monitoring chapters before going live.

---

*This completes Chapter 03: Installation & Setup. The next chapter covers Platform Architecture in detail.*
