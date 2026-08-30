using BMS.Core.Interfaces;
using BMS.Core.ValueObjects;

namespace BMS.Core.Services;

/// <summary>
/// Core layer - Domain service implementation
/// Contains complex business logic that doesn't belong to a single entity
/// </summary>
public class BookDomainService : IBookDomainService
{
    private readonly IBookRepository _bookRepository;

    public BookDomainService(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    public async Task<ValidationResult> ValidateForCreationAsync(string title, string author, int publishedYear)
    {
        // Business rule: No duplicate books
        if (await IsDuplicateAsync(title, author, publishedYear))
        {
            return ValidationResult.Invalid($"A book with title '{title}' by '{author}' published in {publishedYear} already exists");
        }

        return ValidationResult.Valid();
    }

    public async Task<ValidationResult> ValidateForUpdateAsync(int id, string title, string author, int publishedYear)
    {
        var existingBook = await _bookRepository.GetByIdAsync(id);
        if (existingBook == null)
        {
            return ValidationResult.Invalid($"Book with ID {id} not found");
        }

        // Check if another book (not the current one) has the same details
        var isDuplicate = await _bookRepository.ExistsAsync(title, author, publishedYear);
        if (isDuplicate && (existingBook.Title != title || existingBook.Author != author || existingBook.PublishedYear != publishedYear))
        {
            return ValidationResult.Invalid($"Another book with title '{title}' by '{author}' published in {publishedYear} already exists");
        }

        return ValidationResult.Valid();
    }

    public async Task<bool> IsDuplicateAsync(string title, string author, int publishedYear)
    {
        return await _bookRepository.ExistsAsync(title, author, publishedYear);
    }
}