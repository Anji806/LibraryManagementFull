namespace LibraryManagement.Services;

public class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    // OAuth 2.0 Web client ID from Google Cloud Console. Empty = feature disabled.
    public string ClientId { get; set; } = string.Empty;
}
