using BMS.Core.Entities;
using BMS.Core.Interfaces;
using BMS.Core.ValueObjects;
using BMS.External.Data;
using Microsoft.EntityFrameworkCore;

namespace BMS.External.Persistence;

/// <summary>
/// External layer - Database access implementation
/// Protects core from infrastructure concerns
/// </summary>
public class BookRepository : IBookRepository
{
    private readonly BmsDbContext _context;

    public BookRepository(BmsDbContext context)
    {
        _context = context;
    }

    public async Task<Book?> GetByIdAsync(int id)
    {
        var entity = await _context.Books.FindAsync(id);
        return entity?.ToDomainEntity();
    }

    public async Task<IEnumerable<Book>> GetAllAsync()
    {
        var entities = await _context.Books.ToListAsync();
        return entities.Select(e => e.ToDomainEntity());
    }

    public async Task<IEnumerable<Book>> SearchAsync(SearchCriteria criteria)
    {
        var query = _context.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            query = query.Where(b => 
                b.Title.Contains(criteria.SearchTerm) ||
                b.Author.Contains(criteria.SearchTerm) ||
                b.PublishedYear.ToString().Contains(criteria.SearchTerm));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Title))
        {
            query = query.Where(b => b.Title.Contains(criteria.Title));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Author))
        {
            query = query.Where(b => b.Author.Contains(criteria.Author));
        }

        if (criteria.PublishedYear.HasValue)
        {
            query = query.Where(b => b.PublishedYear == criteria.PublishedYear.Value);
        }

        var entities = await query.ToListAsync();
        return entities.Select(e => e.ToDomainEntity());
    }

    public async Task<bool> ExistsAsync(string title, string author, int publishedYear)
    {
        return await _context.Books.AnyAsync(b => 
            b.Title == title && 
            b.Author == author && 
            b.PublishedYear == publishedYear);
    }

    public async Task AddAsync(Book book)
    {
        var entity = book.ToDataEntity();
        await _context.Books.AddAsync(entity);
        await _context.SaveChangesAsync();
        
        // Set the generated ID back to the domain entity
        book.SetId(entity.Id);
    }

    public async Task UpdateAsync(Book book)
    {
        var entity = book.ToDataEntity();
        _context.Books.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _context.Books.FindAsync(id);
        if (entity != null)
        {
            _context.Books.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}