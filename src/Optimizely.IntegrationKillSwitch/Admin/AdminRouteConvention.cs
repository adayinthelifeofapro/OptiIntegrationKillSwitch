namespace Optimizely.IntegrationKillSwitch.Admin;

using Microsoft.AspNetCore.Mvc.ApplicationModels;

/// <summary>
/// Gives the admin controller its route at startup, so the path can follow the site's protected module root and options.
/// </summary>
internal sealed class AdminRouteConvention(string routeTemplate) : IApplicationModelConvention
{
    public string RouteTemplate { get; } = routeTemplate;

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            Apply(controller);
        }
    }

    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType != typeof(IntegrationKillSwitchController))
        {
            return;
        }

        foreach (var selector in controller.Selectors)
        {
            selector.AttributeRouteModel = new AttributeRouteModel { Template = RouteTemplate };
        }
    }

    /// <summary>Joins the protected root ("~/EPiServer") and admin path into a route template ("EPiServer/integration-kill-switches").</summary>
    public static string BuildTemplate(string? protectedRoot, string adminPath)
    {
        var root = (protectedRoot ?? "/EPiServer").Trim().TrimStart('~').Trim('/');
        var path = adminPath.Trim().Trim('/');

        return string.IsNullOrEmpty(root) ? path : $"{root}/{path}";
    }
}
