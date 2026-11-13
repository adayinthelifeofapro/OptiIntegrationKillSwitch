namespace Optimizely.IntegrationKillSwitch.Enforcement;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Optimizely.IntegrationKillSwitch.Models;

/// <summary>
/// Returns 404 for inbound requests to the paths of any integration that is switched off,
/// such as an add-on's protected module UI, API endpoints or public routes.
/// </summary>
internal sealed class IntegrationKillSwitchMiddleware(RequestDelegate next, ILogger<IntegrationKillSwitchMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IIntegrationStatusService integrationStatus)
    {
        var blocked = FindBlockingIntegration(context.Request.Path, integrationStatus);

        if (blocked is not null)
        {
            logger.LogWarning(
                "Blocked request to {Path} because integration {Integration} is switched off.",
                context.Request.Path.Value,
                blocked.Name);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    }

    public static IntegrationDescriptor? FindBlockingIntegration(PathString path, IIntegrationStatusService integrationStatus)
    {
        if (!path.HasValue)
        {
            return null;
        }

        foreach (var descriptor in integrationStatus.GetInactive())
        {
            foreach (var prefix in descriptor.BlockedPaths)
            {
                if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return descriptor;
                }
            }
        }

        return null;
    }
}
