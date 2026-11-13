namespace Optimizely.IntegrationKillSwitch.Models;

/// <summary>
/// The current state of an integration, combining the config flag and the runtime switch.
/// </summary>
public sealed record IntegrationState
{
    public required IntegrationDescriptor Descriptor { get; init; }

    /// <summary>The value of Integrations:{Name}:Enabled, or of the legacy key or default.</summary>
    public bool EnabledInConfig { get; init; }

    /// <summary>True when an administrator has switched the integration off at runtime.</summary>
    public bool DisabledAtRuntime { get; init; }

    public string? Reason { get; init; }

    public string? ChangedBy { get; init; }

    public DateTime? ChangedUtc { get; init; }

    public bool IsActive => EnabledInConfig && !DisabledAtRuntime;
}

public sealed record IntegrationAuditEntry(
    string Name,
    bool Disabled,
    string? Reason,
    string? ChangedBy,
    DateTime ChangedUtc
);
