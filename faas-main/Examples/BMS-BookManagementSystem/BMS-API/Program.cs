using Artichoke.FaaS.Core.Interfaces;
using Artichoke.FaaS.Runtime;
using BMS.Core.Events;
using BMS.Core.Interfaces;
using BMS.Core.Services;
using BMS.External.Data;
using BMS.External.Seeding;
using BMS.External.Events;
using BMS.External.FaaS;
using BMS.External.Persistence;
using BMS.Interface.Services;
using BMS_API.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;

// -----------------------------------------------------------------------------
// BMS-API v8.0.6 — Artichoke architecture + Artichoke-FaaS framework.
//
// Dependency graph:
//   BMS-API
//     └── BMS.External  ──►  Artichoke.FaaS.Core    (ICustomFunction, ITrigger, IDomainEventBus)
//                      └──►  Artichoke.FaaS.Runtime  (FunctionHost, TimerTrigger, DomainEventTrigger, TriggerHost)
//         └── BMS.Interface  (BookApplicationService, DTOs, IEventPublisher)
//             └── BMS.Core   (Book domain, BookDomainService, IBookRepository, BMS.Core.Events.IDomainEvent : Artichoke.FaaS.Core.Interfaces.IDomainEvent)
//
// What the framework provides (v8.0.6):
//   - IFunctionHost   (IFunctionRegistry + IFunctionInvoker + Execute-by-name)
//     · Fast path: ExecuteAsync(ICustomFunction, context) — zero host overhead
//     · Slow path: ExecuteAsync(string, input, options)   — lookup + opt-in observability
//   - IDomainEventBus (in-process pub/sub)
//   - ITrigger        (real — TimerTrigger, DomainEventTrigger<TEvent>)
//   - TriggerHost     (IHostedService that starts/stops all ITrigger instances)
//
// What BMS provides:
//   - Book domain + BookDomainService + IBookRepository + BMS domain events (BMS.Core)
//   - BookApplicationService + DTOs + IEventPublisher (BMS.Interface)
//   - BmsDbContext + BookRepository + EventPublisher (publishes to IDomainEventBus)
//     + AuditFunction + SearchIndexFunction + NotificationFunction + HeartbeatFunction (BMS.External.FaaS)
// -----------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// ---- Controllers + API versioning ----
builder.Services.AddControllers(options =>
{
    options.RespectBrowserAcceptHeader = true;
    options.ReturnHttpNotAcceptable = true;
    options.Filters.Add<CopyrightResponseFilter>();
}).AddXmlSerializerFormatters();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.UrlSegmentApiVersionReader(),
        new Asp.Versioning.QueryStringApiVersionReader("version"),
        new Asp.Versioning.HeaderApiVersionReader("X-Version"));
}).AddApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

// ---- Database (SQLite — no SQL Server required) ----
builder.Services.AddDbContext<BmsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=bms.db"));

// ---- Artichoke architecture: domain + application + infrastructure ----
builder.Services.AddScoped<IBookDomainService, BookDomainService>();
builder.Services.AddScoped<IBookApplicationService, BookApplicationService>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IEventPublisher, EventPublisher>();

// ---- Artichoke-FaaS framework: functions + triggers ----
// This is the v8.0.6 way. One fluent call registers:
//   - IFunctionHost (singleton — picks up ICustomFunction instances from DI)
//   - IDomainEventBus (in-process pub/sub)
//   - TriggerHost (IHostedService — starts all ITrigger instances on app start)
// Then we register the 4 BMS functions and wire triggers:
//   - TimerTrigger → BMS.Heartbeat every 10 seconds (the heartbeat benchmark)
//   - DomainEventTrigger<BookCreatedEvent> → BMS.Audit / BMS.SearchIndex / BMS.Notification
builder.Services.AddArtichokeFaaS(faas => faas
    .RegisterFunction<AuditFunction>()
    .RegisterFunction<SearchIndexFunction>()
    .RegisterFunction<NotificationFunction>()
    .RegisterFunction<HeartbeatFunction>()
    .AddTimerTrigger("BMS.Heartbeat", TimeSpan.FromSeconds(10))
    .AddDomainEventTrigger<BookCreatedEvent>("BMS.Audit", e => new
    {
        eventType = "BookCreatedEvent",
        bookId = e.Book.Id,
        title = e.Book.Title,
        author = e.Book.Author,
        publishedYear = e.Book.PublishedYear,
        occurredAt = e.OccurredOn
    })
    .AddDomainEventTrigger<BookCreatedEvent>("BMS.SearchIndex", e => new
    {
        eventType = "BookCreatedEvent",
        bookId = e.Book.Id,
        title = e.Book.Title,
        author = e.Book.Author
    })
    .AddDomainEventTrigger<BookCreatedEvent>("BMS.Notification", e => new
    {
        eventType = "BookCreatedEvent",
        bookId = e.Book.Id,
        title = e.Book.Title,
        userName = "system"
    })
    .AddDomainEventTrigger<BookUpdatedEvent>("BMS.Audit", e => new
    {
        eventType = "BookUpdatedEvent",
        bookId = e.Book.Id,
        title = e.Book.Title
    })
    .AddDomainEventTrigger<BookUpdatedEvent>("BMS.SearchIndex", e => new
    {
        eventType = "BookUpdatedEvent",
        bookId = e.Book.Id,
        title = e.Book.Title,
        author = e.Book.Author
    })
    .AddDomainEventTrigger<BookDeletedEvent>("BMS.Audit", e => new
    {
        eventType = "BookDeletedEvent",
        bookId = e.BookId,
        title = e.Title
    })
    .AddDomainEventTrigger<BookDeletedEvent>("BMS.SearchIndex", e => new
    {
        eventType = "BookDeletedEvent",
        bookId = e.BookId,
        title = e.Title
    }));

// ---- Identity + JWT ----
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 5;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<BmsDbContext>()
.AddDefaultTokenProviders();

var jwtKey = builder.Configuration["JWT:Key"] ?? "BmsApiDefaultJwtSecretKeyAtLeast32Chars!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization();

// ---- Swagger ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BMS API v8.0.6",
        Version = "v1",
        Description = "Book Management System — Artichoke architecture + Artichoke-FaaS framework (v8.0.6). "
                     + "Functions registered via fluent AddArtichokeFaaS(); triggers fire heartbeats and domain events."
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header,
        Description = "POST /api/v4/auth/login with admin@dwvops1.com / Admin123! to get a token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme } }, Array.Empty<string>() }
    });
});

var app = builder.Build();

// ---- Initialize DB + seed ----
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<BmsDbContext>();
        logger.LogInformation("Initializing BMS database (SQLite)...");
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("BMS database ready.");

        await RoleSeeder.SeedRolesAsync(services);
        await UserSeeder.SeedUsersAsync(services);

        var host = services.GetRequiredService<IFunctionHost>();
        var registered = host.List();
        logger.LogInformation("Registered FaaS functions: {Functions}",
            string.Join(", ", registered) is { } list && list.Length > 0 ? list : "(none)");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize BMS.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/v1/swagger.json", "BMS API v8.0.6");
        c.DefaultModelsExpandDepth(-1);
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("BMS-API v8.0.6 started on http://localhost:5388");
app.Logger.LogInformation("Default admin: admin@dwvops1.com / Admin123!");
app.Logger.LogInformation("Heartbeat trigger fires BMS.Heartbeat every 10s (check logs)");

app.Run();
