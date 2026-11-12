using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LibraryManagement.DTOs.Dictionary;
using LibraryManagement.Services.Implementations;
using LibraryManagement.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace LibraryManagement.Tests;

public class DictionaryServiceTests
{
    private const string HelloJson = """
        [
          {
            "word": "hello",
            "phonetic": "/həˈləʊ/",
            "phonetics": [
              { "text": "/həˈləʊ/", "audio": "" },
              { "text": "/hɛˈləʊ/", "audio": "https://example.test/hello.mp3" }
            ],
            "meanings": [
              {
                "partOfSpeech": "interjection",
                "definitions": [
                  { "definition": "A greeting.", "example": "Hello, everyone." },
                  { "definition": "Used to attract attention." },
                  { "definition": "Third definition is dropped." }
                ]
              },
              {
                "partOfSpeech": "noun",
                "definitions": [ { "definition": "An utterance of hello." } ]
              }
            ]
          },
          {
            "word": "hello",
            "meanings": [
              {
                "partOfSpeech": "noun",
                "definitions": [ { "definition": "Another noun sense." } ]
              }
            ]
          }
        ]
        """;

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static DictionaryService Create(HttpMessageHandler handler)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://dictionary.test/en/") },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<DictionaryService>.Instance);

    [Fact]
    public async Task Lookup_MapsDefinitionsPhoneticsAndAudio()
    {
        var entry = await Create(new StubHandler(_ => Json(HelloJson)))
            .LookupAsync("Hello", CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal("/həˈləʊ/", entry.Phonetic);
        Assert.Equal("https://example.test/hello.mp3", entry.AudioUrl);
        Assert.Equal(["interjection", "noun"], entry.Meanings.Select(m => m.PartOfSpeech));

        var interjection = entry.Meanings[0];
        Assert.Equal(2, interjection.Definitions.Count); // capped
        Assert.Equal("Hello, everyone.", interjection.Definitions[0].Example);
        Assert.Null(interjection.Definitions[1].Example);

        // Same part of speech from the second entry is merged, within the cap.
        Assert.Equal(2, entry.Meanings[1].Definitions.Count);
    }

    [Fact]
    public async Task Lookup_RejectsNonHttpsAudio()
    {
        var json = """
            [{ "word": "x", "phonetics": [{ "audio": "javascript:alert(1)" }, { "audio": "http://insecure.test/a.mp3" }],
               "meanings": [{ "partOfSpeech": "noun", "definitions": [{ "definition": "d" }] }] }]
            """;

        var entry = await Create(new StubHandler(_ => Json(json)))
            .LookupAsync("x", CancellationToken.None);

        Assert.Null(entry!.AudioUrl);
    }

    [Fact]
    public async Task Lookup_UnknownWord_ReturnsNullAndIsCached()
    {
        var handler = new StubHandler(_ => Json("{\"title\":\"No Definitions Found\"}", HttpStatusCode.NotFound));
        var service = Create(handler);

        Assert.Null(await service.LookupAsync("zzzqx", CancellationToken.None));
        Assert.Null(await service.LookupAsync("ZZZQX", CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Lookup_FoundWord_IsCachedCaseInsensitively()
    {
        var handler = new StubHandler(_ => Json(HelloJson));
        var service = Create(handler);

        await service.LookupAsync("hello", CancellationToken.None);
        await service.LookupAsync("HELLO", CancellationToken.None);

        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task Lookup_UpstreamError_IsReportedAsUnavailableAndNotCached(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => Json("{}", status));
        var service = Create(handler);

        await Assert.ThrowsAsync<DictionaryUnavailableException>(
            () => service.LookupAsync("hello", CancellationToken.None));
        await Assert.ThrowsAsync<DictionaryUnavailableException>(
            () => service.LookupAsync("hello", CancellationToken.None));

        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Lookup_Timeout_IsReportedAsUnavailable()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timeout"));

        await Assert.ThrowsAsync<DictionaryUnavailableException>(
            () => Create(handler).LookupAsync("hello", CancellationToken.None));
    }

    [Fact]
    public async Task Lookup_GarbageResponse_IsReportedAsUnavailable()
    {
        await Assert.ThrowsAsync<DictionaryUnavailableException>(
            () => Create(new StubHandler(_ => Json("<html>oops</html>")))
                .LookupAsync("hello", CancellationToken.None));
    }
}

public class DictionaryApiTests : IClassFixture<LibraryApiFactory>
{
    private sealed class FakeDictionary : IDictionaryService
    {
        public Task<DictionaryEntryDto?> LookupAsync(string word, CancellationToken cancellationToken)
            => word.ToLowerInvariant() switch
            {
                "hello" => Task.FromResult<DictionaryEntryDto?>(new DictionaryEntryDto
                {
                    Word = "hello",
                    Phonetic = "/həˈləʊ/",
                    Meanings = [new() { PartOfSpeech = "noun", Definitions = [new() { Definition = "A greeting." }] }]
                }),
                "down" => throw new DictionaryUnavailableException("down"),
                _ => Task.FromResult<DictionaryEntryDto?>(null)
            };
    }

    private readonly HttpClient _client;

    public DictionaryApiTests(LibraryApiFactory factory)
    {
        _client = factory
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.RemoveAll<IDictionaryService>();
                services.AddSingleton<IDictionaryService, FakeDictionary>();
            }))
            .CreateClient();
    }

    private async Task SignInAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Reader",
            email = $"reader-{Guid.NewGuid():N}@test.local",
            password = "Test@12345",
            deviceId = "dictionary-test"
        });
        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<Auth>())!.Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record Auth(string Token);

    [Fact]
    public async Task Lookup_RequiresSignIn()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/dictionary/hello")).StatusCode);
    }

    [Fact]
    public async Task Lookup_KnownWord_ReturnsEntry()
    {
        await SignInAsync();

        var response = await _client.GetAsync("/api/dictionary/hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = await response.Content.ReadFromJsonAsync<DictionaryEntryDto>();
        Assert.Equal("hello", entry!.Word);
        Assert.Equal("A greeting.", entry.Meanings[0].Definitions[0].Definition);
    }

    [Fact]
    public async Task Lookup_UnknownWord_ReturnsFriendlyNotFound()
    {
        await SignInAsync();

        var response = await _client.GetAsync("/api/dictionary/zzzqx");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("No definition", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("two%20words")]
    [InlineData("abc123")]
    [InlineData("-leading")]
    [InlineData("a%2Fb")]
    [InlineData("averyveryveryveryveryveryveryveryveryverylongword")]
    public async Task Lookup_InvalidWord_ReturnsBadRequest(string word)
    {
        await SignInAsync();

        var response = await _client.GetAsync($"/api/dictionary/{word}");

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Unexpected {response.StatusCode}");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Lookup_WhenDictionaryIsDown_Returns503()
    {
        await SignInAsync();

        var response = await _client.GetAsync("/api/dictionary/down");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
