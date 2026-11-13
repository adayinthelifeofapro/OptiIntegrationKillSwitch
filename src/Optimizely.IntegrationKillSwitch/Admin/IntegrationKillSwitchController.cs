namespace Optimizely.IntegrationKillSwitch.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Optimizely.IntegrationKillSwitch.Configuration;

/// <summary>
/// The admin page. Its route is set from <see cref="IntegrationKillSwitchOptions.AdminPath"/> by <see cref="AdminRouteConvention"/>.
/// </summary>
[Authorize(Policy = IntegrationKillSwitchOptions.AdminPolicyName)]
public sealed class IntegrationKillSwitchController(IIntegrationStatusService integrationStatus) : Controller
{
    private const int AuditCount = 25;

    private const string ViewPath = "~/Views/IntegrationKillSwitch/Index.cshtml";

    [HttpGet]
    public IActionResult Index()
    {
        return View(ViewPath, BuildViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(string name, bool disable, string? reason)
    {
        if (string.IsNullOrWhiteSpace(name)
            || !integrationStatus.GetStates().Any(s => string.Equals(s.Descriptor.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return View(ViewPath, BuildViewModel("Unknown integration.", isError: true));
        }

        if (disable && string.IsNullOrWhiteSpace(reason))
        {
            return View(ViewPath, BuildViewModel("Enter a reason before switching an integration off.", isError: true));
        }

        integrationStatus.SetRuntimeState(name, disable, reason?.Trim(), User.Identity?.Name);

        var message = disable
            ? $"{name} is now switched off on all instances."
            : $"{name}'s runtime switch is cleared. It is active again unless config turns it off.";

        return View(ViewPath, BuildViewModel(message));
    }

    private IntegrationKillSwitchViewModel BuildViewModel(string? message = null, bool isError = false)
    {
        return new IntegrationKillSwitchViewModel
        {
            States = integrationStatus.GetStates(),
            Audit = integrationStatus.GetAudit(AuditCount),
            Message = message,
            IsError = isError
        };
    }
}
