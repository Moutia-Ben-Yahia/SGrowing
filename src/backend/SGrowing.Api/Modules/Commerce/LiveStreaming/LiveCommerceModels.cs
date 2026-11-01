using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Commerce.LiveStreaming;

#region Enums

/// <summary>
/// Lifecycle status of an interactive live commerce stream.
/// </summary>
public enum LiveStreamStatus
{
    Scheduled,
    Live,
    Paused,
    Ended
}

/// <summary>
/// Real-time streaming protocols supported for ultra-low latency broadcasting and playback.
/// </summary>
public enum StreamingProtocolType
{
    WebRTC_WHIP,
    WebRTC_WHEP,
    HLS_Adaptive
}

/// <summary>
/// Status assigned by the real-time AI moderation filter.
/// </summary>
public enum ModerationStatus
{
    Approved,
    FlaggedToxic,
    SpamBlocked
}

/// <summary>
/// Intent classified from live viewer comments by the AI Co-Host engine.
/// </summary>
public enum DetectedIntent
{
    ProductInquiry,
    PriceInquiry,
    Compliment,
    General,
    Spam
}

/// <summary>
/// Priority urgency of actionable prompts sent to the host's earpiece monitor.
/// </summary>
public enum CueUrgency
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3
}

#endregion

#region Domain Records & Models

/// <summary>
/// Low-latency streaming connection and ingest/playback URLs.
/// </summary>
public record StreamingProtocolInfo(
    string IngestUrl,
    string PlaybackUrl,
    string HlsFallbackUrl,
    StreamingProtocolType ProtocolType = StreamingProtocolType.WebRTC_WHIP);

/// <summary>
/// Represents the live flash sale product pinned to the stream overlay.
/// </summary>
public record PinnedProductRecord(
    string ProductId,
    string Sku,
    string Title,
    decimal OriginalPrice,
    decimal FlashSalePrice,
    int AvailableStock,
    int ReservedStock,
    DateTime PinnedAt,
    DateTime ExpirationTime,
    bool IsActive)
{
    /// <summary>
    /// Calculated available units remaining for immediate flash checkout.
    /// </summary>
    public int RemainingStock => Math.Max(0, AvailableStock - ReservedStock);

    /// <summary>
    /// Indicates whether the countdown timer for this flash deal has elapsed.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpirationTime;

    /// <summary>
    /// Percentage discount off the original catalog price.
    /// </summary>
    public decimal DiscountPercentage => OriginalPrice > 0m
        ? Math.Round(((OriginalPrice - FlashSalePrice) / OriginalPrice) * 100m, 2)
        : 0m;
}

/// <summary>
/// Active or past live commerce broadcast session.
/// </summary>
public class LiveStreamSession
{
    public required string SessionId { get; init; }
    public required string HostId { get; init; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StreamKey { get; init; } = string.Empty;
    public LiveStreamStatus Status { get; set; } = LiveStreamStatus.Scheduled;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int CurrentViewers { get; set; }
    public int PeakViewers { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public PinnedProductRecord? PinnedProduct { get; set; }
    public required StreamingProtocolInfo StreamingProtocol { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Sanitized and analyzed viewer comment within a live stream.
/// </summary>
public record ChatMessageRecord(
    string MessageId,
    string SessionId,
    string UserId,
    string Username,
    string Text,
    DateTime Timestamp,
    ModerationStatus ModerationStatus,
    double SentimentScore,
    DetectedIntent DetectedIntent,
    string? ModerationReason = null);

/// <summary>
/// Actionable cue whispered into the host's earpiece monitor or displayed on the teleprompter.
/// </summary>
public record AiCoHostCue(
    string CueId,
    string SessionId,
    string HostPromptText,
    CueUrgency Urgency,
    string RecommendedAction,
    DateTime GeneratedAt,
    bool IsAcknowledged = false);

/// <summary>
/// Result of an inventory lock attempt on a pinned flash deal.
/// </summary>
public record StockReservationResult(
    bool Success,
    string? ReservationId,
    string SessionId,
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    int RemainingStock,
    string Message);

/// <summary>
/// Composite outcome from processing a viewer chat message via the AI Co-Host engine.
/// </summary>
public record AiCoHostProcessingResult(
    ChatMessageRecord Message,
    string? AutoReplyText,
    AiCoHostCue? GeneratedCue);

#endregion

#region API Contracts

public record CreateLiveSessionRequest(
    string HostId,
    string Title,
    string Description);

public record PinProductRequest(
    string ProductId,
    string Sku,
    string Title,
    decimal OriginalPrice,
    decimal FlashSalePrice,
    int StockLimit,
    int DurationSeconds);

public record ReserveFlashStockRequest(
    string ProductId,
    string CustomerId,
    int Quantity);

public record PostChatMessageRequest(
    string UserId,
    string Username,
    string Text);

public record LiveCommerceHealthResponse(
    string Status,
    string Service,
    int ActiveSessionsCount,
    DateTime TimestampUtc);

#endregion
