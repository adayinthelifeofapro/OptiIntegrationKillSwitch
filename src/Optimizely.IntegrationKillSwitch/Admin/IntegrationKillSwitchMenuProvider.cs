namespace Optimizely.IntegrationKillSwitch.Admin;

using EPiServer.Shell.Modules;
using EPiServer.Shell.Navigation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Optimizely.IntegrationKillSwitch.Configuration;

/// <summary>
/// Adds the admin page to the CMS Settings menu for users who meet the admin policy.
/// </summary>
[MenuProvider]
public sealed class IntegrationKillSwitchMenuProvider(
    IOptions<IntegrationKillSwitchOptions> options,
    IOptions<ProtectedModuleOptions> protectedModuleOptions
) : IMenuProvider
{
    public const string MenuPath = "/global/cms/admin/integration-kill-switches";

    public IEnumerable<MenuItem> GetMenuItems()
    {
        if (!options.Value.ShowInMenu)
        {
            return [];
        }

        var url = "/" + AdminRouteConvention.BuildTemplate(protectedModuleOptions.Value.RootPath, options.Value.AdminPath);

        return
        [
            new UrlMenuItem("Integration kill switches", MenuPath, url)
            {
                IsAvailable = IsAuthorized,
                SortIndex = 900
            }
        ];
    }

    internal static bool IsAuthorized(HttpContext? context)
    {
        if (context?.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var authorization = context.RequestServices.GetService<IAuthorizationService>();

        return authorization is not null
            && authorization.AuthorizeAsync(context.User, IntegrationKillSwitchOptions.AdminPolicyName).GetAwaiter().GetResult().Succeeded;
    }
}
