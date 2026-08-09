using System.Reflection;
using EPM.Application.Abstractions;

namespace EPM.Api.Extensions;

/// <summary>
/// Finds and maps every <see cref="IEndpoint"/> in an assembly.
/// </summary>
/// <remarks>
/// This is the mechanism that makes vertical slicing work without a controller. Adding a
/// feature means adding a folder; nothing central needs editing, so two people adding
/// endpoints in the same sprint never touch the same file.
/// </remarks>
public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var endpoints = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .Where(type => type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type));

        services.TryAddEnumerableRange(endpoints);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        // A shared route group so the /api prefix is declared once instead of being retyped
        // (and eventually mistyped) in every endpoint.
        var routeGroup = app.MapGroup("/api");

        foreach (var endpoint in app.Services.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(routeGroup);
        }

        return app;
    }

    private static void TryAddEnumerableRange(
        this IServiceCollection services,
        IEnumerable<ServiceDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            services.Add(descriptor);
        }
    }
}
