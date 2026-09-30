using System.Text.Json;
using FlagForge.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ServiceDefaults;

/// <summary>
/// Maps expected failures to RFC 9457 ProblemDetails: validation (400, with an <c>errors</c> map keyed by field
/// path), 401, 403, 404, and 409 (with extensions such as <c>currentVersion</c>). Anything else falls through to the
/// default 500 handler, which logs it.
/// </summary>
internal sealed class AppExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    // SQL Server duplicate key errors: unique index (2601) and unique constraint (2627).
    private const int DuplicateKeyIndex = 2601;
    private const int DuplicateKeyConstraint = 2627;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var problem = ToProblem(exception);
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    public static ProblemDetails? ToProblem(Exception exception) => exception switch
    {
        RequestValidationException e => new HttpValidationProblemDetails(e.Errors.ToDictionary(k => k.Key, k => k.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Some fields need attention.",
            Detail = "Fix the highlighted fields and try again.",
        },
        BadHttpRequestException { InnerException: JsonException json } => new HttpValidationProblemDetails(
            new Dictionary<string, string[]> { [ToFieldPath(json.Path)] = [WithoutLocation(json.Message)] })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The request body does not match this endpoint.",
            Detail = "Check the field below; it is missing, has the wrong type, or has an unknown value.",
        },
        BadHttpRequestException e => Problem(e.StatusCode, "The request could not be read.", e.Message),
        UnauthorizedException e => Problem(StatusCodes.Status401Unauthorized, "Sign-in required.", e.Message),
        ForbiddenException e => Problem(StatusCodes.Status403Forbidden, "Not allowed.", e.Message),
        NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not found.", e.Message),
        ConflictException e => WithExtensions(Problem(StatusCodes.Status409Conflict, "Conflict.", e.Message), e.Extensions),
        DbUpdateException { InnerException: SqlException { Number: DuplicateKeyIndex or DuplicateKeyConstraint } } =>
            Problem(StatusCodes.Status409Conflict, "Conflict.", "Something with the same key was created at the same time. Refresh and try again."),
        _ => null,
    };

    private static ProblemDetails Problem(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail };

    private static ProblemDetails WithExtensions(ProblemDetails problem, IReadOnlyDictionary<string, object?> extensions)
    {
        foreach (var (key, value) in extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }

    /// <summary>Turns <c>$.config.rules[0].operator</c> into <c>config.rules[0].operator</c>.</summary>
    private static string ToFieldPath(string? jsonPath) =>
        string.IsNullOrEmpty(jsonPath) || jsonPath == "$" ? "body" : jsonPath.TrimStart('$').TrimStart('.');

    private static string WithoutLocation(string message)
    {
        var index = message.IndexOf(" Path:", StringComparison.Ordinal);
        return index < 0 ? message : message[..index];
    }
}
