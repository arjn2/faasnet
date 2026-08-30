using System.Text.Json;
using Artichoke.FaaS.Core.Base;
using Artichoke.FaaS.Core.Interfaces;
using BMS.External.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BMS.External.FaaS;

/// <summary>
/// FaaS Audit Function - Logs domain events to the audit trail
/// Triggered by DomainEventTrigger when books are created/updated/deleted
/// Replaces the old fake AuditLoggerFunction that used Random.Shared.Next()
/// </summary>
public class AuditFunction : CustomFunctionBase
{
    public override string FunctionType => "BMS.Audit";

    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        await OnInitializeAsync();

        var eventType = GetParameter<string>(context, "eventType") ?? "Unknown";
        var bookId = GetParameter<int>(context, "bookId");
        var title = GetParameter<string>(context, "title") ?? "";
        var userName = GetParameter<string>(context, "userName") ?? "system";

        Console.WriteLine("[AUDIT] [{0}] User={1} BookId={2} Title=\"{3}\" at {4}",
            eventType, userName, bookId, title, DateTime.UtcNow);

        return FunctionExecutionResult.Success(
            new
            {
                Action = "AuditLogged",
                EventType = eventType,
                BookId = bookId,
                Title = title,
                AuditedBy = userName,
                AuditedAt = DateTime.UtcNow
            },
            $"Audit log recorded for {eventType}: '{title}' (ID: {bookId})",
            TimeSpan.Zero);
    }

    public override Task<ValidationResult> ValidateInputAsync(object input)
    {
        if (input == null) return Task.FromResult(ValidationResult.Failure("Input is required"));
        return Task.FromResult(ValidationResult.Success());
    }

    public override JsonDocument GetInputSchema()
    {
        return CreateSchema(new
        {
            type = "object",
            properties = new
            {
                eventType = new { type = "string", description = "Domain event type" },
                bookId = new { type = "integer", description = "Book ID" },
                title = new { type = "string", description = "Book title" },
                author = new { type = "string", description = "Book author" },
                userName = new { type = "string", description = "User who triggered the event" }
            },
            required = new[] { "eventType", "bookId" }
        });
    }

    public override JsonDocument GetOutputSchema()
    {
        return CreateSchema(new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string" },
                eventType = new { type = "string" },
                bookId = new { type = "integer" },
                title = new { type = "string" },
                auditedBy = new { type = "string" },
                auditedAt = new { type = "string", format = "date-time" }
            }
        });
    }
}
