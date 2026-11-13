namespace Optimizely.IntegrationKillSwitch.Models;

/// <summary>
/// Describes an integration and the surfaces a kill switch has to block.
/// </summary>
public sealed record IntegrationDescriptor
{
    /// <summary>
    /// The token in <see cref="BlockedPaths"/> that is replaced with the site's protected module root, e.g. "/EPiServer".
    /// </summary>
    public const string ProtectedRootToken = "{ProtectedRoot}";

    /// <summary>The key used in config (Integrations:{Name}:Enabled) and in the runtime store.</summary>
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// The value used when neither Integrations:{Name}:Enabled nor <see cref="LegacyEnabledConfigKey"/> is set.
    /// </summary>
    public bool EnabledByDefault { get; init; } = true;

    /// <summary>
    /// An older flat config key that also turns the integration on or off (e.g. "GTMEnabled").
    /// It is only read when Integrations:{Name}:Enabled is not set.
    /// </summary>
    public string? LegacyEnabledConfigKey { get; init; }

    /// <summary>
    /// Inbound request paths that get a 404 while the integration is switched off.
    /// May start with <see cref="ProtectedRootToken"/>.
    /// </summary>
    public IReadOnlyList<string> BlockedPaths { get; init; } = [];

    /// <summary>Outbound hosts (and their subdomains) that get no HTTP calls while the integration is switched off.</summary>
    public IReadOnlyList<string> BlockedHosts { get; init; } = [];

    /// <summary>Config keys holding URLs. The host of each URL is added to <see cref="BlockedHosts"/>.</summary>
    public IReadOnlyList<string> HostConfigKeys { get; init; } = [];
}
