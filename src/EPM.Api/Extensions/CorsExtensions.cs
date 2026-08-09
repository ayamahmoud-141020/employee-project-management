namespace EPM.Api.Extensions;

/// <summary>Allowed browser origins, bound from the "Cors" section.</summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Explicit list, never "*". The API sends credentials-bearing requests, and a wildcard
    /// origin is both refused by browsers in that mode and wrong in principle.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];
}

public static class CorsExtensions
{
    public const string PolicyName = "AngularClient";

    public static IServiceCollection AddApplicationCors(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        services.AddCors(cors => cors.AddPolicy(PolicyName, policy =>
        {
            // Only matters when the Angular dev server talks to the API directly. Running
            // through the compose stack, requests go via the dev-server proxy and are
            // same-origin, so CORS never comes into it.
            policy.WithOrigins(options.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }));

        return services;
    }
}
