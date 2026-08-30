using BMS.Core.Entities;
using BMS.Core.ValueObjects;

namespace BMS.Core.Interfaces;

/// <summary>
/// Core layer - Repository contract (no dependencies)
/// </summary>
public interface IBookRepository
{
    Task<Book?> GetByIdAsync(int id);
    Task<IEnumerable<Book>> GetAllAsync();
    Task<IEnumerable<Book>> SearchAsync(SearchCriteria criteria);
    Task<bool> ExistsAsync(string title, string author, int publishedYear);
    Task AddAsync(Book book);
    Task UpdateAsync(Book book);
    Task DeleteAsync(int id);
}