using LibraryManagement.DTOs.LoginHistory;
using LibraryManagement.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LibraryManagement.Realtime;
using System.Text.Json;

namespace LibraryManagement.Controllers;

[ApiController]
[Route("api/login-history")]
[Authorize(Roles = "Admin")]
public class LoginHistoryController(ILoginHistoryService service, ILoginActivityEventBus eventBus) : ControllerBase
{
    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);

    public sealed class DeleteLoginHistoryRequest
    {
        public IReadOnlyList<long>? LoginLogIds { get; set; } = [];
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] LoginHistoryQuery query)
    {
        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100 ||
            (query.Status is not null and not "Success" and not "Failed") ||
            (query.SortDirection is not "asc" and not "desc") ||
            (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc >= query.ToUtc))
            return BadRequest(new { message = "Invalid login history filters." });

        return Ok(await service.GetLoginHistoryAsync(query));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] DeleteLoginHistoryRequest? request)
    {
        var ids = request?.LoginLogIds;
        if (ids is null || ids.Count is < 1 or > 100 || ids.Any(id => id < 1) ||
            ids.Distinct().Count() != ids.Count)
            return BadRequest(new { message = "Select between 1 and 100 unique login history records." });

        var deleted = await service.DeleteLoginHistoryAsync(ids);
        if (deleted < 0)
            return NotFound(new { message = "One or more selected login history records no longer exist." });
        return Ok(new { deletedCount = deleted });
    }

    [HttpGet("events")]
    public async Task StreamEvents(CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers.Append("X-Accel-Buffering", "no");

        using var subscription = eventBus.Subscribe();
        try
        {
            await Response.WriteAsync(": connected\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var available = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();
                var heartbeat = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                if (await Task.WhenAny(available, heartbeat) == heartbeat)
                {
                    await Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    continue;
                }

                if (!await available) break;
                while (subscription.Reader.TryRead(out var activity))
                {
                    var data = JsonSerializer.Serialize(activity.Record, EventJsonOptions);
                    await Response.WriteAsync($"event: {activity.Name}\ndata: {data}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The admin closed the activity page or the browser disconnected.
        }
    }
}
