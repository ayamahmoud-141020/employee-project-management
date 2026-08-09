using EPM.Application.Common;
using EPM.Application.Common.Http;
using Microsoft.AspNetCore.Diagnostics;

namespace EPM.Api.Middleware;

/// <summary>
/// Catches anything that escaped a handler and returns it in the same envelope as everything
/// else, so the client never has to parse two different error shapes.
/// </summary>
/// <remarks>
/// Uses IExceptionHandler rather than a hand-written middleware — it composes with the
/// framework's own diagnostics and keeps the pipeline in Program.cs to one line.
///
/// Only two categories reach here. Validation failures, thrown by the pipeline behaviour by
/// design. And genuine faults, which are logged in full and reported to the caller without
/// the detail: a stack trace tells an attacker about your dependencies and file layout, and
/// tells a legitimate user nothing they can act on.
/// </remarks>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, payload) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                ApiResponse.Fail("One or more validation errors occurred.", "Validation.Failed", validation.Errors)),

            // A cancelled request is the client hanging up, not a server fault. 499 is
            // non-standard but it keeps these out of the 5xx error rate.
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (
                499,
                ApiResponse.Fail("The request was cancelled.", "Request.Cancelled")),

            _ => (
                StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("An unexpected error occurred. Please try again.", "Server.Unexpected")),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation(
                "Request {Method} {Path} rejected with {StatusCode}: {Code}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode,
                payload.Code);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(payload, cancellationToken);

        return true;
    }
}
