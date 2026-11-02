namespace SGrowing.Api.Modules.Integrations.Marketplace;

public enum AppCategory
{
    CRM,
    Commerce,
    Advertising,
    Productivity,
    AI,
    Communication
}

public enum InstallationStatus
{
    Available,
    Installing,
    Installed,
    Disabled,
    Error
}

public record AppManifest(
    string AppId,
    string Name,
    string Description,
    AppCategory Category,
    string Version,
    string Icon,
    string Author,
    List<string> RequiredPermissions,
    List<string> WebhookEvents,
    string? SandboxedIframeUrl,
    bool IsVerified
);

public record InstalledApp(
    string InstallationId,
    string TenantId,
    string AppId,
    InstallationStatus Status,
    Dictionary<string, string> Config,
    DateTime InstalledAtUtc,
    DateTime? LastSyncedUtc
);

public record InstallAppRequest(
    string TenantId,
    string AppId,
    Dictionary<string, string> Config
);

public record UninstallAppRequest(
    string TenantId,
    string AppId
);
