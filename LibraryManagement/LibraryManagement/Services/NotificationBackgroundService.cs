using LibraryManagement.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Services;

/// <summary>
/// Runs the notification checks on a timer, independently of any API request:
/// due-soon reminders, overdue notices and a sweep for waiting availability
/// requests. Each run uses its own DI scope. Duplicate prevention lives in the
/// database (see NotificationDispatcher), so overlapping instances, restarts
/// and retries cannot send the same email twice.
/// </summary>
public class NotificationBackgroundService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationBackgroundService> _logger;

    public NotificationBackgroundService(
        IServiceScopeFactory scopes,
        IOptions<NotificationOptions> options,
        ILogger<NotificationBackgroundService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Notification scheduler is disabled (Notifications:Enabled = false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.ScanIntervalMinutes));

        _logger.LogInformation(
            "Notification scheduler started: checking every {Minutes} minute(s).", interval.TotalMinutes);

        try
        {
            // Let the application finish starting (and migrating) first.
            await Task.Delay(StartupDelay, stoppingToken);

            using var timer = new PeriodicTimer(interval);

            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            await scope.ServiceProvider.GetRequiredService<ILoanNotificationService>()
                .RunAsync(nowUtc: null, ignoreSendWindow: false, cancellationToken);

            await scope.ServiceProvider.GetRequiredService<IAvailabilityService>()
                .NotifyAllWaitingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A failed run must never stop the scheduler: try again next tick.
            _logger.LogError("Notification run failed ({ExceptionType}); will retry next interval.", ex.GetType().Name);
        }
    }
}
