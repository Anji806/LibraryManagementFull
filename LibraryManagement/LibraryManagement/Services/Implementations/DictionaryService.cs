using System.Text.Json;
using LibraryManagement.DTOs.Dictionary;
using LibraryManagement.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace LibraryManagement.Services.Implementations;

/// <summary>
/// Free Dictionary API (https://dictionaryapi.dev): free, no key. It is
/// community run with no uptime guarantee and asks for fair use, so every
/// result is cached here. Entries are derived from Wiktionary (CC BY-SA 3.0),
/// which is why the UI credits the source.
/// </summary>
public class DictionaryService : IDictionaryService
{
    private static readonly TimeSpan FoundLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan NotFoundLifetime = TimeSpan.FromHours(1);

    private const int MaxMeanings = 3;
    private const int MaxDefinitionsPerMeaning = 2;

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DictionaryService> _logger;

    public DictionaryService(
        HttpClient http,
        IMemoryCache cache,
        ILogger<DictionaryService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DictionaryEntryDto?> LookupAsync(
        string word,
        CancellationToken cancellationToken)
    {
        var key = "dictionary:" + word.ToLowerInvariant();

        if (_cache.TryGetValue(key, out DictionaryEntryDto? cached))
        {
            return cached;
        }

        var entry = await FetchAsync(word.ToLowerInvariant(), cancellationToken);

        // Failures are not cached (they throw above); "not found" is, briefly.
        _cache.Set(key, entry, entry is null ? NotFoundLifetime : FoundLifetime);

        return entry;
    }

    private async Task<DictionaryEntryDto?> FetchAsync(
        string word,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(
                Uri.EscapeDataString(word), cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

            return Map(word, document.RootElement);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // OperationCanceledException without our token = HttpClient timeout.
            _logger.LogWarning(ex, "Dictionary lookup failed.");
            throw new DictionaryUnavailableException(
                "The dictionary service is not available right now.", ex);
        }
    }

    private static DictionaryEntryDto? Map(string requestedWord, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            return null;
        }

        var result = new DictionaryEntryDto { Word = requestedWord };
        var meanings = new Dictionary<string, DictionaryMeaningDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in root.EnumerateArray())
        {
            result.Phonetic ??= Text(entry, "phonetic");

            if (entry.TryGetProperty("phonetics", out var phonetics) &&
                phonetics.ValueKind == JsonValueKind.Array)
            {
                foreach (var phonetic in phonetics.EnumerateArray())
                {
                    result.Phonetic ??= Text(phonetic, "text");
                    result.AudioUrl ??= SafeAudio(Text(phonetic, "audio"));
                }
            }

            if (!entry.TryGetProperty("meanings", out var entryMeanings) ||
                entryMeanings.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var meaning in entryMeanings.EnumerateArray())
            {
                var partOfSpeech = Text(meaning, "partOfSpeech") ?? "";

                if (!meanings.TryGetValue(partOfSpeech, out var target))
                {
                    if (meanings.Count >= MaxMeanings) continue;

                    target = new DictionaryMeaningDto { PartOfSpeech = partOfSpeech };
                    meanings[partOfSpeech] = target;
                    result.Meanings.Add(target);
                }

                if (!meaning.TryGetProperty("definitions", out var definitions) ||
                    definitions.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var definition in definitions.EnumerateArray())
                {
                    if (target.Definitions.Count >= MaxDefinitionsPerMeaning) break;

                    if (Text(definition, "definition") is { } meaningText)
                    {
                        target.Definitions.Add(new DictionaryDefinitionDto
                        {
                            Definition = meaningText,
                            Example = Text(definition, "example")
                        });
                    }
                }
            }
        }

        result.Meanings.RemoveAll(m => m.Definitions.Count == 0);

        return result.Meanings.Count == 0 ? null : result;
    }

    private static string? Text(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    // The URL is played by the browser, so only accept plain https links.
    private static string? SafeAudio(string? url)
    {
        if (url is null) return null;

        if (url.StartsWith("//", StringComparison.Ordinal)) url = "https:" + url;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.AbsoluteUri
            : null;
    }
}
