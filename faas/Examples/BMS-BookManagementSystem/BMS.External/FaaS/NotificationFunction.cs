using System.Text.Json;
using Artichoke.FaaS.Core.Base;
using Artichoke.FaaS.Core.Interfaces;

namespace BMS.External.FaaS;

/// <summary>
/// FaaS Notification Function - Sends notifications on domain events
/// Triggered by DomainEventTrigger for all book operations
/// In production, connects to email/SMS/push notification services
/// Runs as isolated FaaS function - no dependency on the main API process
/// </summary>
public class NotificationFunction : CustomFunctionBase
{
    public override string FunctionType => "BMS.Notification";

    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        await OnInitializeAsync();

        var eventType = GetParameter<string>(context, "eventType") ?? "Unknown";
        var bookId = GetParameter<int>(context, "bookId");
        var title = GetParameter<string>(context, "title") ?? "";
        var userName = GetParameter<string>(context, "userName") ?? "system";

        var message = eventType switch
        {
            "BookCreatedEvent" => $"New book added: \"{title}\" (ID: {bookId}) by {userName}",
            "BookUpdatedEvent" => $"Book updated: \"{title}\" (ID: {bookId}) by {userName}",
            "BookDeletedEvent" => $"Book deleted: \"{title}\" (ID: {bookId}) by {userName}",
            _ => $"Book event {eventType} for ID: {bookId}"
        };

        Console.WriteLine("[NOTIFICATION] {0}", message);

        return FunctionExecutionResult.Success(
            new
            {
                Channel = "Log",
                Recipient = "admin@bms.local",
                Message = message,
                SentAt = DateTime.UtcNow,
                Status = "Delivered"
            },
            $"Notification sent for {eventType}: '{title}'",
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
                eventType = new { type = "string" },
                bookId = new { type = "integer" },
                title = new { type = "string" },
                userName = new { type = "string" }
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
                channel = new { type = "string" },
                recipient = new { type = "string" },
                message = new { type = "string" },
                sentAt = new { type = "string", format = "date-time" },
                status = new { type = "string" }
            }
        });
    }
}
