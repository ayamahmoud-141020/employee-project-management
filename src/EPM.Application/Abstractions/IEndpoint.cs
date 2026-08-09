using Microsoft.AspNetCore.Routing;

namespace EPM.Application.Abstractions;

/// <summary>
/// One HTTP route, declared next to the handler it calls.
/// </summary>
/// <remarks>
/// This is the piece that replaces controllers. Every slice ships a small class implementing
/// this, and startup scans the assembly and calls each one — so adding a feature never means
/// editing a shared controller file, and deleting a feature never leaves an orphan route.
/// </remarks>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
