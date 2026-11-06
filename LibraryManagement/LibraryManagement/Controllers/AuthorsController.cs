using LibraryManagement.Models;
using LibraryManagement.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthorsController : ControllerBase
{
    private readonly IAuthorRepository _authorRepository;

    public AuthorsController(IAuthorRepository authorRepository)
    {
        _authorRepository = authorRepository;
    }

    // GET: api/authors
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAuthors()
    {
        var authors = await _authorRepository.GetAllAsync();
        Console.WriteLine(authors);

        return Ok(authors);
    }

    // GET: api/authors/1
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAuthor(int id)
    {
        var author = await _authorRepository.GetByIdAsync(id);

        if (author == null)
        {
            return NotFound(new
            {
                message = "Author not found."
            });
        }

        return Ok(author);
    }

    // POST: api/authors
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAuthor(Author author)
    {
        await _authorRepository.AddAsync(author);

        await _authorRepository.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetAuthor),
            new { id = author.AuthorId },
            author
        );
    }

    // PUT: api/authors/1
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAuthor(
        int id,
        Author author)
    {
        var existingAuthor =
            await _authorRepository.GetByIdAsync(id);

        if (existingAuthor == null)
        {
            return NotFound(new
            {
                message = "Author not found."
            });
        }

        existingAuthor.AuthorName = author.AuthorName;

        _authorRepository.Update(existingAuthor);

        await _authorRepository.SaveChangesAsync();

        return Ok(existingAuthor);
    }

    // DELETE: api/authors/1
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAuthor(int id)
    {
        var author = await _authorRepository.GetByIdAsync(id);

        if (author == null)
        {
            return NotFound(new
            {
                message = "Author not found."
            });
        }

        _authorRepository.Delete(author);

        await _authorRepository.SaveChangesAsync();

        return NoContent();
    }
}