using FluentValidation;
using MediatR;
using ValidationException = EPM.Application.Common.ValidationException;

namespace EPM.Application.Common.Behaviours;

/// <summary>
/// Runs every validator registered for a request before the handler sees it.
/// </summary>
/// <remarks>
/// Doing this in the pipeline rather than at the top of each handler means a slice cannot
/// forget to validate — adding a validator class is enough to wire it up. Handlers are then
/// free to assume their input is structurally sound and spend their lines on business rules.
///
/// Requests with no validator pass straight through, which is the normal case for queries.
/// </remarks>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        // All validators run, not just up to the first failure: a form should light up every
        // bad field at once rather than making the user fix them one round trip at a time.
        var results = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        var errors = failures
            .GroupBy(failure => ToCamelCase(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        throw new ValidationException(errors);
    }

    // FluentValidation reports "HireDate"; the JSON the client sent said "hireDate". Matching
    // the payload is what lets the frontend map an error back onto its form control.
    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
        {
            return propertyName;
        }

        // Nested paths arrive as "Address.PostCode" — each segment needs the same treatment.
        return string.Join('.', propertyName.Split('.').Select(segment =>
            string.IsNullOrEmpty(segment) ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
    }
}
