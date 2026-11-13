namespace Optimizely.IntegrationKillSwitch;

using Optimizely.IntegrationKillSwitch.Models;

public interface IIntegrationStatusService
{
    /// <summary>
    /// True when the integration is on in config and has not been switched off at runtime.
    /// Names that are not registered are treated as active.
    /// </summary>
    bool IsActive(string name);

    IReadOnlyList<IntegrationState> GetStates();

    /// <summary>The descriptors of all integrations that are currently off, for any reason.</summary>
    IReadOnlyList<IntegrationDescriptor> GetInactive();

    /// <summary>Switches an integration off or on at runtime. Applies across all instances.</summary>
    void SetRuntimeState(string name, bool disabled, string? reason, string? changedBy);

    IReadOnlyList<IntegrationAuditEntry> GetAudit(int count);
}
