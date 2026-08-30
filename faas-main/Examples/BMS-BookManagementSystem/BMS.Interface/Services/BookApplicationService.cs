using BMS.Core.Entities;
using BMS.Core.Events;
using BMS.Core.Interfaces;
using BMS.Core.Services;
using BMS.Core.ValueObjects;
using BMS.Interface.DTOs;
using Mapster;

namespace BMS.Interface.Services;

/// <summary>
/// Interface layer - Application service implementation
/// Orchestrates domain operations and handles DTOs
/// </summary>
public class BookApplicationService : IBookApplicationService
{
    private readonly IBookRepository _bookRepository;
    private readonly IBookDomainService _bookDomainService;
    private readonly IEventPublisher _eventPublisher;

    public BookApplicationService(
        IBookRepository bookRepository,
        IBookDomainService bookDomainService,
        IEventPublisher eventPublisher)
    {
        _bookRepository = bookRepository;
        _bookDomainService = bookDomainService;
        _eventPublisher = eventPublisher;
    }

    public async Task<ServiceResult<IEnumerable<BookResponseDto>>> GetAllBooksAsync()
    {
        try
        {
            var books = await _bookRepository.GetAllAsync();
            var response = books.Select(book => book.Adapt<BookResponseDto>()).ToList();
            
            return ServiceResult<IEnumerable<BookResponseDto>>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<BookResponseDto>>.Failure($"Failed to retrieve books: {ex.Message}");
        }
    }

    public async Task<ServiceResult<BookResponseDto?>> GetBookByIdAsync(int id)
    {
        try
        {
            var book = await _bookRepository.GetByIdAsync(id);
            
            if (book == null)
                return ServiceResult<BookResponseDto?>.Success(null);
            
            var response = book.Adapt<BookResponseDto>();
            return ServiceResult<BookResponseDto?>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<BookResponseDto?>.Failure($"Failed to retrieve book: {ex.Message}");
        }
    }

    public async Task<ServiceResult<BookResponseDto>> CreateBookAsync(CreateBookRequestDto request)
    {
        try
        {
            // Domain validation
            var validationResult = await _bookDomainService.ValidateForCreationAsync(
                request.Title, request.Author, request.PublishedYear);
            
            if (!validationResult.IsValid)
                return ServiceResult<BookResponseDto>.Failure(validationResult.ErrorMessage);

            // Create domain entity
            var book = Book.Create(request.Title, request.Author, request.PublishedYear);
            
            // Persist
            await _bookRepository.AddAsync(book);
            
            // Publish domain events
            foreach (var domainEvent in book.DomainEvents)
            {
                await _eventPublisher.PublishAsync(domainEvent);
            }
            book.ClearDomainEvents();
            
            var response = book.Adapt<BookResponseDto>();
            return ServiceResult<BookResponseDto>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<BookResponseDto>.Failure($"Failed to create book: {ex.Message}");
        }
    }

    public async Task<ServiceResult<BookResponseDto>> UpdateBookAsync(UpdateBookRequestDto request)
    {
        try
        {
            var book = await _bookRepository.GetByIdAsync(request.Id);
            if (book == null)
                return ServiceResult<BookResponseDto>.Failure($"Book with ID {request.Id} not found");

            // Domain validation
            var validationResult = await _bookDomainService.ValidateForUpdateAsync(
                request.Id, request.Title, request.Author, request.PublishedYear);
            
            if (!validationResult.IsValid)
                return ServiceResult<BookResponseDto>.Failure(validationResult.ErrorMessage);

            // Update domain entity
            book.UpdateDetails(request.Title, request.Author, request.PublishedYear);
            
            // Persist
            await _bookRepository.UpdateAsync(book);
            
            // Publish domain events
            foreach (var domainEvent in book.DomainEvents)
            {
                await _eventPublisher.PublishAsync(domainEvent);
            }
            book.ClearDomainEvents();
            
            var response = book.Adapt<BookResponseDto>();
            return ServiceResult<BookResponseDto>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<BookResponseDto>.Failure($"Failed to update book: {ex.Message}");
        }
    }

    public async Task<ServiceResult<bool>> DeleteBookAsync(int id)
    {
        try
        {
            var book = await _bookRepository.GetByIdAsync(id);
            if (book == null)
                return ServiceResult<bool>.Failure($"Book with ID {id} not found");

            book.MarkForDeletion();
            await _bookRepository.DeleteAsync(id);
            
            // Publish domain events
            foreach (var domainEvent in book.DomainEvents)
            {
                await _eventPublisher.PublishAsync(domainEvent);
            }
            book.ClearDomainEvents();
            
            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ServiceResult<bool>.Failure($"Failed to delete book: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<BookResponseDto>>> SearchBooksAsync(string keywords)
    {
        try
        {
            var criteria = new SearchCriteria(SearchTerm: keywords);
            var books = await _bookRepository.SearchAsync(criteria);
            var response = books.Select(book => book.Adapt<BookResponseDto>()).ToList();
            
            return ServiceResult<IEnumerable<BookResponseDto>>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<BookResponseDto>>.Failure($"Failed to search books: {ex.Message}");
        }
    }

    public async Task<ServiceResult<IEnumerable<BookResponseDto>>> FilterBooksOptimizedAsync(BookSearchParametersDto searchParams)
    {
        try
        {
            var criteria = new SearchCriteria(
                searchParams.SearchTerm,
                searchParams.Title,
                searchParams.Author,
                searchParams.PublishedYear);
                
            var books = await _bookRepository.SearchAsync(criteria);
            var response = books.Select(book => book.Adapt<BookResponseDto>()).ToList();
            
            return ServiceResult<IEnumerable<BookResponseDto>>.Success(response);
        }
        catch (Exception ex)
        {
            return ServiceResult<IEnumerable<BookResponseDto>>.Failure($"Failed to filter books: {ex.Message}");
        }
    }
}