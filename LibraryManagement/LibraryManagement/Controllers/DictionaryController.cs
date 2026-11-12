using System.Text.RegularExpressions;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Librarian,Member")]
public partial class DictionaryController : ControllerBase
{
    private readonly IDictionaryService _dictionary;

    public DictionaryController(IDictionaryService dictionary)
    {
        _dictionary = dictionary;
    }

    // One word: letters, with inner apostrophes or hyphens ("don't", "well-known").
    [GeneratedRegex(@"^\p{L}[\p{L}'-]{0,39}$")]
    private static partial Regex WordPattern();

    // GET: api/dictionary/serendipity
    [HttpGet("{word}")]
    public async Task<IActionResult> Lookup(string word, CancellationToken cancellationToken)
    {
        word = (word ?? string.Empty).Trim().Replace('’', '\'');

        if (!WordPattern().IsMatch(word))
        {
            return BadRequest(new { message = "Enter a single word." });
        }

        try
        {
            var entry = await _dictionary.LookupAsync(word, cancellationToken);

            return entry is null
                ? NotFound(new { message = $"No definition was found for \"{word}\"." })
                : Ok(entry);
        }
        catch (DictionaryUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "The dictionary is not available right now. Please try again."
            });
        }
    }
}
