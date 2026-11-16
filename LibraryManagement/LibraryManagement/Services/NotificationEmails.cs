using System.Net;
using LibraryManagement.Models;

namespace LibraryManagement.Services;

/// <summary>HTML for the three notification emails.</summary>
public static class NotificationEmails
{
    public static (string Subject, string Body) DueSoon(
        string userName, string bookTitle, DateOnly issued, DateOnly due, int daysLeft,
        NotificationOptions options)
    {
        var when = daysLeft switch
        {
            0 => "<strong>today</strong>",
            1 => "in <strong>1 day</strong>",
            _ => $"in <strong>{daysLeft} days</strong>"
        };

        return (
            "Reminder: Your Library Book Is Due Soon",
            Layout("Your book is due soon", userName, options, $"""
                <p>This is a friendly reminder that the following book is due {when}.</p>
                {Details(
                    ("Book", bookTitle),
                    ("Issued on", Format(issued)),
                    ("Due date", Format(due)),
                    ("Days remaining", daysLeft.ToString()))}
                <p>Please return it on or before the due date to avoid a late fine.
                   You can return it at the library desk.</p>
                """));
    }

    public static (string Subject, string Body) Overdue(
        string userName, string bookTitle, DateOnly due, int overdueDays,
        decimal outstandingFine, decimal finePerDay, NotificationOptions options)
    {
        var fineRows = outstandingFine > 0
            ? new[]
            {
                ("Late fine so far", Money(outstandingFine)),
                ("Fine rate", $"{Money(finePerDay)} per day")
            }
            : [("Late fine so far", "No fine has been charged yet")];

        return (
            "Overdue Book Notification",
            Layout("Your book is overdue", userName, options, $"""
                <p>The following book was due on <strong>{Format(due)}</strong> and has not been returned yet.</p>
                {Details(
                    new[] { ("Book", bookTitle), ("Original due date", Format(due)), ("Days overdue", overdueDays.ToString()) }
                        .Concat(fineRows).ToArray())}
                <p>Please return the book to the library desk as soon as possible. A late fine
                   keeps growing each day until the book is returned.</p>
                """));
    }

    public static (string Subject, string Body) BookAvailable(
        string userName, string bookTitle, int availableCopies, NotificationOptions options)
    {
        var link = string.IsNullOrWhiteSpace(options.AppUrl)
            ? string.Empty
            : $"""<p style="margin:22px 0"><a href="{Html(options.AppUrl.TrimEnd('/'))}/books" style="background:#011F5B;color:#ffffff;padding:11px 20px;border-radius:4px;text-decoration:none;font-weight:600">Open the catalog</a></p>""";

        return (
            "Good News! Your Requested Book Is Available",
            Layout("Your requested book is available", userName, options, $"""
                <p>A copy of a book you asked about has just been returned and is ready to borrow.</p>
                {Details(
                    ("Book", bookTitle),
                    ("Status", "Available"),
                    ("Copies available", availableCopies.ToString()))}
                <p>Sign in to the library and borrow it from the catalog. Copies are given on a
                   first-come, first-served basis, so borrow it soon.</p>
                {link}
                """));
    }

    // ----------------------------------------------------------------------

    private static string Layout(string heading, string userName, NotificationOptions options, string content)
    {
        var name = Html(string.IsNullOrWhiteSpace(userName) ? "there" : userName);
        var contact = string.IsNullOrWhiteSpace(options.LibraryContact)
            ? string.Empty
            : $"""<p style="font-size:13px;color:#595F6A">Library contact: {Html(options.LibraryContact)}</p>""";

        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;color:#2D3545">
              <h2 style="color:#011F5B;margin-bottom:4px">{Html(heading)}</h2>
              <p>Hello {name},</p>
              {content}
              <hr style="border:none;border-top:1px solid #D9DBE0;margin:24px 0">
              {contact}
              <p style="font-size:12px;color:#8a909a">Greenleaf Library &middot; This is an automated message.</p>
            </div>
            """;
    }

    private static string Details(params (string Label, string Value)[] rows)
        => "<table style=\"border-collapse:collapse;margin:14px 0;width:100%\">" +
           string.Concat(rows.Select(row =>
               "<tr>" +
               $"<td style=\"padding:6px 12px 6px 0;color:#595F6A;white-space:nowrap\">{Html(row.Label)}</td>" +
               $"<td style=\"padding:6px 0;font-weight:600\">{Html(row.Value)}</td>" +
               "</tr>")) +
           "</table>";

    private static string Format(DateOnly date) => date.ToString("dd MMM yyyy");

    private static string Money(decimal amount) => "₹" + amount.ToString("0.00");

    private static string Html(string value) => WebUtility.HtmlEncode(value);
}
