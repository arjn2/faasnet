namespace BMS.Core.ValueObjects;

/// <summary>
/// Core layer - Value objects
/// </summary>
public record SearchCriteria(
    string? SearchTerm = null,
    string? Title = null,
    string? Author = null,
    int? PublishedYear = null
);

public class ValidationResult
{
    public bool IsValid { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    private ValidationResult(bool isValid, string errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Valid()
    {
        return new ValidationResult(true, string.Empty);
    }

    public static ValidationResult Invalid(string errorMessage)
    {
        return new ValidationResult(false, errorMessage);
    }
}