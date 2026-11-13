namespace Optimizely.IntegrationKillSwitch.Tests;

using Optimizely.IntegrationKillSwitch.Enforcement;
using Optimizely.IntegrationKillSwitch.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using System.Net;

[TestFixture]
public class IntegrationKillSwitchEnforcementTests
{
    private static readonly IntegrationDescriptor Sitemaps = new()
    {
        Name = "GetaSitemaps",
        DisplayName = "Sitemaps",
        BlockedPaths = ["/website-cms-edit/Geta.Optimizely.Sitemaps", "/sitemap.xml"]
    };

    private static readonly IntegrationDescriptor Calendar = new()
    {
        Name = "CalendarApi",
        DisplayName = "Calendar",
        BlockedHosts = ["calendar.example.com"]
    };

    private Mock<IIntegrationStatusService> _status;

    [SetUp]
    public void SetUp()
    {
        _status = new Mock<IIntegrationStatusService>();
        _status.Setup(s => s.GetInactive()).Returns([]);
    }

    [TestCase("/website-cms-edit/Geta.Optimizely.Sitemaps")]
    [TestCase("/website-cms-edit/geta.optimizely.sitemaps/admin/index")]
    [TestCase("/sitemap.xml")]
    public async Task MiddlewareReturns404ForBlockedPaths(string path)
    {
        _status.Setup(s => s.GetInactive()).Returns([Sitemaps]);
        var nextCalled = false;
        var middleware = new IntegrationKillSwitchMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<IntegrationKillSwitchMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context, _status.Object);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
            Assert.That(nextCalled, Is.False);
        });
    }

    [TestCase("/website-cms-edit/Geta.Optimizely.SitemapsExtra")]
    [TestCase("/sitemap.xml.bak")]
    [TestCase("/news")]
    public async Task MiddlewareAllowsPathsThatOnlyShareAPrefixString(string path)
    {
        _status.Setup(s => s.GetInactive()).Returns([Sitemaps]);
        var nextCalled = false;
        var middleware = new IntegrationKillSwitchMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<IntegrationKillSwitchMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        await middleware.InvokeAsync(context, _status.Object);

        Assert.That(nextCalled, Is.True);
    }

    [Test]
    public async Task MiddlewareAllowsEverythingWhenNothingIsSwitchedOff()
    {
        var nextCalled = false;
        var middleware = new IntegrationKillSwitchMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<IntegrationKillSwitchMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/sitemap.xml";

        await middleware.InvokeAsync(context, _status.Object);

        Assert.That(nextCalled, Is.True);
    }

    [TestCase("https://calendar.example.com/api/events")]
    [TestCase("https://CALENDAR.example.com/")]
    [TestCase("https://eu.calendar.example.com/")]
    public async Task HttpHandlerBlocksCallsToSwitchedOffHosts(string url)
    {
        _status.Setup(s => s.GetInactive()).Returns([Calendar]);
        var inner = new RecordingHandler();
        using var client = new HttpClient(new IntegrationKillSwitchHttpHandler(
            _status.Object,
            NullLogger<IntegrationKillSwitchHttpHandler>.Instance) { InnerHandler = inner });

        var response = await client.GetAsync(url);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(response.ReasonPhrase, Is.EqualTo(IntegrationKillSwitchHttpHandler.BlockedReasonPhrase));
            Assert.That(inner.Called, Is.False);
        });
    }

    [TestCase("https://othercalendar.example.com/")]
    [TestCase("https://committees.example.com/")]
    public async Task HttpHandlerAllowsOtherHosts(string url)
    {
        _status.Setup(s => s.GetInactive()).Returns([Calendar]);
        var inner = new RecordingHandler();
        using var client = new HttpClient(new IntegrationKillSwitchHttpHandler(
            _status.Object,
            NullLogger<IntegrationKillSwitchHttpHandler>.Instance) { InnerHandler = inner });

        var response = await client.GetAsync(url);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(inner.Called, Is.True);
        });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public bool Called { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
