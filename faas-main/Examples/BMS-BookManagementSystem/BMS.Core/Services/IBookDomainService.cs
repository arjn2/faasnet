using BMS.Core.ValueObjects;

namespace BMS.Core.Services;

/// <summary>
/// Core layer - Domain service for complex business rules
/// </summary>
public interface IBookDomainService
{
    Task<ValidationResult> ValidateForCreationAsync(string title, string author, int publishedYear);
    Task<ValidationResult> ValidateForUpdateAsync(int id, string title, string author, int publishedYear);
    Task<bool> IsDuplicateAsync(string title, string author, int publishedYear);
}