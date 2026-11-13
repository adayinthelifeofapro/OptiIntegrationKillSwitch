namespace Optimizely.IntegrationKillSwitch.Tests;

using Microsoft.Extensions.Configuration;

using Optimizely.IntegrationKillSwitch.Configuration;
using Optimizely.IntegrationKillSwitch.Models;

[TestFixture]
public class IntegrationConfigReaderTests
{
    private static readonly IntegrationDescriptor Gtm = new()
    {
        Name = "GoogleTagManager",
        DisplayName = "GTM",
        EnabledByDefault = false,
        LegacyEnabledConfigKey = "GTMEnabled"
    };

    private static readonly IntegrationDescriptor Calendar = new()
    {
        Name = "CalendarApi",
        DisplayName = "Calendar",
        HostConfigKeys = ["CalendarServiceUrl"]
    };

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public void IsEnabledDefaultsToDescriptorDefault()
    {
        var config = Config([]);

        Assert.Multiple(() =>
        {
            Assert.That(IntegrationConfigReader.IsEnabled(config, Calendar), Is.True);
            Assert.That(IntegrationConfigReader.IsEnabled(config, Gtm), Is.False);
        });
    }

    [Test]
    public void IsEnabledReadsExplicitFlag()
    {
        var config = Config(new() { ["Integrations:CalendarApi:Enabled"] = "false" });

        Assert.That(IntegrationConfigReader.IsEnabled(config, Calendar), Is.False);
    }

    [Test]
    public void IsEnabledUsesLegacyKeyWhenNoExplicitFlag()
    {
        var config = Config(new() { ["GTMEnabled"] = "true" });

        Assert.That(IntegrationConfigReader.IsEnabled(config, Gtm), Is.True);
    }

    [Test]
    public void IsEnabledPrefersExplicitFlagOverLegacyKey()
    {
        var config = Config(new() { ["GTMEnabled"] = "true", ["Integrations:GoogleTagManager:Enabled"] = "false" });

        Assert.That(IntegrationConfigReader.IsEnabled(config, Gtm), Is.False);
    }

    [Test]
    public void IsEnabledHonoursCustomSectionName()
    {
        var config = Config(new() { ["KillSwitches:CalendarApi:Enabled"] = "false" });

        Assert.That(IntegrationConfigReader.IsEnabled(config, Calendar, "KillSwitches"), Is.False);
    }

    [Test]
    public void BuildResolvesHostsFromConfigUrls()
    {
        var config = Config(new() { ["CalendarServiceUrl"] = "https://Calendar.Example.com/api" });

        var calendar = IntegrationConfigReader.Build(config, [Calendar], "~/EPiServer").Single();

        Assert.That(calendar.BlockedHosts, Is.EquivalentTo(new[] { "calendar.example.com" }));
    }

    [Test]
    public void BuildIgnoresConfigUrlsThatAreNotAbsolute()
    {
        var config = Config(new() { ["CalendarServiceUrl"] = "not-a-url" });

        var calendar = IntegrationConfigReader.Build(config, [Calendar], "~/EPiServer").Single();

        Assert.That(calendar.BlockedHosts, Is.Empty);
    }

    [TestCase("~/EPiServer", "/EPiServer/Geta.Optimizely.Sitemaps")]
    [TestCase("~/website-cms-edit/", "/website-cms-edit/Geta.Optimizely.Sitemaps")]
    [TestCase("/cms", "/cms/Geta.Optimizely.Sitemaps")]
    public void BuildReplacesProtectedRootToken(string root, string expected)
    {
        var sitemaps = IntegrationConfigReader.Build(Config([]), [KnownAddOns.GetaSitemaps], root).Single();

        Assert.That(sitemaps.BlockedPaths, Does.Contain(expected));
    }

    [Test]
    public void BuildAppendsBlockedPathsAndHostsFromConfig()
    {
        var config = Config(new()
        {
            ["Integrations:GetaSitemaps:BlockedPaths:0"] = "sitemap-news.xml",
            ["Integrations:GetaSitemaps:BlockedPaths:1"] = "{ProtectedRoot}/Extra",
            ["Integrations:GetaSitemaps:BlockedHosts:0"] = "Extra.Example.com"
        });

        var sitemaps = IntegrationConfigReader.Build(config, [KnownAddOns.GetaSitemaps], "~/EPiServer").Single();

        Assert.Multiple(() =>
        {
            Assert.That(sitemaps.BlockedPaths, Does.Contain("/sitemap-news.xml"));
            Assert.That(sitemaps.BlockedPaths, Does.Contain("/sitemap.xml"));
            Assert.That(sitemaps.BlockedPaths, Does.Contain("/EPiServer/Extra"));
            Assert.That(sitemaps.BlockedHosts, Does.Contain("extra.example.com"));
        });
    }

    [Test]
    public void BuildAddsIntegrationsDefinedOnlyInConfig()
    {
        var config = Config(new()
        {
            ["Integrations:Vendor:DisplayName"] = "Some vendor",
            ["Integrations:Vendor:BlockedPaths:0"] = "/vendor-api",
            ["Integrations:Vendor:BlockedHosts:0"] = "api.vendor.example"
        });

        var vendor = IntegrationConfigReader.Build(config, [], "~/EPiServer").Single();

        Assert.Multiple(() =>
        {
            Assert.That(vendor.Name, Is.EqualTo("Vendor"));
            Assert.That(vendor.DisplayName, Is.EqualTo("Some vendor"));
            Assert.That(vendor.BlockedPaths, Is.EquivalentTo(new[] { "/vendor-api" }));
            Assert.That(vendor.BlockedHosts, Is.EquivalentTo(new[] { "api.vendor.example" }));
            Assert.That(IntegrationConfigReader.IsEnabled(config, vendor), Is.True);
        });
    }

    [Test]
    public void ConfigSectionForRegisteredIntegrationDoesNotDuplicateIt()
    {
        var config = Config(new() { ["Integrations:CalendarApi:Enabled"] = "false" });

        var descriptors = IntegrationConfigReader.Build(config, [Calendar], "~/EPiServer");

        Assert.That(descriptors, Has.Count.EqualTo(1));
    }

    [Test]
    public void KnownAddOnsHaveUniqueNames()
    {
        Assert.That(KnownAddOns.All.Select(d => d.Name), Is.Unique);
    }
}
