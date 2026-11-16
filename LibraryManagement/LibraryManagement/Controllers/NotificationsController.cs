using LibraryManagement.Data;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Controllers;

/// <summary>Administrators can see what was emailed and run the checks on demand.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NotificationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET: api/notifications?type=Overdue&status=Failed&page=1&pageSize=25
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new { message = "Invalid paging values." });
        }

        var query = _db.Notifications.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(n => n.NotificationType == type);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(n => n.Status == status);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_db.Users, n => n.UserId, u => u.UserId, (n, u) => new { n, u })
            .Join(_db.Books, x => x.n.BookId, b => b.BookId, (x, b) => new
            {
                x.n.Id,
                x.n.NotificationType,
                x.n.Status,
                x.n.Attempts,
                x.n.NotificationDate,
                x.n.CreatedAt,
                x.n.LastAttemptAt,
                x.n.SentAt,
                x.n.ErrorMessage,
                x.n.UserId,
                UserName = x.u.FullName,
                UserEmail = x.u.Email,
                x.n.BookId,
                BookTitle = b.Title,
                x.n.TransactionId
            })
            .ToListAsync();

        return Ok(new { items, totalCount = total, page, pageSize });
    }

    // GET: api/notifications/summary
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var rows = await _db.Notifications
            .AsNoTracking()
            .GroupBy(n => new { n.NotificationType, n.Status })
            .Select(g => new { g.Key.NotificationType, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        return Ok(rows);
    }

    // POST: api/notifications/run-now
    // Runs the same checks as the scheduler right now (ignoring the send-after hour).
    [HttpPost("run-now")]
    public async Task<IActionResult> RunNow(
        [FromServices] ILoanNotificationService loans,
        [FromServices] IAvailabilityService availability,
        CancellationToken cancellationToken)
    {
        var summary = await loans.RunAsync(null, ignoreSendWindow: true, cancellationToken);
        var availabilitySent = await availability.NotifyAllWaitingAsync(cancellationToken);

        return Ok(new
        {
            dueReminders = summary.DueReminders,
            overdueNotices = summary.OverdueNotices,
            availabilityNotices = availabilitySent,
            failed = summary.Failed,
            alreadyHandled = summary.SkippedAlreadyHandled,
            emailNotConfigured = summary.SkippedEmailNotConfigured
        });
    }
}
