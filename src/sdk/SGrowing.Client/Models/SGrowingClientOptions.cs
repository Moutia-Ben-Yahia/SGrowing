namespace SGrowing.Client.Models;

/// <summary>
/// Configuration options for the SGrowing client SDK.
/// </summary>
public class SGrowingClientOptions
{
    /// <summary>
    /// Base URL of the SGrowing API instance (default: "https://api.sgrowing.io").
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.sgrowing.io";

    /// <summary>
    /// API secret key or bearer token (e.g., "sg_live_...").
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Multi-tenant identifier.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// HTTP request timeout (default: 30 seconds).
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Additional custom HTTP headers to include in every outgoing request.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
