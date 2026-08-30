using BMS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BMS.External.Data;

/// <summary>
/// External layer - Database context with Identity support
/// Unified context for both Artichoke entities and ASP.NET Core Identity
/// </summary>
public class BmsDbContext : IdentityDbContext<IdentityUser>
{
    public BmsDbContext(DbContextOptions<BmsDbContext> options) : base(options)
    {
    }

    public DbSet<BookEntity> Books { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Call base first to configure Identity tables
        base.OnModelCreating(modelBuilder);
        
        // Configure BookEntity
        modelBuilder.Entity<BookEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Author).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PublishedYear).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired(false);

            // Seed data with static DateTime values
            entity.HasData(
                new BookEntity { Id = 1, Title = "Sherlock Holmes", Author = "Doyle", PublishedYear = 1979, CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc) },
                new BookEntity { Id = 2, Title = "Tom Holland", Author = "Tom", PublishedYear = 2001, CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc) },
                new BookEntity { Id = 3, Title = "Tarzan", Author = "Rich Burroughs", PublishedYear = 2000, CreatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc) }
            );
        });
    }
}

/// <summary>
/// External layer - Data entity for EF Core
/// </summary>
public class BookEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int PublishedYear { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Mapping method to domain entity using factory method and proper reconstruction
    public Book ToDomainEntity()
    {
        // Use the static factory method to create a proper domain entity
        var book = Book.CreateFromRepository(
            Id, 
            Title, 
            Author, 
            PublishedYear, 
            CreatedAt, 
            UpdatedAt);
        
        // Clear events for existing entities loaded from database
        book.ClearDomainEvents();
        return book;
    }
}

/// <summary>
/// Extension methods for mapping
/// </summary>
public static class BookMappingExtensions
{
    public static BookEntity ToDataEntity(this Book book)
    {
        return new BookEntity
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            PublishedYear = book.PublishedYear,
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt
        };
    }
}