using BMS.Interface.DTOs;

namespace BMS.Interface.Services;

/// <summary>
/// Interface layer - Application service contracts
/// Defines use cases and orchestration
/// </summary>
public interface IBookApplicationService
{
    Task<ServiceResult<IEnumerable<BookResponseDto>>> GetAllBooksAsync();
    Task<ServiceResult<BookResponseDto?>> GetBookByIdAsync(int id);
    Task<ServiceResult<BookResponseDto>> CreateBookAsync(CreateBookRequestDto request);
    Task<ServiceResult<BookResponseDto>> UpdateBookAsync(UpdateBookRequestDto request);
    Task<ServiceResult<bool>> DeleteBookAsync(int id);
    Task<ServiceResult<IEnumerable<BookResponseDto>>> SearchBooksAsync(string keywords);
    Task<ServiceResult<IEnumerable<BookResponseDto>>> FilterBooksOptimizedAsync(BookSearchParametersDto searchParams);
}