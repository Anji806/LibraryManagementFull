using LibraryManagement.DTOs.Books;
using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Librarian,Member")]
public class BooksController : ControllerBase
{
  
    private readonly IBookRepository _bookRepository;
    private readonly IBorrowTransactionRepository _borrowRepository;

    public BooksController(
        IBookRepository bookRepository,
        IBorrowTransactionRepository borrowRepository)
    {
        _bookRepository = bookRepository;
        _borrowRepository = borrowRepository;
    }
    // GET: api/books
    [HttpGet]
    public async Task<IActionResult> GetBooks()
    {
        var books = await _bookRepository.GetAllAsync();

        var result = books.Select(book => new BookResponseDto
        {
            BookId = book.BookId,
            Title = book.Title,
            ISBN = book.ISBN,
            AuthorId = book.AuthorId,
            AuthorName = book.Author.AuthorName,
            CategoryId = book.CategoryId,
            CategoryName = book.CategoryNames ?? book.Category.CategoryName,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            PublishedYear = book.PublishedYear,
            PreviewIntroduction = book.PreviewIntroduction,
            PreviewTheme = book.PreviewTheme,
            PreviewAbstract = book.PreviewAbstract,
            PreviewMotto = book.PreviewMotto,
            PreviewWhyRead = book.PreviewWhyRead,
            CoverUrl = book.CoverUrl
        });

        return Ok(result);
    }

    // GET: api/books/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBook(int id)
    {
        var book = await _bookRepository.GetByIdAsync(id);

        if (book==null) //
        {
            return NotFound(new
            {
                message = "The Book  You Searching was not found."
            });
        }


        //this is to map the book entity to the BookResponseDto
        var result = new BookResponseDto
        {
            BookId=book.BookId,
            Title=book.Title,
            ISBN=book.ISBN,
            AuthorId=book.AuthorId,
            AuthorName = book.Author.AuthorName,
            CategoryId = book.CategoryId,
            CategoryName = book.CategoryNames ?? book.Category.CategoryName,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            PublishedYear = book.PublishedYear,
            PreviewIntroduction = book.PreviewIntroduction,
            PreviewTheme = book.PreviewTheme,
            PreviewAbstract = book.PreviewAbstract,
            PreviewMotto = book.PreviewMotto,
            PreviewWhyRead = book.PreviewWhyRead,
            CoverUrl = book.CoverUrl
        };

        return Ok(result);
    }
    // GET: api/books/1/borrow-history
    [HttpGet("{bookId:int}/borrow-history")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> GetBorrowHistory(
        int bookId)
    {
        var history =
            await _borrowRepository
                .GetBorrowHistoryByBookAsync(bookId);

        return Ok(history);
    }

    // GET: api/books/search?title=dotnet
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return BadRequest(new
            {
                message = "Title is required."
            });
        }

        var books = await _bookRepository.SearchAsync(title);

        var result = books.Select(book => new BookResponseDto
        {
            BookId = book.BookId,
            Title = book.Title,
            ISBN = book.ISBN,
            AuthorId = book.AuthorId,
            AuthorName = book.Author.AuthorName,
            CategoryId = book.CategoryId,
            CategoryName = book.CategoryNames ?? book.Category.CategoryName,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            PublishedYear = book.PublishedYear,
            PreviewIntroduction = book.PreviewIntroduction,
            PreviewTheme = book.PreviewTheme,
            PreviewAbstract = book.PreviewAbstract,
            PreviewMotto = book.PreviewMotto,
            PreviewWhyRead = book.PreviewWhyRead,
            CoverUrl = book.CoverUrl
        });

        return Ok(result);
    }

    // POST: api/books
    [HttpPost]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> CreateBook(CreateBookDto dto)
    {
        var book = new Book
        {
            Title = dto.Title,
            ISBN = dto.ISBN,
            AuthorId = dto.AuthorId,
            CategoryId = dto.CategoryId,
            TotalCopies = dto.TotalCopies,
            AvailableCopies = dto.TotalCopies,
            PublishedYear = dto.PublishedYear,
            PreviewIntroduction = dto.PreviewIntroduction,
            PreviewTheme = dto.PreviewTheme,
            PreviewAbstract = dto.PreviewAbstract,
            PreviewMotto = dto.PreviewMotto,
            PreviewWhyRead = dto.PreviewWhyRead,
            CoverUrl = dto.CoverUrl
        };

        await _bookRepository.AddAsync(book);
        await _bookRepository.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetBook),
            new { id = book.BookId },
            book
        );
    }

    // PUT: api/books/1
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Librarian")]
    public async Task<IActionResult> UpdateBook(
        int id,
        UpdateBookDto dto)
    {
        var book = await _bookRepository.GetByIdAsync(id);

        if (book == null)
        {
            return NotFound(new
            {
                message = "Book not found."
            });
        }

        int borrowedCopies =
            book.TotalCopies - book.AvailableCopies;

        if (dto.TotalCopies < borrowedCopies)
        {
            return BadRequest(new
            {
                message =
                    $"Total copies cannot be less than currently borrowed copies ({borrowedCopies})."
            });
        }

        book.Title = dto.Title;
        book.AuthorId = dto.AuthorId;
        book.CategoryId = dto.CategoryId;

        book.TotalCopies = dto.TotalCopies;

        book.AvailableCopies =
            dto.TotalCopies - borrowedCopies;

        book.PublishedYear = dto.PublishedYear;
        book.PreviewIntroduction = dto.PreviewIntroduction;
        book.PreviewTheme = dto.PreviewTheme;
        book.PreviewAbstract = dto.PreviewAbstract;
        book.PreviewMotto = dto.PreviewMotto;
        book.PreviewWhyRead = dto.PreviewWhyRead;
        book.CoverUrl = dto.CoverUrl;

        _bookRepository.Update(book);

        await _bookRepository.SaveChangesAsync();

        return Ok(new
        {
            message = "Book updated successfully.",
            book
        });
    }

    // DELETE: api/books/1
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteBook(int id)
    {
        var book = await _bookRepository.GetByIdAsync(id);

        if (book == null)
        {
            return NotFound(new
            {
                message = "Book not found."
            });
        }

        bool hasBorrowHistory =
            await _bookRepository.HasBorrowHistoryAsync(id);

        if (hasBorrowHistory)
        {
            return Conflict(new
            {
                message =
                    "This book cannot be deleted because it has borrowing history."
            });
        }

        try
        {
            _bookRepository.Delete(book);

            await _bookRepository.SaveChangesAsync();
        }
        catch (DbException)
        {
            // A borrowing may have been created after the check above.
            return Conflict(new
            {
                message =
                    "This book cannot be deleted because it is referenced by other records."
            });
        }

        return NoContent();
    }
}
