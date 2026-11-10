using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Common;

/// <summary>SQLite result codes used by the error mapping (sqlite3.h).</summary>
internal static class SqliteErrorCodes
{
    public const int Busy = 5;
    public const int Locked = 6;
    public const int Constraint = 19;
}

/// <summary>Expected failures. Services throw these; <see cref="AppExceptionHandler"/> turns them
/// into RFC 7807 problem responses.</summary>
public abstract class AppException(string message) : Exception(message);

/// <summary>400 with a field → messages map. Keys are camelCase request property names.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("One or more fields are invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationException For(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>404.</summary>
public sealed class NotFoundException(string entity, int id) : AppException($"{entity} {id} was not found.");

/// <summary>409: the request is valid but breaks a rule (e.g. deleting a company that is still referenced).</summary>
public sealed class ConflictException(string message) : AppException(message);

internal sealed class AppExceptionHandler(IProblemDetailsService problemDetails, ILogger<AppExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException v => new ValidationProblemDetails(v.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = v.Message,
            },
            NotFoundException n => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = n.Message },
            ConflictException c => new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = c.Message },
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: SqliteErrorCodes.Constraint } } => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The change conflicts with existing data (a constraint was violated).",
            },
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The record was changed or deleted in the meantime. Reload and try again.",
            },
            SqliteException { SqliteErrorCode: SqliteErrorCodes.Busy or SqliteErrorCodes.Locked } => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The database is busy. Try again in a moment.",
            },
            DbUpdateException or SqliteException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The database could not complete the request.",
            },
            BadHttpRequestException b => new ProblemDetails { Status = b.StatusCode, Title = "The request could not be read." },
            _ => new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "Something went wrong on the server." },
        };

        if (problem.Status >= 500)
        {
            logger.LogError(exception, "Request {Method} {Path} failed", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status ?? 500;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
