namespace Optimizely.IntegrationKillSwitch.Tests;

using EPiServer.Framework.Cache;
using EPiServer.Shell.Modules;
using EPiServer.Shell.Navigation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Optimizely.IntegrationKillSwitch.Admin;
using Optimizely.IntegrationKillSwitch.Configuration;
using Optimizely.IntegrationKillSwitch.Models;
using Optimizely.IntegrationKillSwitch.TagHelpers;

using System.Reflection;
using System.Security.Claims;

[TestFixture]
public class AdminAndRegistrationTests
{
    private Mock<IIntegrationStatusService> _status;
    private IntegrationKillSwitchController _controller;

    [SetUp]
    public void SetUp()
    {
        _status = new Mock<IIntegrationStatusService>();
        _status.Setup(s => s.GetStates()).Returns(
        [
            new IntegrationState
            {
                Descriptor = new IntegrationDescriptor { Name = "Turnstile", DisplayName = "Turnstile" },
                EnabledInConfig = true
            }
        ]);
        _status.Setup(s => s.GetAudit(It.IsAny<int>())).Returns([]);

        _controller = new IntegrationKillSwitchController(_status.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin@example.com")], "test"))
                }
            }
        };
    }

    [TearDown]
    public void TearDown()
    {
        _controller.Dispose();
    }

    [Test]
    public void PostSwitchesIntegrationOffWithUserAndTrimmedReason()
    {
        var model = (_controller.Index("Turnstile", true, "  incident 42 ") as ViewResult)!.Model as IntegrationKillSwitchViewModel;

        Assert.Multiple(() =>
        {
            _status.Verify(s => s.SetRuntimeState("Turnstile", true, "incident 42", "admin@example.com"), Times.Once);
            Assert.That(model!.IsError, Is.False);
        });
    }

    [Test]
    public void PostRequiresReasonToSwitchOff()
    {
        var model = (_controller.Index("Turnstile", true, " ") as ViewResult)!.Model as IntegrationKillSwitchViewModel;

        Assert.Multiple(() =>
        {
            _status.Verify(s => s.SetRuntimeState(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.That(model!.IsError, Is.True);
        });
    }

    [Test]
    public void PostRejectsUnknownIntegration()
    {
        var model = (_controller.Index("Unknown", true, "reason") as ViewResult)!.Model as IntegrationKillSwitchViewModel;

        Assert.Multiple(() =>
        {
            _status.Verify(s => s.SetRuntimeState(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.That(model!.IsError, Is.True);
        });
    }

    [Test]
    public void PostAllowsClearingWithoutReason()
    {
        _controller.Index("Turnstile", false, null);

        _status.Verify(s => s.SetRuntimeState("Turnstile", false, null, "admin@example.com"), Times.Once);
    }

    [Test]
    public void ControllerRequiresAdminPolicy()
    {
        var attribute = typeof(IntegrationKillSwitchController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(attribute!.Policy, Is.EqualTo(IntegrationKillSwitchOptions.AdminPolicyName));
    }

    [TestCase("~/EPiServer", "integration-kill-switches", "EPiServer/integration-kill-switches")]
    [TestCase("~/website-cms-edit/", "/kill-switches/", "website-cms-edit/kill-switches")]
    [TestCase(null, "x", "EPiServer/x")]
    public void BuildTemplateJoinsRootAndPath(string? root, string path, string expected)
    {
        Assert.That(AdminRouteConvention.BuildTemplate(root, path), Is.EqualTo(expected));
    }

    [Test]
    public void RouteConventionOnlyAppliesToTheAdminController()
    {
        var convention = new AdminRouteConvention("cms/kill");

        var admin = new ControllerModel(typeof(IntegrationKillSwitchController).GetTypeInfo(), []) { Selectors = { new SelectorModel() } };
        var other = new ControllerModel(typeof(Controller).GetTypeInfo(), []) { Selectors = { new SelectorModel() } };

        convention.Apply(admin);
        convention.Apply(other);

        Assert.Multiple(() =>
        {
            Assert.That(admin.Selectors[0].AttributeRouteModel!.Template, Is.EqualTo("cms/kill"));
            Assert.That(other.Selectors[0].AttributeRouteModel, Is.Null);
        });
    }

    [Test]
    public void MenuItemPointsAtAdminRoute()
    {
        var provider = new IntegrationKillSwitchMenuProvider(
            Options.Create(new IntegrationKillSwitchOptions { AdminPath = "kill" }),
            Options.Create(new ProtectedModuleOptions { RootPath = "~/cms" }));

        var item = provider.GetMenuItems().Single();

        Assert.That(item.Url, Is.EqualTo("/cms/kill"));
    }

    [Test]
    public void MenuItemCanBeHidden()
    {
        var provider = new IntegrationKillSwitchMenuProvider(
            Options.Create(new IntegrationKillSwitchOptions { ShowInMenu = false }),
            Options.Create(new ProtectedModuleOptions()));

        Assert.That(provider.GetMenuItems(), Is.Empty);
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public void MenuItemFollowsAdminPolicy(bool authenticated, bool policySucceeds, bool expected)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), IntegrationKillSwitchOptions.AdminPolicyName))
            .ReturnsAsync(policySucceeds ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var services = new ServiceCollection().AddSingleton(authorization.Object).BuildServiceProvider();
        var identity = authenticated ? new ClaimsIdentity([], "test") : new ClaimsIdentity();
        var context = new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(identity) };

        Assert.That(IntegrationKillSwitchMenuProvider.IsAuthorized(context), Is.EqualTo(expected));
    }

    [TestCase(true, "<script></script>")]
    [TestCase(false, "")]
    public void TagHelperRendersContentOnlyWhenActive(bool active, string expected)
    {
        _status.Setup(s => s.IsActive("GoogleTagManager")).Returns(active);
        var tagHelper = new IntegrationTagHelper(_status.Object) { Name = "GoogleTagManager" };

        var context = new TagHelperContext([], new Dictionary<object, object>(), "id");
        var output = new TagHelperOutput("integration", [], (_, _) =>
            Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        output.Content.SetHtmlContent("<script></script>");

        tagHelper.Process(context, output);

        using var writer = new StringWriter();
        output.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);

        Assert.That(writer.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void OptionsAddReplacesExistingNameCaseInsensitively()
    {
        var options = new IntegrationKillSwitchOptions()
            .Add(new IntegrationDescriptor { Name = "Vendor", DisplayName = "First" })
            .Add(new IntegrationDescriptor { Name = "vendor", DisplayName = "Second" });

        Assert.That(options.Integrations.Single().DisplayName, Is.EqualTo("Second"));
    }

    [Test]
    public void OptionsAddKnownAddOnsRegistersPresets()
    {
        var options = new IntegrationKillSwitchOptions().AddKnownAddOns();

        Assert.That(options.Integrations, Has.Count.EqualTo(KnownAddOns.All.Count));
    }

    [Test]
    public void AddIntegrationKillSwitchesRegistersServicesPolicyAndRoute()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Mock.Of<ISynchronizedObjectInstanceCache>());
        services.Configure<ProtectedModuleOptions>(o => o.RootPath = "~/cms");

        services.AddIntegrationKillSwitches(o =>
        {
            o.AdminPath = "kill";
            o.ConfigureAdminPolicy = policy => policy.RequireRole("Custom");
        });

        using var provider = services.BuildServiceProvider();

        var policy = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value.GetPolicy(IntegrationKillSwitchOptions.AdminPolicyName);
        var convention = provider.GetRequiredService<IOptions<MvcOptions>>().Value.Conventions.OfType<AdminRouteConvention>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(policy, Is.Not.Null);
            Assert.That(policy!.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>()
                .Single().AllowedRoles, Is.EquivalentTo(new[] { "Custom" }));
            Assert.That(convention.RouteTemplate, Is.EqualTo("cms/kill"));
            Assert.That(services.Any(s => s.ServiceType == typeof(IIntegrationStatusService)), Is.True);
        });
    }

    [TestCase(null, true)]
    [TestCase("false", false)]
    public void IsIntegrationEnabledExtensionReadsConfig(string? value, bool expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(value is null ? [] : new Dictionary<string, string?> { ["Integrations:Vendor:Enabled"] = value })
            .Build();

        Assert.That(config.IsIntegrationEnabled("Vendor"), Is.EqualTo(expected));
    }
}
