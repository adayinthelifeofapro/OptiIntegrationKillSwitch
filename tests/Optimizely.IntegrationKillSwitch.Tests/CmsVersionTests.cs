namespace Optimizely.IntegrationKillSwitch.Tests;

using EPiServer.Shell.Navigation;

using Optimizely.IntegrationKillSwitch.Admin;

[TestFixture]
public class CmsVersionTests
{
    [Test]
    public void BuildTargetsTheReferencedCmsMajorVersion()
    {
        var shellMajor = typeof(MenuProviderAttribute).Assembly.GetName().Version!.Major;

        Assert.That(AdminLayout.CmsMajorVersion, Is.EqualTo(shellMajor));
    }

    [Test]
    public void AdminLayoutMatchesTheCmsVersion()
    {
        Assert.That(AdminLayout.Path, Does.EndWith($".Cms{AdminLayout.CmsMajorVersion}.cshtml"));
    }
}
