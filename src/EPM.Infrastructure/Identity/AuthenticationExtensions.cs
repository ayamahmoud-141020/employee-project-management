using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EPM.Application.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

namespace EPM.Infrastructure.Identity;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApplicationAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at boot, not on the first login. A missing signing key is a deployment
            // mistake, and the fastest way to find one is the app refusing to start.
            .ValidateOnStart();

        services.AddOptions<EntraIdOptions>()
            .Bind(configuration.GetSection(EntraIdOptions.SectionName))
            .Validate(
                options => !options.Enabled || options.IsConfigured,
                "EntraId:Enabled is true but TenantId and ClientId are not both set.")
            .ValidateOnStart();

        // Whether to register the Entra scheme at all has to be decided now, because adding an
        // authentication scheme is a registration-time act. Reading the flag eagerly is safe:
        // a host that layers configuration on later is changing values, not deciding whether
        // an entire identity provider exists.
        var entraOptions = configuration.GetSection(EntraIdOptions.SectionName).Get<EntraIdOptions>()
                           ?? new EntraIdOptions();

        var authenticationBuilder = services
            .AddAuthentication(AuthenticationSchemes.Smart)
            .AddPolicyScheme(AuthenticationSchemes.Smart, AuthenticationSchemes.Smart, options =>
            {
                // Both token types hit the same endpoints, and a JwtBearer handler configured
                // for one issuer will reject the other's tokens outright. Rather than making
                // every endpoint declare which scheme it accepts, this peeks at the token's
                // `iss` claim and forwards to the handler that can actually validate it.
                options.ForwardDefaultSelector = context =>
                {
                    var issuer = context.RequestServices
                        .GetRequiredService<IOptionsMonitor<JwtOptions>>()
                        .CurrentValue.Issuer;

                    return ResolveScheme(context, issuer, entraOptions.IsConfigured);
                };
            });

        // The bearer options are configured through the options system rather than inline, so
        // the signing key is read when the scheme is first used and not when it is registered.
        // Capturing `configuration` here would freeze whatever was bound at that moment — which
        // in the integration tests is nothing at all, since the test host adds its settings
        // after this method has run.
        services.AddOptions<JwtBearerOptions>(AuthenticationSchemes.Local)
            .Configure<IOptionsMonitor<JwtOptions>>((bearer, jwt) =>
            {
                var options = jwt.CurrentValue;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
                    ValidateLifetime = true,
                    // Default is five minutes of grace, which quietly extends every token's
                    // life. Zero means "expired" means expired.
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        authenticationBuilder.AddJwtBearer(AuthenticationSchemes.Local);

        if (entraOptions.IsConfigured)
        {
            // Registered only when a tenant is actually configured. Microsoft.Identity.Web
            // fetches the signing keys from the authority's discovery document on first use,
            // so wiring it up without a tenant would fail at runtime rather than at boot.
            authenticationBuilder.AddMicrosoftIdentityWebApi(
                configuration.GetSection(EntraIdOptions.SectionName),
                jwtBearerScheme: AuthenticationSchemes.EntraId);

            services.AddSingleton<IClaimsTransformation, EntraIdClaimsTransformation>();
        }

        return services;
    }

    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(AuthenticationSchemes.Smart)
                .RequireAuthenticatedUser()
                .Build());

        // Built from the same table the endpoints and the Swagger docs read, so the three can
        // never disagree about who is allowed to do what.
        foreach (var (policyName, roles) in Policies.RolesByPolicy)
        {
            services.AddAuthorizationBuilder()
                .AddPolicy(policyName, policy => policy
                    .AddAuthenticationSchemes(AuthenticationSchemes.Smart)
                    .RequireAuthenticatedUser()
                    .RequireRole(roles));
        }

        return services;
    }

    private static string ResolveScheme(HttpContext context, string localIssuer, bool entraConfigured)
    {
        if (!entraConfigured)
        {
            return AuthenticationSchemes.Local;
        }

        var token = ReadBearerToken(context);

        if (token is null)
        {
            return AuthenticationSchemes.Local;
        }

        var handler = new JwtSecurityTokenHandler();

        // Reading is not validating — nothing is trusted here. The worst a forged issuer can
        // do is get the token sent to the wrong handler, which then rejects it.
        if (!handler.CanReadToken(token))
        {
            return AuthenticationSchemes.Local;
        }

        var issuer = handler.ReadJwtToken(token).Issuer;

        return string.Equals(issuer, localIssuer, StringComparison.Ordinal)
            ? AuthenticationSchemes.Local
            : AuthenticationSchemes.EntraId;
    }

    private static string? ReadBearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : null;
    }
}
