namespace Optimizely.IntegrationKillSwitch;

using EPiServer.Framework.Cache;
using EPiServer.Shell.Modules;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Optimizely.IntegrationKillSwitch.Configuration;
using Optimizely.IntegrationKillSwitch.Models;
using Optimizely.IntegrationKillSwitch.Storage;

/// <summary>
/// Combines the config flags with the runtime switches stored in the CMS.
/// </summary>
/// <remarks>
/// The runtime switches are held in <see cref="ISynchronizedObjectInstanceCache"/>. When a switch changes, the cache entry is removed,
/// which reaches every instance through the Optimizely event provider. The cache expiry is a safety net in case an event is lost.
/// </remarks>
internal sealed class IntegrationStatusService : IIntegrationStatusService
{
    public const string CacheKey = "Optimizely.IntegrationKillSwitch:States";

    private readonly ISynchronizedObjectInstanceCache _cache;
    private readonly TimeSpan _cacheDuration;
    private readonly IReadOnlyList<IntegrationDescriptor> _descriptors;
    private readonly IReadOnlyDictionary<string, bool> _enabledInConfig;
    private readonly ILogger<IntegrationStatusService> _logger;
    private readonly IIntegrationKillSwitchRepository _repository;

    public IntegrationStatusService(
        IConfiguration configuration,
        IOptions<IntegrationKillSwitchOptions> options,
        IOptions<ProtectedModuleOptions> protectedModuleOptions,
        IIntegrationKillSwitchRepository repository,
        ISynchronizedObjectInstanceCache cache,
        ILogger<IntegrationStatusService> logger)
    {
        _cache = cache;
        _logger = logger;
        _repository = repository;
        _cacheDuration = options.Value.CacheDuration;

        // Config is read once. Changing a startup flag needs a restart anyway, because registrations are fixed at startup.
        var sectionName = options.Value.ConfigSectionName;

        _descriptors = IntegrationConfigReader.Build(
            configuration,
            options.Value.Integrations,
            protectedModuleOptions.Value.RootPath,
            sectionName);

        _enabledInConfig = _descriptors.ToDictionary(
            d => d.Name,
            d => IntegrationConfigReader.IsEnabled(configuration, d, sectionName),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsActive(string name)
    {
        if (!_enabledInConfig.TryGetValue(name, out var enabled))
        {
            return true;
        }

        return enabled && !IsDisabledAtRuntime(name);
    }

    public IReadOnlyList<IntegrationState> GetStates()
    {
        var records = GetRuntimeRecords();

        return _descriptors.Select(d =>
        {
            records.TryGetValue(d.Name, out var record);

            return new IntegrationState
            {
                Descriptor = d,
                EnabledInConfig = _enabledInConfig[d.Name],
                DisabledAtRuntime = record?.Disabled ?? false,
                Reason = record?.Reason,
                ChangedBy = record?.ChangedBy,
                ChangedUtc = record?.ChangedUtc
            };
        }).ToList();
    }

    public IReadOnlyList<IntegrationDescriptor> GetInactive()
    {
        return _descriptors.Where(d => !IsActive(d.Name)).ToList();
    }

    public void SetRuntimeState(string name, bool disabled, string? reason, string? changedBy)
    {
        var descriptor = _descriptors.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unknown integration '{name}'.", nameof(name));

        var now = DateTime.UtcNow;

        _repository.Save(new IntegrationKillSwitchRecord
        {
            Name = descriptor.Name,
            Disabled = disabled,
            Reason = reason,
            ChangedBy = changedBy,
            ChangedUtc = now
        });

        _repository.AddAudit(new IntegrationKillSwitchAuditRecord
        {
            Name = descriptor.Name,
            Disabled = disabled,
            Reason = reason,
            ChangedBy = changedBy,
            ChangedUtc = now
        });

        // Removing the entry clears it on every instance.
        _cache.Remove(CacheKey);

        _logger.LogWarning(
            "Integration kill switch changed: {Integration} is now {State} at runtime by {User}. Reason: {Reason}",
            descriptor.Name,
            disabled ? "DISABLED" : "ENABLED",
            changedBy ?? "unknown",
            reason ?? "(none)");
    }

    public IReadOnlyList<IntegrationAuditEntry> GetAudit(int count)
    {
        return _repository.GetAudit(count)
            .Select(a => new IntegrationAuditEntry(a.Name, a.Disabled, a.Reason, a.ChangedBy, a.ChangedUtc))
            .ToList();
    }

    private bool IsDisabledAtRuntime(string name)
    {
        return GetRuntimeRecords().TryGetValue(name, out var record) && record.Disabled;
    }

    private IReadOnlyDictionary<string, IntegrationKillSwitchRecord> GetRuntimeRecords()
    {
        if (_cache.Get(CacheKey) is IReadOnlyDictionary<string, IntegrationKillSwitchRecord> cached)
        {
            return cached;
        }

        IReadOnlyDictionary<string, IntegrationKillSwitchRecord> records;

        try
        {
            records = _repository.GetAll()
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ChangedUtc).First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            // If the store cannot be read, fall back to config only rather than taking the site down.
            // The result is not cached, so the next request tries the store again.
            _logger.LogError(ex, "Could not read the integration kill switch store. Using config flags only.");
            return new Dictionary<string, IntegrationKillSwitchRecord>();
        }

        _cache.Insert(CacheKey, records, new CacheEvictionPolicy(_cacheDuration, CacheTimeoutType.Absolute));

        return records;
    }
}
