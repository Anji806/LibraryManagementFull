using System.Security.Claims;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Controllers;

/// <summary>A member asks to be emailed when an unavailable book can be borrowed.</summary>
[ApiController]
[Route("api/availability-requests")]
[Authorize(Roles = "Member")]
public class AvailabilityRequestsController : ControllerBase
{
    private readonly IAvailabilityService _availability;

    public AvailabilityRequestsController(IAvailabilityService availability)
    {
        _availability = availability;
    }

    private int? CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public sealed class CreateAvailabilityRequestDto
    {
        public int BookId { get; set; }
    }

    // POST: api/availability-requests   { "bookId": 12 }
    [HttpPost]
    public async Task<IActionResult> Create(CreateAvailabilityRequestDto dto)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var result = await _availability.RequestAsync(userId, dto.BookId);

        return Ok(new
        {
            bookId = result.BookId,
            status = result.Status,
            position = result.Position,
            message = result.AlreadyWaiting
                ? "You are already on the waiting list for this book."
                : "You will be emailed when this book becomes available."
        });
    }

    // DELETE: api/availability-requests/12
    [HttpDelete("{bookId:int}")]
    public async Task<IActionResult> Cancel(int bookId)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        return await _availability.CancelAsync(userId, bookId)
            ? NoContent()
            : NotFound(new { message = "You are not waiting for this book." });
    }

    // GET: api/availability-requests/mine
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        return Ok(await _availability.GetMineAsync(userId));
    }
}
