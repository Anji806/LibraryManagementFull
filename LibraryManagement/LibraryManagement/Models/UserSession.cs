namespace LibraryManagement.Models;

/// <summary>
/// One signed-in browser/device. A JWT is only accepted while the session
/// named by its jti claim is neither revoked nor expired.
/// </summary>
public class UserSession
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Value of the JWT jti claim.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Browser-profile identifier sent by the client. Not proof of identity.</summary>
    public string? DeviceId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string? Browser { get; set; }

    public string? OperatingSystem { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }
}

/// <summary>
/// Short-lived, single-use proof that a user already passed the password
/// check while at the device limit. Only a SHA-256 hash of the token is stored.
/// </summary>
public class PendingLogin
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public string? DeviceId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string? Browser { get; set; }

    public string? OperatingSystem { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }
}
