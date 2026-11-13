namespace Optimizely.IntegrationKillSwitch.Tests;

using EPiServer.Framework.Cache;
using EPiServer.Shell.Modules;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Optimizely.IntegrationKillSwitch.Configuration;
using Optimizely.IntegrationKillSwitch.Models;
using Optimizely.IntegrationKillSwitch.Storage;

[TestFixture]
public class IntegrationStatusServiceTests
{
    private const string Calendar = "CalendarApi";
    private const string Committees = "CommitteesApi";
    private const string Turnstile = "Turnstile";

    private Dictionary<string, object> _cacheEntries;
    private Mock<ISynchronizedObjectInstanceCache> _cache;
    private List<IntegrationKillSwitchRecord> _records;
    private Mock<IIntegrationKillSwitchRepository> _repository;
    private IntegrationKillSwitchOptions _options;

    [SetUp]
    public void SetUp()
    {
        _cacheEntries = [];
        _cache = new Mock<ISynchronizedObjectInstanceCache>();
        _cache.Setup(c => c.Get(It.IsAny<string>())).Returns((string key) => _cacheEntries.GetValueOrDefault(key));
        _cache.Setup(c => c.Insert(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CacheEvictionPolicy>()))
            .Callback((string key, object value, CacheEvictionPolicy _) => _cacheEntries[key] = value);
        _cache.Setup(c => c.Remove(It.IsAny<string>())).Callback((string key) => _cacheEntries.Remove(key));

        _records = [];
        _repository = new Mock<IIntegrationKillSwitchRepository>();
        _repository.Setup(r => r.GetAll()).Returns(() => _records.ToList());
        _repository.Setup(r => r.Save(It.IsAny<IntegrationKillSwitchRecord>()))
            .Callback((IntegrationKillSwitchRecord record) =>
            {
                _records.RemoveAll(r => r.Name == record.Name);
                _records.Add(record);
            });
        _repository.Setup(r => r.GetAudit(It.IsAny<int>())).Returns([]);

        _options = new IntegrationKillSwitchOptions()
            .Add(new IntegrationDescriptor { Name = Calendar, DisplayName = "Calendar" })
            .Add(new IntegrationDescriptor { Name = Committees, DisplayName = "Committees" })
            .Add(new IntegrationDescriptor { Name = Turnstile, DisplayName = "Turnstile" });
    }

    private IntegrationStatusService CreateService(Dictionary<string, string?>? config = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build();

        return new IntegrationStatusService(
            configuration,
            Options.Create(_options),
            Options.Create(new ProtectedModuleOptions { RootPath = "~/EPiServer" }),
            _repository.Object,
            _cache.Object,
            NullLogger<IntegrationStatusService>.Instance);
    }

    [Test]
    public void IsActiveIsTrueByDefault()
    {
        Assert.That(CreateService().IsActive(Calendar), Is.True);
    }

    [Test]
    public void IsActiveIsTrueForUnknownIntegrations()
    {
        Assert.That(CreateService().IsActive("NotARealIntegration"), Is.True);
    }

    [Test]
    public void IsActiveIsFalseWhenOffInConfig()
    {
        var service = CreateService(new() { ["Integrations:CalendarApi:Enabled"] = "false" });

        Assert.That(service.IsActive(Calendar), Is.False);
    }

    [Test]
    public void IsActiveUsesCustomConfigSection()
    {
        _options.ConfigSectionName = "KillSwitches";

        var service = CreateService(new() { ["KillSwitches:CalendarApi:Enabled"] = "false" });

        Assert.That(service.IsActive(Calendar), Is.False);
    }

    [Test]
    public void IsActiveIsFalseWhenSwitchedOffAtRuntime()
    {
        _records.Add(new IntegrationKillSwitchRecord { Name = Calendar, Disabled = true });

        Assert.That(CreateService().IsActive(Calendar), Is.False);
    }

    [Test]
    public void ConfigOnlyIntegrationCanBeSwitchedOffAtRuntime()
    {
        var service = CreateService(new() { ["Integrations:Vendor:BlockedHosts:0"] = "vendor.example" });

        service.SetRuntimeState("Vendor", true, "incident", "admin");

        Assert.That(service.IsActive("Vendor"), Is.False);
    }

    [Test]
    public void ClearingRuntimeSwitchDoesNotOverrideConfig()
    {
        var service = CreateService(new() { ["Integrations:CalendarApi:Enabled"] = "false" });

        service.SetRuntimeState(Calendar, false, null, "admin");

        Assert.That(service.IsActive(Calendar), Is.False);
    }

    [Test]
    public void SetRuntimeStateTakesEffectImmediatelyAndClearsTheCache()
    {
        var service = CreateService();

        Assert.That(service.IsActive(Calendar), Is.True);

        service.SetRuntimeState(Calendar, true, "compromised", "admin");

        Assert.Multiple(() =>
        {
            Assert.That(service.IsActive(Calendar), Is.False);
            _cache.Verify(c => c.Remove(IntegrationStatusService.CacheKey), Times.Once);
            _repository.Verify(r => r.AddAudit(It.Is<IntegrationKillSwitchAuditRecord>(a =>
                a.Name == Calendar && a.Disabled && a.Reason == "compromised" && a.ChangedBy == "admin")), Times.Once);
        });
    }

    [Test]
    public void SetRuntimeStateNormalisesNameCase()
    {
        CreateService().SetRuntimeState("calendarapi", true, "reason", "admin");

        Assert.That(_records.Single().Name, Is.EqualTo(Calendar));
    }

    [Test]
    public void SetRuntimeStateThrowsForUnknownIntegration()
    {
        Assert.Throws<ArgumentException>(() => CreateService().SetRuntimeState("Unknown", true, "reason", "admin"));
    }

    [Test]
    public void RuntimeRecordsAreReadOnceThenCachedForConfiguredDuration()
    {
        _options.CacheDuration = TimeSpan.FromSeconds(30);
        var service = CreateService();

        service.IsActive(Calendar);
        service.IsActive(Committees);
        service.GetStates();

        Assert.Multiple(() =>
        {
            _repository.Verify(r => r.GetAll(), Times.Once);
            _cache.Verify(c => c.Insert(
                IntegrationStatusService.CacheKey,
                It.IsAny<object>(),
                It.Is<CacheEvictionPolicy>(p => p.Expiration == TimeSpan.FromSeconds(30))), Times.Once);
        });
    }

    [Test]
    public void StoreFailureFallsBackToConfigOnly()
    {
        _repository.Setup(r => r.GetAll()).Throws(new InvalidOperationException("db down"));

        var service = CreateService(new() { ["Integrations:CommitteesApi:Enabled"] = "false" });

        Assert.Multiple(() =>
        {
            Assert.That(service.IsActive(Calendar), Is.True);
            Assert.That(service.IsActive(Committees), Is.False);
        });
    }

    [Test]
    public void GetInactiveReturnsOnlySwitchedOffIntegrations()
    {
        _records.Add(new IntegrationKillSwitchRecord { Name = Turnstile, Disabled = true });

        var service = CreateService(new() { ["Integrations:CalendarApi:Enabled"] = "false" });

        Assert.That(service.GetInactive().Select(d => d.Name), Is.EquivalentTo(new[] { Calendar, Turnstile }));
    }

    [Test]
    public void GetStatesIncludesRuntimeDetails()
    {
        var changed = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        _records.Add(new IntegrationKillSwitchRecord
        {
            Name = Turnstile,
            Disabled = true,
            Reason = "incident 42",
            ChangedBy = "admin",
            ChangedUtc = changed
        });

        var state = CreateService().GetStates().Single(s => s.Descriptor.Name == Turnstile);

        Assert.Multiple(() =>
        {
            Assert.That(state.EnabledInConfig, Is.True);
            Assert.That(state.DisabledAtRuntime, Is.True);
            Assert.That(state.IsActive, Is.False);
            Assert.That(state.Reason, Is.EqualTo("incident 42"));
            Assert.That(state.ChangedBy, Is.EqualTo("admin"));
            Assert.That(state.ChangedUtc, Is.EqualTo(changed));
        });
    }
}
