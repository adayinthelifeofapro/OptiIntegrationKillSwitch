namespace Optimizely.IntegrationKillSwitch.Admin;

using Optimizely.IntegrationKillSwitch.Models;

public sealed class IntegrationKillSwitchViewModel
{
    public IReadOnlyList<IntegrationState> States { get; init; } = [];

    public IReadOnlyList<IntegrationAuditEntry> Audit { get; init; } = [];

    public string? Message { get; init; }

    public bool IsError { get; init; }
}
