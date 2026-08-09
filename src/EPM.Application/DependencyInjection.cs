using System.Reflection;
using EPM.Application.Common.Behaviours;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EPM.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Marker for assembly scanning. Using a type from this assembly rather than a hardcoded
    /// name means a rename cannot silently register nothing.
    /// </summary>
    public static readonly Assembly Assembly = typeof(DependencyInjection).Assembly;

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(Assembly);

            // Order is the execution order. Logging wraps validation so a request that fails
            // validation is still recorded as having been attempted.
            configuration.AddOpenBehavior(typeof(RequestLoggingBehaviour<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        // Picks up every AbstractValidator in the slices. A new validator is wired up by
        // existing, which is the point of keeping it beside its command.
        services.AddValidatorsFromAssembly(Assembly, includeInternalTypes: true);

        return services;
    }
}
