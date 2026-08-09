using EPM.Application.Common;
using Microsoft.OpenApi.Models;

namespace EPM.Api.Extensions;

public static class SwaggerExtensions
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Employee & Project Management API",
                Version = "v1",
                Description = BuildDescription(),
            });

            // Without this the "Authorize" button does not appear and every protected endpoint
            // has to be tested with curl instead. Http/bearer (not ApiKey) so Swagger UI adds
            // the "Bearer " prefix itself — pasting a raw token is the usual first stumble.
            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the accessToken returned by POST /api/auth/login. The 'Bearer' prefix is added for you.",
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = BearerScheme },
                }] = []
            });
        });

        return services;
    }

    // Generated from the same policy table the endpoints enforce, so the documented role
    // matrix cannot drift away from the one that actually runs.
    private static string BuildDescription()
    {
        var lines = Policies.RolesByPolicy
            .Select(entry => $"- **{entry.Key}**: {string.Join(", ", entry.Value)}");

        // $$ raw literal: the envelope example is JSON, so braces are content here and only
        // {{...}} is treated as interpolation.
        return $$"""
                REST API for managing employees, departments, projects and project assignments.

                Every response uses the same envelope: `{ "success": bool, "message": string?, "data": T?, "code": string?, "errors": {}? }`.

                **Authorization policies**

                {{string.Join("\n", lines)}}

                Sign in with `POST /api/auth/login` using one of the seeded accounts
                (`admin@epm.local`, `manager@epm.local`, `user@epm.local`) and the passwords from your `.env`.
                """;
    }
}
