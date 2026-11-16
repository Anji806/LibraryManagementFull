namespace LibraryManagement.Services.Interfaces;

public interface IPasswordResetService
{
    /// <summary>
    /// Starts a reset for the address, if it belongs to an active account:
    /// retires older links, stores a new hashed token and emails the link.
    /// Always completes the same way whether or not the account exists.
    /// </summary>
    Task RequestResetAsync(string email);

    /// <summary>
    /// Sets a new password when the token is valid for the address.
    /// Throws <see cref="ArgumentException"/> (HTTP 400) for an invalid,
    /// expired or already-used token.
    /// </summary>
    Task ResetPasswordAsync(string email, string token, string newPassword);
}
