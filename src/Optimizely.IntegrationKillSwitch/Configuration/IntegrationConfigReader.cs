namespace Optimizely.IntegrationKillSwitch.Configuration;

using Microsoft.Extensions.Configuration;

using Optimizely.IntegrationKillSwitch.Models;

/// <summary>
/// Reads integration flags and extra settings from config.
/// </summary>
/// <remarks>
/// Example:
/// <code>
/// "Integrations": {
///   "GetaSitemaps": { "Enabled": false, "BlockedPaths": [ "/sitemap-news.xml" ] },
///   "Siteimprove": { "DisplayName": "Siteimprove", "BlockedHosts": [ "example-vendor.com" ] }
/// }
/// </code>
/// A section for a name that is not registered in code defines a new integration, with no code change.
/// </remarks>
public static class IntegrationConfigReader
{
    public const string DefaultSectionName = "Integrations";

    /// <summary>
    /// Reads whether an integration is on, in this order: {section}:{Name}:Enabled, then the legacy key, then the default.
    /// </summary>
    public static bool IsEnabled(
        IConfiguration configuration,
        IntegrationDescriptor descriptor,
        string sectionName = DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (bool.TryParse(configuration[$"{sectionName}:{descriptor.Name}:Enabled"], out var enabled))
        {
            return enabled;
        }

        if (descriptor.LegacyEnabledConfigKey is not null
            && bool.TryParse(configuration[descriptor.LegacyEnabledConfigKey], out var legacyEnabled))
        {
            return legacyEnabled;
        }

        return descriptor.EnabledByDefault;
    }

    /// <summary>
    /// Merges the registered descriptors with the config section and resolves hosts from config URLs and the protected root token.
    /// </summary>
    public static IReadOnlyList<IntegrationDescriptor> Build(
        IConfiguration configuration,
        IEnumerable<IntegrationDescriptor> registered,
        string protectedRoot,
        string sectionName = DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(registered);

        var section = configuration.GetSection(sectionName);

        var descriptors = new Dictionary<string, IntegrationDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in registered)
        {
            descriptors[descriptor.Name] = descriptor;
        }

        foreach (var child in section.GetChildren())
        {
            if (!descriptors.ContainsKey(child.Key))
            {
                descriptors[child.Key] = new IntegrationDescriptor
                {
                    Name = child.Key,
                    DisplayName = child["DisplayName"] ?? child.Key,
                    Description = child["Description"] ?? "Defined in config."
                };
            }
        }

        var root = NormaliseProtectedRoot(protectedRoot);

        return descriptors.Values
            .Select(d => Resolve(d, configuration, section.GetSection(d.Name), root))
            .OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Turns "~/EPiServer/" into "/EPiServer".</summary>
    public static string NormaliseProtectedRoot(string? protectedRoot)
    {
        var root = (protectedRoot ?? "/EPiServer").Trim().TrimStart('~').TrimEnd('/');

        return root.StartsWith('/') ? root : "/" + root;
    }

    private static IntegrationDescriptor Resolve(
        IntegrationDescriptor descriptor,
        IConfiguration configuration,
        IConfigurationSection section,
        string protectedRoot)
    {
        var configHosts = descriptor.HostConfigKeys
            .Select(key => configuration[key])
            .Select(TryGetHost)
            .OfType<string>();

        var hosts = descriptor.BlockedHosts
            .Concat(configHosts)
            .Concat(ReadArray(section.GetSection("BlockedHosts")))
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var paths = descriptor.BlockedPaths
            .Concat(ReadArray(section.GetSection("BlockedPaths")))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().Replace(IntegrationDescriptor.ProtectedRootToken, protectedRoot, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.StartsWith('/') ? p : "/" + p)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return descriptor with { BlockedHosts = hosts, BlockedPaths = paths };
    }

    private static IEnumerable<string> ReadArray(IConfigurationSection section)
    {
        return section.GetChildren().Select(c => c.Value).OfType<string>();
    }

    private static string? TryGetHost(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
    }
}
