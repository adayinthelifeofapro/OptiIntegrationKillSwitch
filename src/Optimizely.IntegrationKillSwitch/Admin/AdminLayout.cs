namespace Optimizely.IntegrationKillSwitch.Admin;

/// <summary>
/// The admin page layout for the CMS version this build targets.
/// CMS 12 draws the navigation bar with HTML helpers. CMS 13 uses the platform-navigation tag helpers.
/// </summary>
public static class AdminLayout
{
#if CMS13
    public const string Path = "~/Views/Shared/_IntegrationKillSwitchLayout.Cms13.cshtml";

    public const int CmsMajorVersion = 13;
#else
    public const string Path = "~/Views/Shared/_IntegrationKillSwitchLayout.Cms12.cshtml";

    public const int CmsMajorVersion = 12;
#endif
}
