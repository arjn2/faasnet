using System.Text.Json;
using Artichoke.FaaS.Core.Base;
using Artichoke.FaaS.Core.Interfaces;

namespace BMS.External.FaaS;

/// <summary>
/// FaaS Search Index Function - Updates search index when books change
/// Triggered by DomainEventTrigger for BookCreated and BookUpdated events
/// Replaces passive search with proactive index maintenance
/// </summary>
public class SearchIndexFunction : CustomFunctionBase
{
    private static readonly List<SearchEntry> _searchIndex = new();
    private static readonly object _indexLock = new();

    public override string FunctionType => "BMS.SearchIndex";

    public override async Task<FunctionExecutionResult> ExecuteAsync(FunctionExecutionContext context)
    {
        await OnInitializeAsync();

        var eventType = GetParameter<string>(context, "eventType") ?? "";
        var bookId = GetParameter<int>(context, "bookId");
        var title = GetParameter<string>(context, "title") ?? "";
        var author = GetParameter<string>(context, "author") ?? "";

        lock (_indexLock)
        {
            switch (eventType)
            {
                case "BookCreatedEvent":
                    _searchIndex.Add(new SearchEntry(bookId, title, author, DateTime.UtcNow));
                    Console.WriteLine("[SEARCH-INDEX] ADD: Book {0} '{1}' by {2}", bookId, title, author);
                    break;

                case "BookUpdatedEvent":
                    var existing = _searchIndex.FirstOrDefault(e => e.BookId == bookId);
                    if (existing != null)
                    {
                        existing.Title = title;
                        existing.Author = author;
                        existing.UpdatedAt = DateTime.UtcNow;
                        Console.WriteLine("[SEARCH-INDEX] UPDATE: Book {0} '{1}'", bookId, title);
                    }
                    else
                    {
                        _searchIndex.Add(new SearchEntry(bookId, title, author, DateTime.UtcNow));
                    }
                    break;

                case "BookDeletedEvent":
                    _searchIndex.RemoveAll(e => e.BookId == bookId);
                    Console.WriteLine("[SEARCH-INDEX] REMOVE: Book {0}", bookId);
                    break;
            }
        }

        var indexStats = new
        {
            Action = eventType.Replace("Event", ""),
            BookId = bookId,
            Title = title,
            TotalIndexedEntries = _searchIndex.Count,
            IndexUpdatedAt = DateTime.UtcNow
        };

        return FunctionExecutionResult.Success(
            indexStats,
            $"Search index updated: {eventType} for book '{title}' (ID: {bookId}). Total entries: {_searchIndex.Count}",
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
                author = new { type = "string" }
            },
            required = new[] { "eventType", "bookId", "title" }
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
                bookId = new { type = "integer" },
                totalIndexedEntries = new { type = "integer" },
                indexUpdatedAt = new { type = "string", format = "date-time" }
            }
        });
    }

    /// <summary>
    /// Query the search index (called from search API)
    /// </summary>
    public static IReadOnlyList<SearchEntry> QueryIndex(string? keyword = null)
    {
        lock (_indexLock)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return _searchIndex.AsReadOnly();

            var lower = keyword.ToLower();
            return _searchIndex
                .Where(e => e.Title.ToLower().Contains(lower) || e.Author.ToLower().Contains(lower))
                .ToList()
                .AsReadOnly();
        }
    }

    public record SearchEntry(int BookId, string Title, string Author, DateTime UpdatedAt)
    {
        public string Title { get; set; } = Title;
        public string Author { get; set; } = Author;
        public DateTime UpdatedAt { get; set; } = UpdatedAt;
    }
}
