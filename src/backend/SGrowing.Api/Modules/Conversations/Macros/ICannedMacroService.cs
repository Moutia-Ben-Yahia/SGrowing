namespace SGrowing.Api.Modules.Conversations.Macros;

public record CannedMacroDto(
    Guid Id,
    Guid TenantId,
    string TriggerKey, // e.g. "/pricing", "/refund", "/faq"
    string Title,
    string Category, // "Billing", "Support", "Sales", "General"
    string ContentTemplate,
    List<string> RequiredVariables,
    DateTime CreatedAtUtc
);

public record CreateCannedMacroRequest(
    string TriggerKey,
    string Title,
    string Category,
    string ContentTemplate
);

public record MacroSubstitutionContext(
    string? ContactName = null,
    string? ContactEmail = null,
    string? AgentName = null,
    string? CompanyName = null,
    string? OrderNumber = null,
    decimal? OrderAmount = null,
    string? Currency = "USD",
    Dictionary<string, string>? CustomVariables = null
);

public record CannedMacroResolutionDto(
    string TriggerKey,
    string Title,
    string ResolvedContent,
    List<string> MissingVariables
);

public interface ICannedMacroService
{
    Task<CannedMacroDto> CreateMacroAsync(Guid tenantId, CreateCannedMacroRequest req, CancellationToken ct = default);
    Task<List<CannedMacroDto>> GetMacrosAsync(Guid tenantId, string? category = null, CancellationToken ct = default);
    Task<List<CannedMacroDto>> SearchMacrosByHotkeyAsync(Guid tenantId, string hotkeyQuery, CancellationToken ct = default);
    Task<CannedMacroResolutionDto> ResolveMacroAsync(Guid tenantId, string triggerKey, MacroSubstitutionContext context, CancellationToken ct = default);
}
