using EPM.Domain.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EPM.Application.Common.Behaviours;

/// <summary>
/// Logs the outcome of every request that goes through the bus.
/// </summary>
/// <remarks>
/// Failed Results are the interesting part. They never throw, so without this a refused
/// operation leaves no trace at all — the API returns 409 and the logs stay silent. Logging
/// the error code (not the message) keeps the entries greppable when the wording changes.
/// </remarks>
public sealed class RequestLoggingBehaviour<TRequest, TResponse>(
    ILogger<RequestLoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation("Handling {RequestName}", requestName);

        var response = await next();

        if (response is Result { IsFailure: true } failure)
        {
            logger.LogWarning(
                "{RequestName} refused: {ErrorCode} ({ErrorType})",
                requestName,
                failure.Error.Code,
                failure.Error.Type);
        }
        else
        {
            logger.LogInformation("{RequestName} completed", requestName);
        }

        return response;
    }
}
