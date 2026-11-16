using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Net.Sockets;
using LibraryManagement.Data;
using LibraryManagement.Services;
using LibraryManagement.Services.Implementations;
using LibraryManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Tests;

/// <summary>EmailService behaviour that needs no real mail server.</summary>
public class EmailServiceTests
{
    private const string Secret = "abcd efgh ijkl mnop";

    private static EmailService Create(Action<EmailSettings>? configure = null)
    {
        var settings = new EmailSettings
        {
            SenderEmail = "library@example.test",
            Password = Secret,
            // A closed local port: connecting fails immediately, no internet involved.
            SmtpServer = "127.0.0.1",
            Port = 1,
            EnableSsl = false,
            TimeoutSeconds = 3
        };
        configure?.Invoke(settings);
        return new EmailService(Options.Create(settings), NullLogger<EmailService>.Instance);
    }

    [Fact]
    public void Settings_BindFromTheEmailSettingsSection_UsingTheUserSecretKeyNames()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSettings:SmtpServer"] = "smtp.gmail.com",
                ["EmailSettings:Port"] = "587",
                ["EmailSettings:SenderEmail"] = "library@gmail.com",
                ["EmailSettings:Password"] = Secret
            })
            .Build();

        var settings = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>()!;

        Assert.Equal("EmailSettings", EmailSettings.SectionName);
        Assert.Equal("smtp.gmail.com", settings.SmtpServer);
        Assert.Equal(587, settings.Port);
        Assert.Equal("library@gmail.com", settings.SenderEmail);
        Assert.Equal(Secret, settings.Password);
        Assert.True(settings.EnableSsl); // STARTTLS stays on unless explicitly disabled
    }

    [Fact]
    public async Task MissingCredentials_FailWithAClearConfigurationError()
    {
        var service = Create(s => { s.SenderEmail = ""; s.Password = ""; });

        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => service.SendEmailAsync("a@b.test", "s", "b"));

        Assert.Equal(EmailFailure.NotConfigured, ex.Failure);
        Assert.Contains("EmailSettings:SenderEmail", ex.Message);
    }

    [Fact]
    public async Task MissingAppPasswordOnly_IsAlsoNotConfigured()
    {
        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => Create(s => s.Password = " ").SendEmailAsync("a@b.test", "s", "b"));

        Assert.Equal(EmailFailure.NotConfigured, ex.Failure);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("two@@signs.test")]
    [InlineData("")]
    public async Task InvalidRecipient_IsRejectedBeforeAnyNetworkCall(string recipient)
    {
        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => Create().SendEmailAsync(recipient, "s", "b"));

        Assert.Equal(EmailFailure.InvalidRecipient, ex.Failure);
    }

    [Fact]
    public async Task UnreachableServer_IsReportedAsAConnectionFailure_WithoutLeakingTheSecret()
    {
        var ex = await Assert.ThrowsAsync<EmailDeliveryException>(
            () => Create().SendEmailAsync("someone@example.test", "Subject", "<p>Hi</p>"));

        Assert.Equal(EmailFailure.ConnectionFailed, ex.Failure);
        Assert.DoesNotContain(Secret, ex.Message);
        Assert.DoesNotContain(Secret, ex.ToString().Split("--->")[0]);
    }

    [Fact]
    public async Task CancellationByTheCaller_IsNotTurnedIntoADeliveryFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Create().SendEmailAsync("someone@example.test", "s", "b", cts.Token));
    }

    [Fact]
    public void Gmail_WrongAppPassword_IsMappedToAuthenticationFailed()
    {
        var gmail = new SmtpException(
            SmtpStatusCode.MustIssueStartTlsFirst,
            "The SMTP server requires a secure connection or the client was not authenticated. " +
            $"The server response was: 5.7.8 Username and Password not accepted. {Secret}");

        var result = EmailService.Translate(gmail);

        Assert.Equal(EmailFailure.AuthenticationFailed, result.Failure);
        Assert.Contains("App Password", result.Message);
        Assert.DoesNotContain(Secret, result.Message); // the server text is never copied over
    }

    [Fact]
    public void RefusedRecipient_IsMappedToInvalidRecipient()
    {
        var refused = new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "nobody@nowhere.test");

        Assert.Equal(EmailFailure.InvalidRecipient, EmailService.Translate(refused).Failure);
    }

    [Fact]
    public void NetworkProblems_AreMappedToConnectionFailed()
    {
        Assert.Equal(EmailFailure.ConnectionFailed,
            EmailService.Translate(new SmtpException("Failure sending mail.", new SocketException())).Failure);
        Assert.Equal(EmailFailure.ConnectionFailed,
            EmailService.Translate(new TimeoutException()).Failure);
        Assert.Equal(EmailFailure.ConnectionFailed,
            EmailService.Translate(new IOException("connection reset")).Failure);
        Assert.Equal(EmailFailure.ConnectionFailed,
            EmailService.Translate(new SmtpException(SmtpStatusCode.ServiceNotAvailable, "Unable to connect")).Failure);
    }

    [Fact]
    public void AnythingElse_IsUnknown()
    {
        Assert.Equal(EmailFailure.Unknown,
            EmailService.Translate(new InvalidOperationException("boom")).Failure);
    }
}

/// <summary>POST /api/email/test: admin only, switchable, validated.</summary>
public class EmailEndpointTests : IClassFixture<LibraryApiFactory>
{
    private const string Password = "Test@12345";

    private sealed class FakeEmail : IEmailService
    {
        public static readonly List<(string To, string Subject)> Sent = new();
        public static EmailFailure? FailWith;

        public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (FailWith is { } failure)
            {
                throw new EmailDeliveryException(failure, $"fake {failure}");
            }

            lock (Sent) Sent.Add((toEmail, subject));
            return Task.CompletedTask;
        }
    }

    private readonly LibraryApiFactory _factory;

    public EmailEndpointTests(LibraryApiFactory factory)
    {
        _factory = factory;
    }

    private WebApplicationFactoryWrapper Host(bool enabled = true)
        => new(_factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EmailSettings:EnableTestEndpoint"] = enabled ? "true" : "false"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService, FakeEmail>();
            });
        }));

    private sealed record Auth(string Token);

    private async Task<HttpClient> ClientAsync(WebApplicationFactoryWrapper host, string? role)
    {
        var client = host.Factory.CreateClient();
        if (role is null) return client;

        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@test.local";
        (await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = role, email, password = Password, deviceId = "email-test" })).EnsureSuccessStatusCode();

        if (role != "Member")
        {
            using var scope = host.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.Role = role;
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = Password, deviceId = "email-test" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<Auth>())!.Token);
        return client;
    }

    // Small wrapper so the sub-factory is disposed with the test.
    private sealed class WebApplicationFactoryWrapper(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory { get; } = factory;
    }

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        var client = await ClientAsync(Host(), null);

        var response = await client.PostAsJsonAsync("/api/email/test", new { toEmail = "a@b.test" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Librarian")]
    public async Task NonAdmins_AreForbidden_AndNothingIsSent(string role)
    {
        var before = FakeEmail.Sent.Count;
        var client = await ClientAsync(Host(), role);

        var response = await client.PostAsJsonAsync("/api/email/test", new { toEmail = "victim@example.test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, FakeEmail.Sent.Count);
    }

    [Fact]
    public async Task Admin_CanSendATestEmail()
    {
        FakeEmail.FailWith = null;
        var client = await ClientAsync(Host(), "Admin");

        var response = await client.PostAsJsonAsync("/api/email/test", new { toEmail = "admin-target@example.test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("admin-target@example.test", await response.Content.ReadAsStringAsync());
        Assert.Contains(FakeEmail.Sent, m => m.To == "admin-target@example.test");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a b@c.test")]
    public async Task InvalidRecipient_IsRejectedByValidation(string recipient)
    {
        var before = FakeEmail.Sent.Count;
        var client = await ClientAsync(Host(), "Admin");

        var response = await client.PostAsJsonAsync("/api/email/test", new { toEmail = recipient });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, FakeEmail.Sent.Count);
    }

    [Fact]
    public async Task WhenTheEndpointIsSwitchedOff_AdminsGetNotFound()
    {
        var client = await ClientAsync(Host(enabled: false), "Admin");

        var response = await client.PostAsJsonAsync("/api/email/test", new { toEmail = "a@b.test" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(EmailFailure.InvalidRecipient, HttpStatusCode.BadRequest)]
    [InlineData(EmailFailure.AuthenticationFailed, HttpStatusCode.BadGateway)]
    [InlineData(EmailFailure.ConnectionFailed, HttpStatusCode.ServiceUnavailable)]
    [InlineData(EmailFailure.NotConfigured, HttpStatusCode.ServiceUnavailable)]
    [InlineData(EmailFailure.Unknown, HttpStatusCode.InternalServerError)]
    public async Task DeliveryFailures_AreReportedWithAMatchingStatusAndMessage(
        EmailFailure failure, HttpStatusCode expected)
    {
        var client = await ClientAsync(Host(), "Admin");
        FakeEmail.FailWith = failure;

        try
        {
            var response = await client.PostAsJsonAsync("/api/email/test", new { toEmail = "a@b.test" });
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(expected, response.StatusCode);
            Assert.Contains(failure.ToString(), body);
        }
        finally
        {
            FakeEmail.FailWith = null;
        }
    }
}
