namespace LibraryManagement.Models;

public class LoginHistory
{
    public long LoginLogId { get; set; }
    public int? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime LoginDateTime { get; set; }
    public DateTime? LogoutDateTime { get; set; }
    public DateTime? SessionExpiresAt { get; set; }
    public string? SessionId { get; set; }
    public string? IPAddress { get; set; }
    public string? Browser { get; set; }
    public string? OperatingSystem { get; set; }
    public string LoginStatus { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string SessionStatus { get; set; } = "Unknown";
}
