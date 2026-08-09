using EPM.Domain.Abstractions;
using Microsoft.AspNetCore.Http;

namespace EPM.Application.Common.Http;

/// <summary>
/// Turns a domain <see cref="Result"/> into an HTTP response.
/// </summary>
/// <remarks>
/// The single place where an ErrorType becomes a status code. Handlers stay unaware of HTTP,
/// and adding a new error type is one line here rather than a hunt through every endpoint.
/// </remarks>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(ApiResponse<T>.Ok(result.Value))
            : Problem(result.Error);

    /// <summary>
    /// For a POST that created something. Sets Location so the response is a proper 201
    /// rather than a 200 that happens to contain an id.
    /// </summary>
    public static IResult ToCreatedResult<T>(this Result<T> result, Func<T, string> locationFactory) =>
        result.IsSuccess
            ? Results.Created(locationFactory(result.Value), ApiResponse<T>.Ok(result.Value))
            : Problem(result.Error);

    public static IResult ToHttpResult(this Result result, string successMessage) =>
        result.IsSuccess
            ? Results.Ok(ApiResponse.Ok(successMessage))
            : Problem(result.Error);

    private static IResult Problem(Error error)
    {
        var payload = ApiResponse.Fail(error.Message, error.Code);

        return error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(payload),
            // 409, not 400: the request was well formed, it just lost a race or clashes with
            // state the client could not have known about — a duplicate email, a department
            // that still has people in it.
            ErrorType.Conflict => Results.Conflict(payload),
            ErrorType.Forbidden => Results.Json(payload, statusCode: StatusCodes.Status403Forbidden),
            ErrorType.Unauthorized => Results.Json(payload, statusCode: StatusCodes.Status401Unauthorized),
            _ => Results.BadRequest(payload),
        };
    }
}
