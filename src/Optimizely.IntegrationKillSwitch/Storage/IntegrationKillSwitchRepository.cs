namespace Optimizely.IntegrationKillSwitch.Storage;

using EPiServer.Data;
using EPiServer.Data.Dynamic;

using Microsoft.Extensions.Options;

using Optimizely.IntegrationKillSwitch.Configuration;

using System.Diagnostics.CodeAnalysis;

public interface IIntegrationKillSwitchRepository
{
    IReadOnlyList<IntegrationKillSwitchRecord> GetAll();

    void Save(IntegrationKillSwitchRecord record);

    void AddAudit(IntegrationKillSwitchAuditRecord audit);

    IReadOnlyList<IntegrationKillSwitchAuditRecord> GetAudit(int count);
}

public sealed class IntegrationKillSwitchRecord : IDynamicData
{
    public Identity? Id { get; set; }

    [EPiServerDataIndex]
    public string Name { get; set; } = string.Empty;

    public bool Disabled { get; set; }

    public string? Reason { get; set; }

    public string? ChangedBy { get; set; }

    public DateTime ChangedUtc { get; set; }
}

public sealed class IntegrationKillSwitchAuditRecord : IDynamicData
{
    public Identity? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool Disabled { get; set; }

    public string? Reason { get; set; }

    public string? ChangedBy { get; set; }

    [EPiServerDataIndex]
    public DateTime ChangedUtc { get; set; }
}

/// <summary>
/// Stores the runtime switches in the Optimizely Dynamic Data Store, so no schema migration is needed.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class IntegrationKillSwitchRepository(
    DynamicDataStoreFactory storeFactory,
    IOptions<IntegrationKillSwitchOptions> options
) : IIntegrationKillSwitchRepository
{
    private string StoreName => options.Value.StoreName;

    private string AuditStoreName => options.Value.StoreName + "Audit";

    public IReadOnlyList<IntegrationKillSwitchRecord> GetAll()
    {
        return GetStore<IntegrationKillSwitchRecord>(StoreName).Items<IntegrationKillSwitchRecord>().ToList();
    }

    public void Save(IntegrationKillSwitchRecord record)
    {
        var store = GetStore<IntegrationKillSwitchRecord>(StoreName);

        var existing = store.Items<IntegrationKillSwitchRecord>().FirstOrDefault(r => r.Name == record.Name);

        if (existing is not null)
        {
            record.Id = existing.Id;
        }

        store.Save(record);
    }

    public void AddAudit(IntegrationKillSwitchAuditRecord audit)
    {
        GetStore<IntegrationKillSwitchAuditRecord>(AuditStoreName).Save(audit);
    }

    public IReadOnlyList<IntegrationKillSwitchAuditRecord> GetAudit(int count)
    {
        return GetStore<IntegrationKillSwitchAuditRecord>(AuditStoreName)
            .Items<IntegrationKillSwitchAuditRecord>()
            .OrderByDescending(a => a.ChangedUtc)
            .Take(count)
            .ToList();
    }

    private DynamicDataStore GetStore<T>(string name)
    {
        return storeFactory.GetStore(name)
            ?? storeFactory.CreateStore(name, typeof(T), new StoreDefinitionParameters());
    }
}
