namespace Optimizely.IntegrationKillSwitch.Configuration;

using Optimizely.IntegrationKillSwitch.Models;

/// <summary>
/// Presets for common Optimizely add-ons. Each one blocks the add-on's protected module UI.
/// To remove an add-on completely, also wrap its Add*/Use* calls with IsIntegrationEnabled in Startup.
/// </summary>
/// <remarks>
/// The paths assume each add-on uses its assembly name as its protected module name.
/// Check them against your site and add more in config if needed.
/// </remarks>
public static class KnownAddOns
{
    private const string Root = IntegrationDescriptor.ProtectedRootToken;

    public static IntegrationDescriptor GetaNotFoundHandler { get; } = new()
    {
        Name = "GetaNotFoundHandler",
        DisplayName = "Geta NotFoundHandler",
        Description = "Redirect manager add-on.",
        BlockedPaths = [$"{Root}/Geta.NotFoundHandler.Optimizely"]
    };

    public static IntegrationDescriptor GetaSitemaps { get; } = new()
    {
        Name = "GetaSitemaps",
        DisplayName = "Geta Sitemaps",
        Description = "XML sitemap add-on. Blocks its admin UI and /sitemap.xml.",
        BlockedPaths = [$"{Root}/Geta.Optimizely.Sitemaps", "/sitemap.xml"]
    };

    public static IntegrationDescriptor GetaCategories { get; } = new()
    {
        Name = "GetaCategories",
        DisplayName = "Geta Categories",
        Description = "Category add-on. Content that uses its properties may not edit correctly while it is off.",
        BlockedPaths = [$"{Root}/Geta.Optimizely.Categories"]
    };

    public static IntegrationDescriptor GetaContentTypeIcons { get; } = new()
    {
        Name = "GetaContentTypeIcons",
        DisplayName = "Geta ContentTypeIcons",
        Description = "Editor icon add-on. Switch it off in config to remove it at startup."
    };

    public static IntegrationDescriptor StottRobotsHandler { get; } = new()
    {
        Name = "StottRobotsHandler",
        DisplayName = "Stott RobotsHandler",
        Description = "robots.txt add-on.",
        BlockedPaths = [$"{Root}/Stott.Optimizely.RobotsHandler"]
    };

    public static IReadOnlyList<IntegrationDescriptor> All { get; } =
    [
        GetaNotFoundHandler,
        GetaSitemaps,
        GetaCategories,
        GetaContentTypeIcons,
        StottRobotsHandler
    ];
}
