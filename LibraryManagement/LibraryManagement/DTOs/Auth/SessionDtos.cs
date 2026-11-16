using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LibraryManagement.DTOs.Auth;

public class SessionInfoDto
{
    public int Id { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string? Browser { get; set; }

    public string? OperatingSystem { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public bool IsCurrent { get; set; }
}

/// <summary>Returned (HTTP 409) instead of a token when two devices are already signed in.</summary>
public class DeviceLimitDto
{
    public string Code { get; set; } = "DEVICE_LIMIT_REACHED";

    public string Message { get; set; } =
        "You have reached the maximum device limit. Select a device to log out.";

    public string ChallengeToken { get; set; } = string.Empty;

    public int ExpiresInSeconds { get; set; }

    public List<SessionInfoDto> Sessions { get; set; } = new();
}

public class CompleteLoginDto
{
    [Required]
    public string ChallengeToken { get; set; } = string.Empty;

    /// <summary>The <see cref="SessionInfoDto.Id"/> of the device to log out.</summary>
    [Required]
    public int? SessionToRevokeId { get; set; }
}
