namespace SGrowing.Core.Entities;

public class ChannelCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ChannelType { get; set; } = string.Empty; // "WhatsAppCloud", "MetaGraph", "Telegram", "Discord", "Twilio", "X"
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedAppSecret { get; set; } = string.Empty;
    public string? EncryptedRefreshToken { get; set; }
    public string? EncryptedSigningKey { get; set; }
    public string NonceBase64 { get; set; } = string.Empty;
    public string TagBase64 { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public static ChannelCredential Create(
        Guid tenantId,
        string channelType,
        string encryptedAccessToken,
        string encryptedAppSecret,
        string nonceBase64,
        string tagBase64,
        string? encryptedSigningKey = null,
        string? encryptedRefreshToken = null,
        DateTime? expiresAtUtc = null)
    {
        return new ChannelCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ChannelType = channelType,
            EncryptedAccessToken = encryptedAccessToken,
            EncryptedAppSecret = encryptedAppSecret,
            NonceBase64 = nonceBase64,
            TagBase64 = tagBase64,
            EncryptedSigningKey = encryptedSigningKey,
            EncryptedRefreshToken = encryptedRefreshToken,
            ExpiresAtUtc = expiresAtUtc,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public bool IsExpired()
    {
        return ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= DateTime.UtcNow;
    }
}
