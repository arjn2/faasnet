namespace BMS.Interface.DTOs;

/// <summary>
/// Interface layer - Data Transfer Objects
/// </summary>
public record BookResponseDto(
    int Id,
    string Title,
    string Author,
    int PublishedYear,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateBookRequestDto(
    string Title,
    string Author,
    int PublishedYear
);

public record UpdateBookRequestDto(
    int Id,
    string Title,
    string Author,
    int PublishedYear
);

public record BookSearchParametersDto(
    string? SearchTerm = null,
    string? Title = null,
    string? Author = null,
    int? PublishedYear = null
);

public record LoginRequestDto(
    string Username,
    string Password
);

public record LoginResponseDto(
    string Token,
    string Username
);

/// <summary>
/// Interface layer - Result wrapper for service operations
/// </summary>
public class ServiceResult<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }

    private ServiceResult(bool isSuccess, T? data, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
    }

    public static ServiceResult<T> Success(T data)
    {
        return new ServiceResult<T>(true, data, null);
    }

    public static ServiceResult<T> Failure(string errorMessage)
    {
        return new ServiceResult<T>(false, default, errorMessage);
    }
}