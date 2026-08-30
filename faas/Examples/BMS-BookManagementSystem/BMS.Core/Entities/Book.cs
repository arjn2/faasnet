using BMS.Core.Events;
using BMS.Core.Exceptions;

namespace BMS.Core.Entities;

/// <summary>
/// Core layer - Domain entity with business logic
/// This is the protected heart of the artichoke
/// </summary>
public class Book : IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;
    public int PublishedYear { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // Private constructor for EF Core and serialization
    private Book() { }

    // Factory method - Domain logic for creation
    public static Book Create(string title, string author, int publishedYear)
    {
        // Business rule validation in domain
        ValidateTitle(title);
        ValidateAuthor(author);
        ValidatePublishedYear(publishedYear);

        var book = new Book
        {
            Title = title,
            Author = author,
            PublishedYear = publishedYear,
            CreatedAt = DateTime.UtcNow
        };

        // Domain event
        book.AddDomainEvent(new BookCreatedEvent(book));
        
        return book;
    }

    // Factory method for repository reconstruction (no validation needed, already validated)
    public static Book CreateFromRepository(int id, string title, string author, int publishedYear, DateTime createdAt, DateTime? updatedAt)
    {
        return new Book
        {
            Id = id,
            Title = title,
            Author = author,
            PublishedYear = publishedYear,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    // Method to set ID after persistence (called by repository)
    public void SetId(int id)
    {
        if (Id == 0) // Only allow setting ID once
        {
            Id = id;
        }
    }

    // Business operation
    public void UpdateDetails(string title, string author, int publishedYear)
    {
        ValidateTitle(title);
        ValidateAuthor(author);
        ValidatePublishedYear(publishedYear);

        var hasChanges = Title != title || Author != author || PublishedYear != publishedYear;
        
        Title = title;
        Author = author;
        PublishedYear = publishedYear;
        UpdatedAt = DateTime.UtcNow;

        if (hasChanges)
        {
            AddDomainEvent(new BookUpdatedEvent(this));
        }
    }

    public void MarkForDeletion()
    {
        AddDomainEvent(new BookDeletedEvent(Id, Title));
    }

    // Business rules - Core domain logic (Protected by artichoke layers)
    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Title cannot be empty");
        
        if (title.Length < 3 || title.Length > 200)
            throw new DomainException("Title must be between 3 and 200 characters");
    }

    private static void ValidateAuthor(string author)
    {
        if (string.IsNullOrWhiteSpace(author))
            throw new DomainException("Author cannot be empty");
        
        if (author.Any(char.IsDigit))
            throw new DomainException("Author name cannot contain numbers");
        
        if (author.Length < 3 || author.Length > 100)
            throw new DomainException("Author name must be between 3 and 100 characters");
    }

    private static void ValidatePublishedYear(int year)
    {
        int currentYear = DateTime.Now.Year;
        
        if (year < 1000)
            throw new DomainException("Published year cannot be before 1000");
        
        if (year > currentYear + 1)
            throw new DomainException($"Published year cannot be more than 1 year in the future");
    }

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}