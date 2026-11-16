using LibraryManagement.DTOs.Dictionary;

namespace LibraryManagement.Services.Interfaces;

public interface IDictionaryService
{
    /// <summary>
    /// Looks a word up (cached). Returns null when the dictionary does not
    /// know it; throws <see cref="DictionaryUnavailableException"/> when the
    /// external service cannot be reached in time.
    /// </summary>
    Task<DictionaryEntryDto?> LookupAsync(string word, CancellationToken cancellationToken);
}

public class DictionaryUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
