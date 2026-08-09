using System.Text.Json.Serialization;
using EPM.Api.Extensions;
using EPM.Api.Middleware;
using EPM.Application;
using EPM.Infrastructure;
using EPM.Infrastructure.Persistence;
using EPM.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Endpoints live in the Application assembly next to their handlers, so that is what gets
// scanned — see EPM.Application.Abstractions.IEndpoint for why there is no Controllers folder.
builder.Services.AddEndpoints(EPM.Application.DependencyInjection.Assembly);

builder.Services.AddApplicationCors(builder.Configuration);
builder.Services.AddSwagger();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enums as strings both ways. "Active" survives a refactor that renumbers the enum, and a
    // client reading `status: 2` would have to keep its own copy of the mapping.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DisplayRequestDuration());

    await app.ApplyMigrationsAndSeedAsync();
}

// Only redirect when an HTTPS port is actually configured. In the container the API listens
// on plain HTTP behind the compose network, and an unconditional redirect there turns every
// request into a 307 to a port nothing is listening on.
if (app.Configuration["ASPNETCORE_HTTPS_PORTS"] is not null || !app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseCors(CorsExtensions.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

// Unauthenticated so a load balancer can poll it. Reports only liveness — deliberately not
// the database state, which would let anyone probe infrastructure health.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .ExcludeFromDescription();

await app.RunAsync();

/// <summary>
/// Exposed so the integration tests can drive this host through WebApplicationFactory, which
/// needs a nameable entry-point type. Top-level statements generate an internal one.
/// </summary>
public partial class Program;
