namespace Optimizely.IntegrationKillSwitch.Configuration;

using Microsoft.AspNetCore.Authorization;

using Optimizely.IntegrationKillSwitch.Models;

public sealed class IntegrationKillSwitchOptions
{
    /// <summary>The name of the authorization policy that protects the admin page.</summary>
    public const string AdminPolicyName = "Optimizely.IntegrationKillSwitch.Admin";

    private readonly List<IntegrationDescriptor> _integrations = [];

    /// <summary>The integrations registered in code. More can be defined in the "Integrations" config section.</summary>
    public IReadOnlyList<IntegrationDescriptor> Integrations => _integrations;

    /// <summary>The config section holding the flags and any config-only integrations. Default "Integrations".</summary>
    public string ConfigSectionName { get; set; } = "Integrations";

    /// <summary>
    /// The admin page route, relative to the protected module root. Default "integration-kill-switches",
    /// which gives e.g. /EPiServer/integration-kill-switches.
    /// </summary>
    public string AdminPath { get; set; } = "integration-kill-switches";

    /// <summary>Shows the admin page in the CMS menu. Default true.</summary>
    public bool ShowInMenu { get; set; } = true;

    /// <summary>
    /// The Dynamic Data Store name for the switches. The audit store uses this name plus "Audit".
    /// Change it only to keep the data of an existing store.
    /// </summary>
    public string StoreName { get; set; } = "IntegrationKillSwitch";

    /// <summary>
    /// How long switch state is cached. A change clears the cache on every instance at once,
    /// so this is only a safety net in case a cache event is lost.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Builds the admin policy. By default the user must be in the CmsAdmins or WebAdmins role.
    /// </summary>
    public Action<AuthorizationPolicyBuilder> ConfigureAdminPolicy { get; set; } =
        policy => policy.RequireRole("CmsAdmins", "WebAdmins");

    /// <summary>Registers an integration. A later registration with the same name replaces the earlier one.</summary>
    public IntegrationKillSwitchOptions Add(IntegrationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _integrations.RemoveAll(d => string.Equals(d.Name, descriptor.Name, StringComparison.OrdinalIgnoreCase));
        _integrations.Add(descriptor);

        return this;
    }

    /// <summary>Registers the presets in <see cref="KnownAddOns"/>.</summary>
    public IntegrationKillSwitchOptions AddKnownAddOns()
    {
        foreach (var descriptor in KnownAddOns.All)
        {
            Add(descriptor);
        }

        return this;
    }
}
