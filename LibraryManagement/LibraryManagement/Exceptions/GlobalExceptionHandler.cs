using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LibraryManagement.Exceptions;

public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await HandleExceptionAsync(
                context,
                HttpStatusCode.NotFound,
                ex.Message);
        }
        catch (BusinessRuleException ex)
        {
            await HandleExceptionAsync(
                context,
                HttpStatusCode.Conflict,
                ex.Message);
        }
        catch (ArgumentException ex)
        {
            await HandleExceptionAsync(
                context,
                HttpStatusCode.BadRequest,
                ex.Message);
        }
        catch (DbUpdateException ex) when (IsConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "A database constraint rejected request {Method} {Path}.", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, HttpStatusCode.Conflict, "The requested change conflicts with existing data.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing {Method} {Path}.", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(
                context,
                HttpStatusCode.InternalServerError,
                "An unexpected server error occurred.");
        }
    }

    // PostgreSQL SQLSTATE class 23 = integrity constraint violation
    // (unique, foreign key, not null, check). Other providers (the SQLite
    // test host) only surface constraint failures through DbUpdateException.
    private static bool IsConstraintViolation(DbUpdateException ex)
        => ex is not DbUpdateConcurrencyException &&
           (ex.InnerException is not PostgresException postgres ||
            postgres.SqlState.StartsWith("23", StringComparison.Ordinal));

    private static async Task HandleExceptionAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            statusCode = (int)statusCode,
            message = message
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}
