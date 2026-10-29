using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Calendar;

public enum MeetingRoomStatus
{
    Active = 1,
    Ended = 2,
    Cancelled = 3,
    Expired = 4
}

public record ParticipantMediaStateDto(
    bool IsMicEnabled,
    bool IsCameraEnabled,
    bool IsScreenSharing,
    bool IsSpeaking,
    string NetworkQuality = "excellent",
    int LatencyMs = 32,
    double PacketLossRate = 0.0
);

public record ParticipantStateDto(
    string ParticipantIdentity,
    string DisplayName,
    string ParticipantRole,
    DateTime JoinedAtUtc,
    DateTime? LeftAtUtc,
    DateTime LastHeartbeatUtc,
    ParticipantMediaStateDto MediaState,
    int TotalSpeakingSeconds = 0
);

public record WebRtcAccessTokenResult(
    string AccessToken,
    string RoomToken,
    string ParticipantIdentity,
    string ParticipantRole,
    bool CanPublish,
    bool CanSubscribe,
    bool CanPublishData,
    DateTime ExpiresAtUtc,
    string SfuServerUrl
);

public record LiveKitTokenClaims(
    string RoomToken,
    string ParticipantIdentity,
    string ParticipantRole,
    bool CanPublish,
    bool CanSubscribe,
    bool CanPublishData,
    DateTime ExpiresAtUtc,
    DateTime IssuedAtUtc
);

public record SalesBattleCard(
    string ObjectionKey,
    string Category,
    string Title,
    string DetectedTrigger,
    string QuickPitch,
    List<string> KeyTalkingPoints,
    string RecommendedFollowUpQuestion,
    double ConfidenceScore,
    string Severity = "medium"
);

public record ObjectionEvaluationResult(
    string RoomToken,
    string TranscriptSegment,
    string Sentiment,
    double SentimentScore,
    List<SalesBattleCard> MatchedBattleCards,
    DateTime EvaluatedAtUtc
);

public record MeetingTranscriptItem(
    string SpeakerIdentity,
    string Transcript,
    double SentimentScore,
    bool IsFinal,
    DateTime TimestampUtc
);

public record AudioIngestResultDto(
    bool IsSpeech,
    double RmsEnergy,
    string VadStatus,
    double ProcessingLatencyMs,
    string TranscribedText,
    double SentimentScore,
    List<string> BuyingSignals,
    List<SalesBattleCard> TriggeredBattleCards
);

public record VideoMeetingSessionDto(
    string RoomToken,
    Guid TenantId,
    string Title,
    string HostName,
    string AttendeeName,
    MeetingRoomStatus Status,
    DateTime CreatedAtUtc,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    int DurationSeconds,
    string DurationFormatted,
    int ActiveParticipantCount,
    List<ParticipantStateDto> Participants,
    List<MeetingTranscriptItem> RecentTranscripts,
    List<SalesBattleCard> ActiveBattleCards,
    string MeetingUrl
);

public record MeetingParticipantAttendanceDto(
    string ParticipantIdentity,
    string DisplayName,
    string Role,
    DateTime JoinedAtUtc,
    DateTime LeftAtUtc,
    int DurationSeconds,
    int SpeakingSeconds,
    bool UsedCamera,
    bool UsedScreenShare
);

public record MeetingSessionSummaryDto(
    string RoomToken,
    Guid TenantId,
    string Title,
    string HostName,
    string AttendeeName,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    int TotalDurationSeconds,
    string TotalDurationFormatted,
    List<MeetingParticipantAttendanceDto> AttendanceHistory,
    List<string> ObjectionsEncountered,
    List<string> BuyingSignalsDetected,
    List<string> FollowUpTasks,
    string SummaryNotes
);

public class VideoMeetingSessionRecord
{
    public string RoomToken { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid? BookingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string AttendeeName { get; set; } = string.Empty;
    public MeetingRoomStatus Status { get; set; } = MeetingRoomStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public string SummaryNotes { get; set; } = string.Empty;
    public ConcurrentDictionary<string, ParticipantStateDto> Participants { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<MeetingTranscriptItem> Transcripts { get; set; } = new();
    public List<SalesBattleCard> BattleCardsHistory { get; set; } = new();
    public HashSet<string> DetectedSignals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> FollowUpTasks { get; set; } = new();
}

public interface IVideoMeetingRoomService
{
    Task<VideoMeetingSessionDto> CreateOrGetRoomAsync(Guid tenantId, string roomToken, string hostName = "", string attendeeName = "");
    Task<VideoMeetingSessionDto?> GetRoomInfoAsync(string roomToken);
    Task<WebRtcAccessTokenResult> MintWebRtcAccessTokenAsync(string roomToken, string participantIdentity, string participantRole, bool isPublisher = true);
    LiveKitTokenClaims VerifyWebRtcAccessToken(string token, string? expectedRoomToken = null);
    Task<ParticipantStateDto> UpdateParticipantStateAsync(string roomToken, string participantIdentity, ParticipantMediaStateDto state);
    Task<AudioIngestResultDto> IngestLiveCallAudioChunkAsync(string roomToken, string speakerIdentity, byte[] audioPcm16Chunk, string? transcriptHint = null);
    Task<ObjectionEvaluationResult> EvaluateInCallObjectionsAndBattleCardsAsync(string roomToken, string transcriptSegment);
    Task<MeetingSessionSummaryDto> EndMeetingRoomAsync(string roomToken, string summaryNotes = "");
}

public class VideoMeetingRoomService : IVideoMeetingRoomService
{
    private readonly SGrowingDbContext? _db;
    private readonly VoiceAiStreamService? _voiceAiService;
    private readonly ILogger<VideoMeetingRoomService>? _logger;
    private readonly string _jwtSecretKey;
    private readonly string _sfuServerUrl;

    // In-memory store for active ephemeral meeting sessions
    private static readonly ConcurrentDictionary<string, VideoMeetingSessionRecord> _activeSessions = new(StringComparer.OrdinalIgnoreCase);

    public VideoMeetingRoomService(
        SGrowingDbContext? db = null,
        VoiceAiStreamService? voiceAiService = null,
        IConfiguration? configuration = null,
        ILogger<VideoMeetingRoomService>? logger = null)
    {
        _db = db;
        _voiceAiService = voiceAiService;
        _logger = logger;
        _jwtSecretKey = configuration?["LiveKit:ApiKeySecret"] 
            ?? "sgrowing_livekit_super_secret_signing_key_2026_enterprise_sfu_webrtc";
        _sfuServerUrl = configuration?["LiveKit:ServerUrl"] 
            ?? "wss://livekit.sgrowing.io";
    }

    // 1. Create or Get Ephemeral Video Meeting Session
    public async Task<VideoMeetingSessionDto> CreateOrGetRoomAsync(
        Guid tenantId,
        string roomToken,
        string hostName = "",
        string attendeeName = "")
    {
        if (string.IsNullOrWhiteSpace(roomToken))
        {
            throw new ArgumentException("Room token cannot be empty.", nameof(roomToken));
        }

        var normalizedToken = roomToken.Trim();

        // 1. Check existing active in-memory session
        if (_activeSessions.TryGetValue(normalizedToken, out var existingSession))
        {
            if (existingSession.Status == MeetingRoomStatus.Ended || existingSession.Status == MeetingRoomStatus.Expired)
            {
                throw new InvalidOperationException("Meeting room has expired or been cancelled.");
            }

            // Update names if provided
            if (!string.IsNullOrWhiteSpace(hostName)) existingSession.HostName = hostName;
            if (!string.IsNullOrWhiteSpace(attendeeName)) existingSession.AttendeeName = attendeeName;

            return MapToDto(existingSession);
        }

        // 2. Validate against Database CalendarBookingRecord
        CalendarBookingRecord? booking = null;
        if (_db != null)
        {
            var tokenStr = normalizedToken;
            booking = await _db.CalendarBookingRecords
                .FirstOrDefaultAsync(b => b.Id.ToString() == tokenStr ||
                                          b.Id.ToString().StartsWith(tokenStr) ||
                                          b.MeetingUrl.EndsWith(tokenStr) ||
                                          b.IcsUid.StartsWith(tokenStr));
        }

        if (booking != null)
        {
            if (booking.Status == CalendarBookingStatus.Cancelled)
            {
                throw new InvalidOperationException("Meeting room has expired or been cancelled.");
            }

            // Reject if booking ended more than 24 hours ago
            if (booking.EndTimeUtc < DateTime.UtcNow.AddHours(-24))
            {
                throw new InvalidOperationException("Meeting room has expired.");
            }

            var sessionTitle = $"{booking.HostName} & {booking.AttendeeName} Consultation";
            var bookingSession = new VideoMeetingSessionRecord
            {
                RoomToken = normalizedToken,
                TenantId = booking.TenantId,
                BookingId = booking.Id,
                Title = sessionTitle,
                HostName = string.IsNullOrWhiteSpace(hostName) ? booking.HostName : hostName,
                AttendeeName = string.IsNullOrWhiteSpace(attendeeName) ? booking.AttendeeName : attendeeName,
                Status = MeetingRoomStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                StartedAtUtc = DateTime.UtcNow
            };

            _activeSessions[normalizedToken] = bookingSession;
            return MapToDto(bookingSession);
        }

        // 3. Fallback: Ad-hoc room generation if host/attendee specified or tenant provided
        if (tenantId != Guid.Empty && (!string.IsNullOrWhiteSpace(hostName) || !string.IsNullOrWhiteSpace(attendeeName)))
        {
            var hName = string.IsNullOrWhiteSpace(hostName) ? "Host Representative" : hostName.Trim();
            var aName = string.IsNullOrWhiteSpace(attendeeName) ? "Client Participant" : attendeeName.Trim();

            var adHocSession = new VideoMeetingSessionRecord
            {
                RoomToken = normalizedToken,
                TenantId = tenantId,
                BookingId = null,
                Title = $"{hName} & {aName} Strategy Session",
                HostName = hName,
                AttendeeName = aName,
                Status = MeetingRoomStatus.Active,
                CreatedAtUtc = DateTime.UtcNow,
                StartedAtUtc = DateTime.UtcNow
            };

            _activeSessions[normalizedToken] = adHocSession;
            return MapToDto(adHocSession);
        }

        throw new KeyNotFoundException($"Meeting room with token '{normalizedToken}' was not found or is invalid.");
    }

    public Task<VideoMeetingSessionDto?> GetRoomInfoAsync(string roomToken)
    {
        if (string.IsNullOrWhiteSpace(roomToken)) return Task.FromResult<VideoMeetingSessionDto?>(null);

        var token = roomToken.Trim();
        if (_activeSessions.TryGetValue(token, out var session))
        {
            return Task.FromResult<VideoMeetingSessionDto?>(MapToDto(session));
        }

        return Task.FromResult<VideoMeetingSessionDto?>(null);
    }

    // 2. Mint Secure WebRTC SFU Access Token (HMAC-SHA256 Signed JWT)
    public Task<WebRtcAccessTokenResult> MintWebRtcAccessTokenAsync(
        string roomToken,
        string participantIdentity,
        string participantRole,
        bool isPublisher = true)
    {
        if (string.IsNullOrWhiteSpace(roomToken))
            throw new ArgumentException("Room token cannot be empty.", nameof(roomToken));
        if (string.IsNullOrWhiteSpace(participantIdentity))
            throw new ArgumentException("Participant identity cannot be empty.", nameof(participantIdentity));

        var token = roomToken.Trim();
        var identity = participantIdentity.Trim();
        var role = string.IsNullOrWhiteSpace(participantRole) ? "attendee" : participantRole.Trim().ToLowerInvariant();

        if (_activeSessions.TryGetValue(token, out var session))
        {
            if (session.Status == MeetingRoomStatus.Ended || session.Status == MeetingRoomStatus.Expired)
            {
                throw new InvalidOperationException("Cannot mint access token for an ended or expired meeting room.");
            }
        }

        var expiresAtUtc = DateTime.UtcNow.AddHours(6);
        var jwt = GenerateJwt(identity, role, token, isPublisher, expiresAtUtc);

        // Register participant initial state in session if session exists
        if (session != null)
        {
            var displayName = role == "host" ? session.HostName : (role == "attendee" ? session.AttendeeName : identity);
            if (string.IsNullOrWhiteSpace(displayName)) displayName = identity;

            session.Participants.AddOrUpdate(
                identity,
                _ => new ParticipantStateDto(
                    ParticipantIdentity: identity,
                    DisplayName: displayName,
                    ParticipantRole: role,
                    JoinedAtUtc: DateTime.UtcNow,
                    LeftAtUtc: null,
                    LastHeartbeatUtc: DateTime.UtcNow,
                    MediaState: new ParticipantMediaStateDto(
                        IsMicEnabled: true,
                        IsCameraEnabled: true,
                        IsScreenSharing: false,
                        IsSpeaking: false,
                        NetworkQuality: "excellent",
                        LatencyMs: 28,
                        PacketLossRate: 0.0
                    )
                ),
                (_, existing) => existing with
                {
                    LastHeartbeatUtc = DateTime.UtcNow,
                    LeftAtUtc = null
                }
            );
        }

        var result = new WebRtcAccessTokenResult(
            AccessToken: jwt,
            RoomToken: token,
            ParticipantIdentity: identity,
            ParticipantRole: role,
            CanPublish: isPublisher,
            CanSubscribe: true,
            CanPublishData: isPublisher,
            ExpiresAtUtc: expiresAtUtc,
            SfuServerUrl: _sfuServerUrl
        );

        return Task.FromResult(result);
    }

    // Cryptographic Token Verification
    public LiveKitTokenClaims VerifyWebRtcAccessToken(string token, string? expectedRoomToken = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));

        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new InvalidOperationException("Invalid JWT token format.");

        var header = parts[0];
        var payload = parts[1];
        var signature = parts[2];

        // 1. Verify HMAC-SHA256 signature
        var stringToSign = $"{header}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_jwtSecretKey));
        var expectedSignature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expectedSignature)))
        {
            throw new InvalidOperationException("Token signature verification failed.");
        }

        // 2. Decode and parse claims
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(payload));
        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        var expUnix = root.GetProperty("exp").GetInt64();
        var expUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
        if (DateTime.UtcNow > expUtc)
        {
            throw new InvalidOperationException("WebRTC token has expired.");
        }

        var iatUnix = root.TryGetProperty("iat", out var iatProp)
            ? iatProp.GetInt64()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var iatUtc = DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime;

        var identity = root.GetProperty("sub").GetString() ?? "";
        var role = root.TryGetProperty("role", out var rProp) ? rProp.GetString() ?? "attendee" : "attendee";
        var room = root.TryGetProperty("room", out var roomProp) ? roomProp.GetString() ?? "" : "";

        bool canPublish = true;
        bool canSubscribe = true;
        bool canPublishData = true;

        if (root.TryGetProperty("video", out var videoProp))
        {
            if (videoProp.TryGetProperty("room", out var vRoom) && string.IsNullOrEmpty(room))
            {
                room = vRoom.GetString() ?? "";
            }
            if (videoProp.TryGetProperty("canPublish", out var cp)) canPublish = cp.GetBoolean();
            if (videoProp.TryGetProperty("canSubscribe", out var cs)) canSubscribe = cs.GetBoolean();
            if (videoProp.TryGetProperty("canPublishData", out var cpd)) canPublishData = cpd.GetBoolean();
        }

        if (!string.IsNullOrWhiteSpace(expectedRoomToken) &&
            !string.Equals(room, expectedRoomToken, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Token room mismatch. Expected '{expectedRoomToken}', got '{room}'.");
        }

        return new LiveKitTokenClaims(
            RoomToken: room,
            ParticipantIdentity: identity,
            ParticipantRole: role,
            CanPublish: canPublish,
            CanSubscribe: canSubscribe,
            CanPublishData: canPublishData,
            ExpiresAtUtc: expUtc,
            IssuedAtUtc: iatUtc
        );
    }

    // 3. Update Participant Media State (Microphone, Camera, Screen Sharing, Quality)
    public Task<ParticipantStateDto> UpdateParticipantStateAsync(
        string roomToken,
        string participantIdentity,
        ParticipantMediaStateDto state)
    {
        if (string.IsNullOrWhiteSpace(roomToken))
            throw new ArgumentException("Room token cannot be empty.", nameof(roomToken));
        if (string.IsNullOrWhiteSpace(participantIdentity))
            throw new ArgumentException("Participant identity cannot be empty.", nameof(participantIdentity));

        var token = roomToken.Trim();
        var identity = participantIdentity.Trim();

        if (!_activeSessions.TryGetValue(token, out var session))
        {
            throw new KeyNotFoundException($"Meeting room '{token}' was not found.");
        }

        if (session.Status == MeetingRoomStatus.Ended)
        {
            throw new InvalidOperationException("Cannot update participant state on an ended meeting.");
        }

        var updated = session.Participants.AddOrUpdate(
            identity,
            _ => new ParticipantStateDto(
                ParticipantIdentity: identity,
                DisplayName: identity,
                ParticipantRole: "attendee",
                JoinedAtUtc: DateTime.UtcNow,
                LeftAtUtc: null,
                LastHeartbeatUtc: DateTime.UtcNow,
                MediaState: state,
                TotalSpeakingSeconds: state.IsSpeaking ? 2 : 0
            ),
            (_, existing) => existing with
            {
                MediaState = state,
                LastHeartbeatUtc = DateTime.UtcNow,
                TotalSpeakingSeconds = state.IsSpeaking ? existing.TotalSpeakingSeconds + 2 : existing.TotalSpeakingSeconds
            }
        );

        return Task.FromResult(updated);
    }

    // 4. Ingest Live Audio Stream Chunk (Bridged into VoiceAiStreamService)
    public async Task<AudioIngestResultDto> IngestLiveCallAudioChunkAsync(
        string roomToken,
        string speakerIdentity,
        byte[] audioPcm16Chunk,
        string? transcriptHint = null)
    {
        if (string.IsNullOrWhiteSpace(roomToken))
            throw new ArgumentException("Room token cannot be empty.", nameof(roomToken));

        var token = roomToken.Trim();
        if (!_activeSessions.TryGetValue(token, out var session))
        {
            throw new KeyNotFoundException($"Meeting room '{token}' was not found.");
        }

        if (session.Status == MeetingRoomStatus.Ended)
        {
            throw new InvalidOperationException("Cannot stream audio to an ended meeting room.");
        }

        // Bridge into VoiceAiStreamService if available
        double rmsEnergy = 0.0;
        bool isSpeech = false;
        string vadStatus = "Silence";
        double latencyMs = 12.5;

        if (_voiceAiService != null)
        {
            var tracker = _voiceAiService.GetOrCreateTracker(session.TenantId, $"meeting_{token}_{speakerIdentity}");
            var result = _voiceAiService.IngestAudioFrame(audioPcm16Chunk, "pcm16", tracker);
            rmsEnergy = result.RmsEnergy;
            isSpeech = result.IsSpeech;
            vadStatus = result.VadStatus.ToString();
            latencyMs = result.ProcessingLatencyMs;
        }
        else
        {
            rmsEnergy = VoiceAiStreamService.CalculatePcm16RmsEnergy(audioPcm16Chunk);
            isSpeech = rmsEnergy >= 300.0;
            vadStatus = isSpeech ? "SpeechOngoing" : "Silence";
        }

        // Extract transcript and analyze
        var transcript = (transcriptHint ?? (isSpeech ? "Speaking live audio stream segment..." : "")).Trim();
        double sentimentScore = 0.0;
        var buyingSignals = new List<string>();
        var battleCards = new List<SalesBattleCard>();

        if (!string.IsNullOrWhiteSpace(transcript))
        {
            if (_voiceAiService != null)
            {
                sentimentScore = _voiceAiService.CalculateSentimentScore(transcript);
                buyingSignals = _voiceAiService.ExtractBuyingSignals(transcript);
            }
            else
            {
                sentimentScore = CalculateBasicSentiment(transcript);
                buyingSignals = ExtractBasicSignals(transcript);
            }

            foreach (var sig in buyingSignals)
            {
                session.DetectedSignals.Add(sig);
            }

            // In-call objection and battle-card evaluation
            var evaluation = await EvaluateInCallObjectionsAndBattleCardsAsync(token, transcript);
            battleCards = evaluation.MatchedBattleCards;

            // Record transcript
            session.Transcripts.Add(new MeetingTranscriptItem(
                SpeakerIdentity: speakerIdentity,
                Transcript: transcript,
                SentimentScore: sentimentScore,
                IsFinal: true,
                TimestampUtc: DateTime.UtcNow
            ));
        }

        return new AudioIngestResultDto(
            IsSpeech: isSpeech,
            RmsEnergy: Math.Round(rmsEnergy, 2),
            VadStatus: vadStatus,
            ProcessingLatencyMs: Math.Round(latencyMs, 2),
            TranscribedText: transcript,
            SentimentScore: sentimentScore,
            BuyingSignals: buyingSignals,
            TriggeredBattleCards: battleCards
        );
    }

    // 5. Evaluate In-Call Objections and Return AI Battle Cards
    public Task<ObjectionEvaluationResult> EvaluateInCallObjectionsAndBattleCardsAsync(
        string roomToken,
        string transcriptSegment)
    {
        var text = (transcriptSegment ?? "").Trim();
        var lower = text.ToLowerInvariant();
        var matched = new List<SalesBattleCard>();

        // 1. Pricing & Budget Objections
        if (lower.Contains("pricing too high") || lower.Contains("expensive") || lower.Contains("cost too much") ||
            lower.Contains("out of budget") || lower.Contains("can't afford") || lower.Contains("discount") ||
            lower.Contains("cheaper") || lower.Contains("pricing is high") || lower.Contains("too high"))
        {
            matched.Add(new SalesBattleCard(
                ObjectionKey: "PricingHigh",
                Category: "Budget & ROI",
                Title: "All-in-One Tech Stack Consolidation (3.4x ROI)",
                DetectedTrigger: "Pricing sensitivity or expensive software perception",
                QuickPitch: "Unlike buying separate tools for scheduling (Cal.com), video (Zoom), CRM (HubSpot), and call recording (Gong), SGrowing consolidates your entire stack, cutting monthly software bills by 40-60% with a proven 3.4x average ROI.",
                KeyTalkingPoints: new List<string>
                {
                    "Consolidates 4 software licenses into 1 unified platform with zero integration glue code.",
                    "Demonstrated 3.4x average ROI in the first 60 days by slashing lead response times to under 30 seconds.",
                    "Volume discounts available with flexible monthly or annual amortization."
                },
                RecommendedFollowUpQuestion: "What is your current total monthly spend across your separate CRM, video dialer, and scheduling tools?",
                ConfidenceScore: 0.95,
                Severity: "high"
            ));
        }

        // 2. Migration & Onboarding Objections
        if (lower.Contains("how does migration work") || lower.Contains("migration") || lower.Contains("migrate") ||
            lower.Contains("switch from") || lower.Contains("import contacts") || lower.Contains("downtime") ||
            lower.Contains("transition") || lower.Contains("switching"))
        {
            matched.Add(new SalesBattleCard(
                ObjectionKey: "MigrationFriction",
                Category: "Implementation & Onboarding",
                Title: "Zero-Downtime Dual Sync Migration",
                DetectedTrigger: "Concern about data transfer complexity or operational interruption",
                QuickPitch: "We support automated 1-click dual sync: your existing CRM and calendar continue running normally while all contacts, booking histories, and pipelines are mirrored into SGrowing in real time.",
                KeyTalkingPoints: new List<string>
                {
                    "1-click CSV/API contact and pipeline importer for HubSpot, Salesforce, Deskcomm, and Google Contacts.",
                    "Automated bi-directional calendar sync with Google Calendar & Outlook 365.",
                    "Dedicated onboarding specialist handles webhook setup and domain DNS verification with zero downtime."
                },
                RecommendedFollowUpQuestion: "Which CRM or calendar platform does your sales team currently rely on every day?",
                ConfidenceScore: 0.92,
                Severity: "medium"
            ));
        }

        // 3. Local Currency (Tunisian Dinars - TND) and Cash On Delivery (COD)
        if (lower.Contains("tunisian dinars") || lower.Contains("tnd") || lower.Contains("dinars") ||
            lower.Contains("tunisia") || lower.Contains("tunisie") || lower.Contains("cod") ||
            lower.Contains("cash on delivery") || lower.Contains("flouci") || lower.Contains("konnect") ||
            lower.Contains("paiement") || lower.Contains("livraison"))
        {
            matched.Add(new SalesBattleCard(
                ObjectionKey: "LocalPaymentTunisianDinarsCOD",
                Category: "Localization & Payment Gateways",
                Title: "Native Tunisian Dinars (TND) & WhatsApp COD Verification",
                DetectedTrigger: "Regional currency limitations or Cash-on-Delivery payment handling",
                QuickPitch: "SGrowing natively supports invoicing in Tunisian Dinars (TND), integrates local gateways like Flouci and Konnect, and provides automated WhatsApp OTP delivery verification that cuts COD returns by 45%.",
                KeyTalkingPoints: new List<string>
                {
                    "Native TND invoice generation with fiscal compliance and local bank transfer tracking.",
                    "Pre-integrated Tunisian payment gateways: Konnect, Flouci, and GPG with zero foreign exchange fees.",
                    "Automated WhatsApp confirmation workflows to verify customer addresses and dispatch status before courier pickup."
                },
                RecommendedFollowUpQuestion: "Are you primarily selling to local consumers across Tunisia via COD, or international B2B clients?",
                ConfidenceScore: 0.96,
                Severity: "high"
            ));
        }

        // 4. Data Security, GDPR & Privacy
        if (lower.Contains("security") || lower.Contains("gdpr") || lower.Contains("data privacy") ||
            lower.Contains("where is data stored") || lower.Contains("encryption") || lower.Contains("compliance") ||
            lower.Contains("soc2"))
        {
            matched.Add(new SalesBattleCard(
                ObjectionKey: "SecurityCompliance",
                Category: "Security & Governance",
                Title: "DTLS-SRTP WebRTC Encryption & Strict Tenant Boundary",
                DetectedTrigger: "Data sovereignty, confidentiality, or regulatory compliance inquiry",
                QuickPitch: "All real-time video and audio streams are encrypted end-to-end with DTLS-SRTP, meeting enterprise SOC 2 and GDPR compliance standards with strict multi-tenant database isolation.",
                KeyTalkingPoints: new List<string>
                {
                    "End-to-end DTLS 1.3 / SRTP media streaming; recordings are encrypted at rest with AES-256.",
                    "Multi-tenant PostgreSQL Row-Level Security ensuring strict cryptographic tenant data segregation.",
                    "Continuous WAL disaster recovery via Litestream to sovereign cloud object storage."
                },
                RecommendedFollowUpQuestion: "Do you have specific compliance requirements or a security audit checklist from your IT team?",
                ConfidenceScore: 0.90,
                Severity: "medium"
            ));
        }

        // 5. AI Reliability & Hallucination Concerns
        if (lower.Contains("hallucinate") || lower.Contains("ai mistake") || lower.Contains("accuracy") ||
            lower.Contains("can we trust the ai") || lower.Contains("bot error") || lower.Contains("reliable"))
        {
            matched.Add(new SalesBattleCard(
                ObjectionKey: "AiReliability",
                Category: "AI Trust & Accuracy",
                Title: "Stealth Human-in-the-Loop Sales Copilot",
                DetectedTrigger: "Skepticism regarding AI hallucinations or uncontrolled autonomous actions",
                QuickPitch: "The In-Call AI Copilot acts purely as an assistive advisor to the human sales rep: it surfaces talking points and battle cards in real time, while your human representative retains full conversational control.",
                KeyTalkingPoints: new List<string>
                {
                    "Grounded strictly in your company's product catalog and documentation via vector RAG embeddings.",
                    "Human-in-the-loop: battle cards are private to the representative and never exposed to the client.",
                    "Sub-500ms real-time objection recognition using high-precision semantic classifiers."
                },
                RecommendedFollowUpQuestion: "Would you prefer using the AI as an assistive co-pilot for your sales reps, or as an automated inbound receptionist?",
                ConfidenceScore: 0.88,
                Severity: "low"
            ));
        }

        // 6. Setup Time & Complexity
        if (lower.Contains("setup time") || lower.Contains("how long") || lower.Contains("complicated") ||
            lower.Contains("learning curve") || lower.Contains("too complex") || lower.Contains("get started"))
        {
            matched.Add(new SalesBattleCard(
                ObjectionKey: "SetupTimeComplexity",
                Category: "Time to Value",
                Title: "Zero-Install In-Browser WebRTC Experience",
                DetectedTrigger: "Anxiety around implementation delays or difficult user adoption",
                QuickPitch: "Your clients join directly in their browser without downloading any software or creating an account, and your team can be fully operational in under 15 minutes.",
                KeyTalkingPoints: new List<string>
                {
                    "No apps or plugins required: works seamlessly across Chrome, Safari, Edge, and mobile browsers.",
                    "Pre-configured booking templates and round-robin host routing out of the box.",
                    "Self-service onboarding with instant Google & Microsoft calendar connection."
                },
                RecommendedFollowUpQuestion: "How soon is your team looking to launch your next video consultation campaign?",
                ConfidenceScore: 0.89,
                Severity: "low"
            ));
        }

        // Calculate Sentiment
        double sentimentScore = 0.0;
        if (_voiceAiService != null)
        {
            sentimentScore = _voiceAiService.CalculateSentimentScore(text);
        }
        else
        {
            sentimentScore = CalculateBasicSentiment(text);
        }

        string sentimentLabel;
        if (matched.Any(b => b.Severity == "high") || sentimentScore <= -0.2)
        {
            sentimentLabel = "Skeptical";
        }
        else if (sentimentScore >= 0.25)
        {
            sentimentLabel = "Positive";
        }
        else
        {
            sentimentLabel = "Neutral";
        }

        // Store matched battle cards into session history
        if (_activeSessions.TryGetValue(roomToken, out var session))
        {
            foreach (var card in matched)
            {
                if (!session.BattleCardsHistory.Any(c => c.ObjectionKey == card.ObjectionKey))
                {
                    session.BattleCardsHistory.Add(card);
                }
            }
        }

        var result = new ObjectionEvaluationResult(
            RoomToken: roomToken,
            TranscriptSegment: text,
            Sentiment: sentimentLabel,
            SentimentScore: sentimentScore,
            MatchedBattleCards: matched,
            EvaluatedAtUtc: DateTime.UtcNow
        );

        return Task.FromResult(result);
    }

    // 6. End Meeting Room, Record Attendance & Generate Follow-ups
    public async Task<MeetingSessionSummaryDto> EndMeetingRoomAsync(string roomToken, string summaryNotes = "")
    {
        if (string.IsNullOrWhiteSpace(roomToken))
            throw new ArgumentException("Room token cannot be empty.", nameof(roomToken));

        var token = roomToken.Trim();
        if (!_activeSessions.TryGetValue(token, out var session))
        {
            throw new KeyNotFoundException($"Meeting room '{token}' was not found.");
        }

        session.Status = MeetingRoomStatus.Ended;
        session.EndedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(summaryNotes))
        {
            session.SummaryNotes = summaryNotes.Trim();
        }

        var totalDuration = session.EndedAtUtc.Value - session.StartedAtUtc;
        var durationSec = (int)Math.Max(0, totalDuration.TotalSeconds);
        var durationFormatted = FormatDuration(durationSec);

        // Update booking in database if linked
        if (_db != null && session.BookingId.HasValue)
        {
            var booking = await _db.CalendarBookingRecords.FirstOrDefaultAsync(b => b.Id == session.BookingId.Value);
            if (booking != null)
            {
                booking.Status = CalendarBookingStatus.Completed;
                if (!string.IsNullOrWhiteSpace(session.SummaryNotes))
                {
                    booking.Notes = string.IsNullOrWhiteSpace(booking.Notes)
                        ? $"[Call Completed]: {session.SummaryNotes}"
                        : $"{booking.Notes}\n[Call Completed]: {session.SummaryNotes}";
                }
                await _db.SaveChangesAsync();
            }
        }

        // Build attendance history
        var attendanceList = new List<MeetingParticipantAttendanceDto>();
        foreach (var p in session.Participants.Values)
        {
            var leftTime = p.LeftAtUtc ?? session.EndedAtUtc.Value;
            var participantDuration = (int)Math.Max(0, (leftTime - p.JoinedAtUtc).TotalSeconds);

            attendanceList.Add(new MeetingParticipantAttendanceDto(
                ParticipantIdentity: p.ParticipantIdentity,
                DisplayName: p.DisplayName,
                Role: p.ParticipantRole,
                JoinedAtUtc: p.JoinedAtUtc,
                LeftAtUtc: leftTime,
                DurationSeconds: participantDuration,
                SpeakingSeconds: p.TotalSpeakingSeconds,
                UsedCamera: p.MediaState.IsCameraEnabled,
                UsedScreenShare: p.MediaState.IsScreenSharing
            ));
        }

        // Generate intelligent follow-up tasks based on objections, signals, and notes
        var followUps = new List<string>();
        var objections = session.BattleCardsHistory.Select(c => c.Title).Distinct().ToList();

        if (session.BattleCardsHistory.Any(c => c.ObjectionKey == "PricingHigh"))
        {
            followUps.Add("Deliver customized ROI calculator & annual bundle pricing proposal with tech stack consolidation breakdown.");
        }

        if (session.BattleCardsHistory.Any(c => c.ObjectionKey == "MigrationFriction"))
        {
            followUps.Add("Share 1-click CRM & calendar migration guide with sample CSV mapping template.");
        }

        if (session.BattleCardsHistory.Any(c => c.ObjectionKey == "LocalPaymentTunisianDinarsCOD"))
        {
            followUps.Add("Send formal quote in Tunisian Dinars (TND) with Flouci/Konnect checkout and COD WhatsApp workflow setup.");
        }

        if (session.BattleCardsHistory.Any(c => c.ObjectionKey == "SecurityCompliance"))
        {
            followUps.Add("Provide SGrowing Security Architecture Whitepaper and SOC2 / GDPR compliance declaration.");
        }

        if (session.DetectedSignals.Contains("PricingInquiry") || session.DetectedSignals.Contains("ContractRequest"))
        {
            followUps.Add("Prepare enterprise Master Services Agreement (MSA) and DocuSign contract for immediate review.");
        }

        if (!string.IsNullOrWhiteSpace(summaryNotes))
        {
            followUps.Add($"Action client meeting notes: \"{summaryNotes}\"");
        }

        if (followUps.Count == 0)
        {
            followUps.Add("Send consultation recap email with recorded action items and calendar follow-up link.");
        }

        session.FollowUpTasks = followUps;

        var summary = new MeetingSessionSummaryDto(
            RoomToken: session.RoomToken,
            TenantId: session.TenantId,
            Title: session.Title,
            HostName: session.HostName,
            AttendeeName: session.AttendeeName,
            StartedAtUtc: session.StartedAtUtc,
            EndedAtUtc: session.EndedAtUtc.Value,
            TotalDurationSeconds: durationSec,
            TotalDurationFormatted: durationFormatted,
            AttendanceHistory: attendanceList,
            ObjectionsEncountered: objections,
            BuyingSignalsDetected: session.DetectedSignals.ToList(),
            FollowUpTasks: followUps,
            SummaryNotes: session.SummaryNotes
        );

        return summary;
    }

    // Helper: Map internal record to DTO
    private static VideoMeetingSessionDto MapToDto(VideoMeetingSessionRecord s)
    {
        var duration = (s.EndedAtUtc ?? DateTime.UtcNow) - s.StartedAtUtc;
        var durationSec = (int)Math.Max(0, duration.TotalSeconds);

        return new VideoMeetingSessionDto(
            RoomToken: s.RoomToken,
            TenantId: s.TenantId,
            Title: s.Title,
            HostName: s.HostName,
            AttendeeName: s.AttendeeName,
            Status: s.Status,
            CreatedAtUtc: s.CreatedAtUtc,
            StartedAtUtc: s.StartedAtUtc,
            EndedAtUtc: s.EndedAtUtc,
            DurationSeconds: durationSec,
            DurationFormatted: FormatDuration(durationSec),
            ActiveParticipantCount: s.Participants.Values.Count(p => p.LeftAtUtc == null),
            Participants: s.Participants.Values.ToList(),
            RecentTranscripts: s.Transcripts.TakeLast(15).ToList(),
            ActiveBattleCards: s.BattleCardsHistory.TakeLast(6).ToList(),
            MeetingUrl: $"https://meet.sgrowing.io/room/{s.RoomToken}"
        );
    }

    public static string FormatDuration(int seconds)
    {
        int hrs = seconds / 3600;
        int mins = (seconds % 3600) / 60;
        int secs = seconds % 60;
        return hrs > 0 ? $"{hrs:D2}:{mins:D2}:{secs:D2}" : $"{mins:D2}:{secs:D2}";
    }

    private string GenerateJwt(
        string participantIdentity,
        string participantRole,
        string roomToken,
        bool canPublish,
        DateTime expiresAtUtc)
    {
        var headerObj = new { alg = "HS256", typ = "JWT" };
        var headerBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(headerObj));
        var header = Base64UrlEncode(headerBytes);

        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds();

        var payloadObj = new Dictionary<string, object>
        {
            ["iss"] = "sgrowing-livekit-sfu",
            ["sub"] = participantIdentity,
            ["name"] = participantIdentity,
            ["room"] = roomToken,
            ["role"] = participantRole,
            ["video"] = new Dictionary<string, object>
            {
                ["room"] = roomToken,
                ["roomJoin"] = true,
                ["canPublish"] = canPublish,
                ["canSubscribe"] = true,
                ["canPublishData"] = canPublish
            },
            ["iat"] = iat,
            ["exp"] = exp
        };

        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payloadObj));
        var payload = Base64UrlEncode(payloadBytes);

        var stringToSign = $"{header}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_jwtSecretKey));
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));

        return $"{stringToSign}.{signature}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        string padded = base64Url.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }

    private static double CalculateBasicSentiment(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0.0;
        var lower = text.ToLowerInvariant();
        double score = 0.0;
        if (lower.Contains("great") || lower.Contains("excellent") || lower.Contains("perfect") || lower.Contains("interested")) score += 0.5;
        if (lower.Contains("expensive") || lower.Contains("bad") || lower.Contains("problem") || lower.Contains("difficult")) score -= 0.5;
        return Math.Clamp(score, -1.0, 1.0);
    }

    private static List<string> ExtractBasicSignals(string text)
    {
        var list = new List<string>();
        var lower = text.ToLowerInvariant();
        if (lower.Contains("pricing") || lower.Contains("cost") || lower.Contains("discount")) list.Add("PricingInquiry");
        if (lower.Contains("contract") || lower.Contains("sign") || lower.Contains("proposal")) list.Add("ContractRequest");
        return list;
    }
}
