using LibraryManagement.Data;
using LibraryManagement.Exceptions;
using LibraryManagement.Models;
using LibraryManagement.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Services.Implementations;

/// <summary>
/// "Tell me when this book is available". No reservation feature existed, so
/// this adds a simple first-come, first-served queue per book. It does not
/// reserve a copy: it only tells members, in order, that a copy is free.
/// </summary>
public class AvailabilityService : IAvailabilityService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationDispatcher _dispatcher;
    private readonly BorrowingPolicy _policy;
    private readonly NotificationOptions _options;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AvailabilityService> _logger;

    public AvailabilityService(
        ApplicationDbContext db,
        INotificationDispatcher dispatcher,
        BorrowingPolicy policy,
        IOptions<NotificationOptions> options,
        IServiceScopeFactory scopes,
        ILogger<AvailabilityService> logger)
    {
        _db = db;
        _dispatcher = dispatcher;
        _policy = policy;
        _options = options.Value;
        _scopes = scopes;
        _logger = logger;
    }

    // ------------------------------------------------------------ member side

    public async Task<AvailabilityRequestResult> RequestAsync(int userId, int bookId)
    {
        var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.BookId == bookId)
            ?? throw new NotFoundException("Book not found.");

        // Never ask to be told about something that can be borrowed right now.
        if (book.AvailableCopies > 0)
        {
            throw new BusinessRuleException("This book is available now. You can borrow it instead.");
        }

        var alreadyBorrowed = await _db.BorrowTransactions.AnyAsync(t =>
            t.BookId == bookId && t.MemberId == userId && t.ReturnDate == null);

        if (alreadyBorrowed)
        {
            throw new BusinessRuleException("You have already borrowed this book.");
        }

        var waiting = await _db.BookAvailabilityRequests.AsNoTracking().FirstOrDefaultAsync(r =>
            r.BookId == bookId && r.UserId == userId && r.Status == AvailabilityStatuses.Waiting);

        if (waiting is not null)
        {
            return new AvailabilityRequestResult(bookId, waiting.Status, await PositionAsync(waiting), true);
        }

        var request = new BookAvailabilityRequest
        {
            UserId = userId,
            BookId = bookId,
            Status = AvailabilityStatuses.Waiting,
            CreatedAt = DateTime.UtcNow
        };

        _db.BookAvailabilityRequests.Add(request);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two identical requests raced; the unique rule kept one.
            _db.Entry(request).State = EntityState.Detached;
            var existing = await _db.BookAvailabilityRequests.AsNoTracking().FirstAsync(r =>
                r.BookId == bookId && r.UserId == userId && r.Status == AvailabilityStatuses.Waiting);
            return new AvailabilityRequestResult(bookId, existing.Status, await PositionAsync(existing), true);
        }

        return new AvailabilityRequestResult(bookId, request.Status, await PositionAsync(request), false);
    }

    public async Task<bool> CancelAsync(int userId, int bookId)
    {
        var changed = await _db.BookAvailabilityRequests
            .Where(r => r.UserId == userId && r.BookId == bookId && r.Status == AvailabilityStatuses.Waiting)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, AvailabilityStatuses.Cancelled));

        return changed > 0;
    }

    public async Task<List<MyAvailabilityRequest>> GetMineAsync(int userId)
    {
        var rows = await _db.BookAvailabilityRequests
            .AsNoTracking()
            .Where(r => r.UserId == userId &&
                        (r.Status == AvailabilityStatuses.Waiting || r.Status == AvailabilityStatuses.Notified))
            .Join(_db.Books, r => r.BookId, b => b.BookId, (r, b) => new { Request = r, b.Title })
            .OrderBy(x => x.Request.CreatedAt)
            .ToListAsync();

        var result = new List<MyAvailabilityRequest>();

        foreach (var row in rows)
        {
            int? position = row.Request.Status == AvailabilityStatuses.Waiting
                ? await PositionAsync(row.Request)
                : null;

            result.Add(new MyAvailabilityRequest(
                row.Request.BookId, row.Title, row.Request.Status, row.Request.CreatedAt, position));
        }

        return result;
    }

    private Task<int> PositionAsync(BookAvailabilityRequest request)
        => _db.BookAvailabilityRequests.CountAsync(r =>
            r.BookId == request.BookId &&
            r.Status == AvailabilityStatuses.Waiting &&
            (r.CreatedAt < request.CreatedAt || (r.CreatedAt == request.CreatedAt && r.Id <= request.Id)));

    // ------------------------------------------------------- notifying waiters

    public async Task<int> NotifyForBookAsync(int bookId, CancellationToken cancellationToken)
    {
        var book = await _db.Books.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken);

        // Only tell people about a copy that really exists right now.
        if (book is null || book.AvailableCopies <= 0)
        {
            return 0;
        }

        // Members emailed recently are still "holding" a copy: they have first
        // claim, so those copies must not be offered to the next person too.
        var now = DateTime.UtcNow;
        var holdSince = now.AddHours(-Math.Max(0, _options.AvailabilityHoldHours));

        var held = await _db.BookAvailabilityRequests.CountAsync(r =>
            r.BookId == bookId &&
            r.Status == AvailabilityStatuses.Notified &&
            r.NotifiedAt > holdSince, cancellationToken);

        var slots = book.AvailableCopies - held;
        if (slots <= 0)
        {
            return 0;
        }

        // First come, first served.
        var queue = await _db.BookAvailabilityRequests
            .AsNoTracking()
            .Where(r => r.BookId == bookId && r.Status == AvailabilityStatuses.Waiting)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Take(slots)
            .ToListAsync(cancellationToken);

        var today = _policy.ToLocalDate(now);
        var notified = 0;

        foreach (var request in queue)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

            if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
            {
                await SetStatusAsync(request.Id, AvailabilityStatuses.Cancelled, notifiedAt: null);
                continue;
            }

            var mail = NotificationEmails.BookAvailable(
                user.FullName, book.Title, book.AvailableCopies, _options);

            var result = await _dispatcher.SendAsync(
                new NotificationDraft(
                    user.UserId, bookId, null, NotificationTypes.BookAvailable,
                    $"avail:{request.Id}", today),
                user.Email, mail.Subject, mail.Body, cancellationToken);

            switch (result)
            {
                case DispatchResult.Sent:
                case DispatchResult.AlreadySent:
                    await SetStatusAsync(request.Id, AvailabilityStatuses.Notified, DateTime.UtcNow);
                    notified++;
                    break;

                case DispatchResult.GaveUp:
                    // Undeliverable: free the slot for the next member in line.
                    await SetStatusAsync(request.Id, AvailabilityStatuses.Failed, notifiedAt: null);
                    break;

                // Failed / NotDue / Busy: stays Waiting; a later run retries it.
            }
        }

        return notified;
    }

    public async Task<int> NotifyAllWaitingAsync(CancellationToken cancellationToken)
    {
        var bookIds = await _db.BookAvailabilityRequests
            .AsNoTracking()
            .Where(r => r.Status == AvailabilityStatuses.Waiting)
            .Join(_db.Books.Where(b => b.AvailableCopies > 0), r => r.BookId, b => b.BookId, (r, b) => b.BookId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var total = 0;

        foreach (var bookId in bookIds)
        {
            total += await NotifyForBookAsync(bookId, cancellationToken);
        }

        return total;
    }

    public void NotifyForBookInBackground(int bookId)
    {
        // The return request must not wait for emails; the scheduler also sweeps
        // periodically, so a missed or failed attempt is picked up later.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IAvailabilityService>()
                    .NotifyForBookAsync(bookId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Availability notification for book {BookId} failed ({ExceptionType}); the scheduler will retry.",
                    bookId, ex.GetType().Name);
            }
        });
    }

    public async Task MarkFulfilledAsync(int userId, int bookId)
    {
        await _db.BookAvailabilityRequests
            .Where(r => r.UserId == userId && r.BookId == bookId &&
                        (r.Status == AvailabilityStatuses.Waiting || r.Status == AvailabilityStatuses.Notified))
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, AvailabilityStatuses.Fulfilled));
    }

    private Task SetStatusAsync(int requestId, string status, DateTime? notifiedAt)
        => _db.BookAvailabilityRequests
            .Where(r => r.Id == requestId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.NotifiedAt, notifiedAt));
}
