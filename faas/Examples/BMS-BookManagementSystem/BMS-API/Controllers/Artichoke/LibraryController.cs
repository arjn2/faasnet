using BMS.Interface.Services;
using BMS.Interface.DTOs;
using BMS.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BMS.Core.Attributes;

namespace BMS_API.Controllers.Artichoke;

/// <summary>
/// External layer - Web API endpoints using Artichoke Architecture
/// Protects inner layers from HTTP concerns
/// </summary>
[ApiController]
[Route("api/artichoke/[controller]")]
[Copyright("ARJUN A L",2025)]
public class LibraryController : ControllerBase
{
    private readonly IBookApplicationService _bookService;

    public LibraryController(IBookApplicationService bookService)
    {
        _bookService = bookService;
    }

    /// <summary>
    /// Get all books
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<BookResponseDto>>> GetAllBooks()
    {
        try
        {
            var result = await _bookService.GetAllBooksAsync();
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return BadRequest(result.ErrorMessage);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get book by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<BookResponseDto>> GetBook(int id)
    {
        try
        {
            var result = await _bookService.GetBookByIdAsync(id);
            
            if (result.IsSuccess && result.Data != null)
                return Ok(result.Data);
                
            if (!result.IsSuccess)
                return BadRequest(result.ErrorMessage);
                
            return NotFound($"Book with ID {id} not found");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a new book (Admin only)
    /// </summary>
    
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BookResponseDto>> CreateBook([FromBody] CreateBookRequestDto request)
    {
        try
        {
            if (request == null)
                return BadRequest("Book data is required");

            var result = await _bookService.CreateBookAsync(request);
            
            if (result.IsSuccess && result.Data != null)
                return CreatedAtAction(nameof(GetBook), new { id = result.Data.Id }, result.Data);
                
            return BadRequest(result.ErrorMessage);
        }
        catch (DomainException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Update an existing book (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BookResponseDto>> UpdateBook(int id, [FromBody] UpdateBookRequestDto request)
    {
        try
        {
            if (request == null)
                return BadRequest("Book data is required");

            if (id != request.Id)
                return BadRequest("ID mismatch");

            var result = await _bookService.UpdateBookAsync(request);
            
            if (result.IsSuccess && result.Data != null)
                return Ok(result.Data);
                
            return BadRequest(result.ErrorMessage);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (DomainException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a book by ID (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteBook(int id)
    {
        try
        {
            var result = await _bookService.DeleteBookAsync(id);
            
            if (result.IsSuccess)
                return NoContent();
                
            return BadRequest(result.ErrorMessage);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Search books by keywords
    /// </summary>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<BookResponseDto>>> SearchBooks([FromQuery] string keywords)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keywords))
                return BadRequest("Search keywords are required");

            var result = await _bookService.SearchBooksAsync(keywords);
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return BadRequest(result.ErrorMessage);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Advanced book filtering with multiple parameters
    /// </summary>
    [HttpGet("filter")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<BookResponseDto>>> FilterBooks([FromQuery] BookSearchParametersDto searchParams)
    {
        try
        {
            var result = await _bookService.FilterBooksOptimizedAsync(searchParams);
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return BadRequest(result.ErrorMessage);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}