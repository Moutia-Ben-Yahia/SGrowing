using System.Text;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Calendar;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Calendar;

public class VideoMeetingRoomTests
{
    private readonly SGrowingDbContext _db;
    private readonly VoiceAiStreamService _voiceAiService;
    private readonly VideoMeetingRoomService _meetingService;

    public VideoMeetingRoomTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _voiceAiService = new VoiceAiStreamService();
        _meetingService = new VideoMeetingRoomService(_db, _voiceAiService);
    }

    [Fact]
    public async Task CreateOrGetRoomAsync_WithValidBooking_CreatesEphemeralSession()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var booking = CalendarBookingRecord.Create(
            tenantId,
            Guid.NewGuid(),
            hostId,
            "Dr. Tarek Ben Salem",
            "Sarra Mansour",
            "sarra@enterprise.tn",
            "+216 98 123 456",
            DateTime.UtcNow.AddMinutes(10),
            DateTime.UtcNow.AddMinutes(40),
            "Africa/Tunis",
            "Enterprise Consultation"
        );
        _db.CalendarBookingRecords.Add(booking);
        await _db.SaveChangesAsync();

        var tokenPrefix = booking.Id.ToString()[..8];

        // Act
        var room = await _meetingService.CreateOrGetRoomAsync(tenantId, tokenPrefix);

        // Assert
        Assert.NotNull(room);
        Assert.Equal(tokenPrefix, room.RoomToken);
        Assert.Equal(tenantId, room.TenantId);
        Assert.Equal("Dr. Tarek Ben Salem", room.HostName);
        Assert.Equal("Sarra Mansour", room.AttendeeName);
        Assert.Equal(MeetingRoomStatus.Active, room.Status);
        Assert.Contains(tokenPrefix, room.MeetingUrl);
    }

    [Fact]
    public async Task CreateOrGetRoomAsync_AdHocSession_SucceedsWithTenantAndNames()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var adHocToken = $"instant-{Guid.NewGuid().ToString()[..6]}";

        // Act
        var room = await _meetingService.CreateOrGetRoomAsync(
            tenantId,
            adHocToken,
            hostName: "Amine Khemir",
            attendeeName: "Leila Bouazizi"
        );

        // Assert
        Assert.NotNull(room);
        Assert.Equal(adHocToken, room.RoomToken);
        Assert.Equal("Amine Khemir", room.HostName);
        Assert.Equal("Leila Bouazizi", room.AttendeeName);
        Assert.Equal(MeetingRoomStatus.Active, room.Status);
    }

    [Fact]
    public async Task CreateOrGetRoomAsync_NonExistentToken_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _meetingService.CreateOrGetRoomAsync(Guid.Empty, "unknown-room-token-999"));
    }

    [Fact]
    public async Task CreateOrGetRoomAsync_CancelledBooking_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var booking = CalendarBookingRecord.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Host User",
            "Attendee User",
            "attendee@test.com",
            "+216 22 000 111",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2)
        );
        booking.Status = CalendarBookingStatus.Cancelled;
        _db.CalendarBookingRecords.Add(booking);
        await _db.SaveChangesAsync();

        var token = booking.Id.ToString()[..8];

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _meetingService.CreateOrGetRoomAsync(tenantId, token));
        Assert.Contains("expired or been cancelled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateOrGetRoomAsync_ExpiredBooking_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var booking = CalendarBookingRecord.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Host User",
            "Attendee User",
            "attendee@test.com",
            "+216 22 000 111",
            DateTime.UtcNow.AddDays(-3),
            DateTime.UtcNow.AddDays(-3).AddMinutes(30)
        );
        _db.CalendarBookingRecords.Add(booking);
        await _db.SaveChangesAsync();

        var token = booking.Id.ToString()[..8];

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _meetingService.CreateOrGetRoomAsync(tenantId, token));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateOrGetRoomAsync_EmptyToken_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _meetingService.CreateOrGetRoomAsync(Guid.NewGuid(), "   "));
    }

    [Fact]
    public async Task MintWebRtcAccessToken_GeneratesValidJwtAndVerifiesClaims()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var roomToken = $"room-{Guid.NewGuid().ToString()[..6]}";
        await _meetingService.CreateOrGetRoomAsync(tenantId, roomToken, "Host John", "Attendee Sarah");

        // Act
        var tokenResult = await _meetingService.MintWebRtcAccessTokenAsync(
            roomToken,
            participantIdentity: "user_john_host",
            participantRole: "host",
            isPublisher: true
        );

        // Assert Token Result
        Assert.NotNull(tokenResult);
        Assert.NotEmpty(tokenResult.AccessToken);
        Assert.Equal(roomToken, tokenResult.RoomToken);
        Assert.Equal("user_john_host", tokenResult.ParticipantIdentity);
        Assert.Equal("host", tokenResult.ParticipantRole);
        Assert.True(tokenResult.CanPublish);
        Assert.True(tokenResult.CanSubscribe);
        Assert.True(tokenResult.CanPublishData);
        Assert.True(tokenResult.ExpiresAtUtc > DateTime.UtcNow);

        // Act: Verify Token Claims Cryptographically
        var claims = _meetingService.VerifyWebRtcAccessToken(tokenResult.AccessToken, roomToken);

        // Assert Claims
        Assert.NotNull(claims);
        Assert.Equal(roomToken, claims.RoomToken);
        Assert.Equal("user_john_host", claims.ParticipantIdentity);
        Assert.Equal("host", claims.ParticipantRole);
        Assert.True(claims.CanPublish);
        Assert.True(claims.CanSubscribe);
        Assert.True(claims.CanPublishData);
        Assert.True(claims.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task VerifyWebRtcAccessToken_TamperedToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var roomToken = $"room-{Guid.NewGuid().ToString()[..6]}";
        await _meetingService.CreateOrGetRoomAsync(Guid.NewGuid(), roomToken, "Host", "Attendee");
        var tokenResult = await _meetingService.MintWebRtcAccessTokenAsync(roomToken, "user_alice", "attendee");

        var parts = tokenResult.AccessToken.Split('.');
        // Tamper with payload
        var tamperedToken = $"{parts[0]}.{parts[1]}A.{parts[2]}";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _meetingService.VerifyWebRtcAccessToken(tamperedToken, roomToken));
    }

    [Fact]
    public async Task VerifyWebRtcAccessToken_RoomMismatch_ThrowsInvalidOperationException()
    {
        // Arrange
        var roomTokenA = $"room-A-{Guid.NewGuid().ToString()[..6]}";
        var roomTokenB = $"room-B-{Guid.NewGuid().ToString()[..6]}";
        await _meetingService.CreateOrGetRoomAsync(Guid.NewGuid(), roomTokenA, "Host", "Attendee");

        var tokenResult = await _meetingService.MintWebRtcAccessTokenAsync(roomTokenA, "user_alice", "attendee");

        // Act & Assert (verifying token minted for A against room B)
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _meetingService.VerifyWebRtcAccessToken(tokenResult.AccessToken, expectedRoomToken: roomTokenB));
        Assert.Contains("mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateParticipantStateAsync_TracksMediaTransitions()
    {
        // Arrange
        var roomToken = $"room-{Guid.NewGuid().ToString()[..6]}";
        await _meetingService.CreateOrGetRoomAsync(Guid.NewGuid(), roomToken, "Host Rep", "Client VIP");
        await _meetingService.MintWebRtcAccessTokenAsync(roomToken, "client_vip_id", "attendee");

        var state = new ParticipantMediaStateDto(
            IsMicEnabled: false,
            IsCameraEnabled: true,
            IsScreenSharing: true,
            IsSpeaking: true,
            NetworkQuality: "good",
            LatencyMs: 45,
            PacketLossRate: 0.01
        );

        // Act
        var updated = await _meetingService.UpdateParticipantStateAsync(roomToken, "client_vip_id", state);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("client_vip_id", updated.ParticipantIdentity);
        Assert.False(updated.MediaState.IsMicEnabled);
        Assert.True(updated.MediaState.IsCameraEnabled);
        Assert.True(updated.MediaState.IsScreenSharing);
        Assert.True(updated.MediaState.IsSpeaking);
        Assert.Equal("good", updated.MediaState.NetworkQuality);
        Assert.Equal(45, updated.MediaState.LatencyMs);
        Assert.True(updated.TotalSpeakingSeconds > 0);
    }

    [Fact]
    public async Task IngestLiveCallAudioChunk_DetectsEnergyAndExtractsBuyingSignals()
    {
        // Arrange
        var roomToken = $"room-{Guid.NewGuid().ToString()[..6]}";
        await _meetingService.CreateOrGetRoomAsync(Guid.NewGuid(), roomToken, "Host", "Buyer");

        // Create synthetic PCM16 voice sine-wave chunk
        var audioBytes = new byte[320];
        for (int i = 0; i < 160; i++)
        {
            short sample = (short)(Math.Sin(2 * Math.PI * 440 * i / 8000.0) * 12000);
            audioBytes[i * 2] = (byte)(sample & 0xFF);
            audioBytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        // Act
        var result = await _meetingService.IngestLiveCallAudioChunkAsync(
            roomToken,
            speakerIdentity: "Buyer",
            audioPcm16Chunk: audioBytes,
            transcriptHint: "We are very interested and want to understand the enterprise pricing and contract terms."
        );

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSpeech);
        Assert.True(result.RmsEnergy > 1000.0);
        Assert.Equal("SpeechOngoing", result.VadStatus);
        Assert.Contains("PricingInquiry", result.BuyingSignals);
        Assert.True(result.SentimentScore >= 0);
        Assert.NotNull(result.TriggeredBattleCards);
    }

    [Fact]
    public async Task EvaluateInCallObjectionsAndBattleCards_MatchesEnterpriseObjectionsAccurately()
    {
        // Arrange
        var roomToken = $"room-{Guid.NewGuid().ToString()[..6]}";
        await _meetingService.CreateOrGetRoomAsync(Guid.NewGuid(), roomToken, "Sales Rep", "Enterprise Prospect");

        // 1. Pricing Objection
        var pricingEval = await _meetingService.EvaluateInCallObjectionsAndBattleCardsAsync(
            roomToken,
            "Your software looks great, but honestly the pricing is too high and out of our budget."
        );
        Assert.Equal("Skeptical", pricingEval.Sentiment);
        var pricingCard = Assert.Single(pricingEval.MatchedBattleCards, c => c.ObjectionKey == "PricingHigh");
        Assert.Contains("Consolidation", pricingCard.Title);
        Assert.Contains("3.4x", pricingCard.QuickPitch);

        // 2. Migration Objection
        var migrationEval = await _meetingService.EvaluateInCallObjectionsAndBattleCardsAsync(
            roomToken,
            "How does migration work if we are already using HubSpot and Google Calendar?"
        );
        var migrationCard = Assert.Single(migrationEval.MatchedBattleCards, c => c.ObjectionKey == "MigrationFriction");
        Assert.Contains("Dual Sync", migrationCard.Title);
        Assert.Contains("1-click", migrationCard.QuickPitch);

        // 3. Tunisian Dinars / COD Objection
        var codEval = await _meetingService.EvaluateInCallObjectionsAndBattleCardsAsync(
            roomToken,
            "Do you support billing in Tunisian dinars and cash on delivery confirmation via WhatsApp?"
        );
        var codCard = Assert.Single(codEval.MatchedBattleCards, c => c.ObjectionKey == "LocalPaymentTunisianDinarsCOD");
        Assert.Contains("Tunisian Dinars", codCard.Title);
        Assert.Contains("COD", codCard.QuickPitch);
        Assert.Contains("Flouci", codCard.QuickPitch);
    }

    [Fact]
    public async Task EndMeetingRoomAsync_CalculatesDuration_UpdatesBooking_GeneratesFollowUps()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var booking = CalendarBookingRecord.Create(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Yassine Ben Amor",
            "Mouna Trabelsi",
            "mouna@logistics.tn",
            "+216 55 999 888",
            DateTime.UtcNow.AddMinutes(5),
            DateTime.UtcNow.AddMinutes(35)
        );
        _db.CalendarBookingRecords.Add(booking);
        await _db.SaveChangesAsync();

        var token = booking.Id.ToString()[..8];
        await _meetingService.CreateOrGetRoomAsync(tenantId, token);
        await _meetingService.MintWebRtcAccessTokenAsync(token, "host_rep", "host");
        await _meetingService.MintWebRtcAccessTokenAsync(token, "mouna_attendee", "attendee");

        // Trigger objections during call
        await _meetingService.EvaluateInCallObjectionsAndBattleCardsAsync(
            token,
            "We need invoices in Tunisian dinars (TND) and want to know how COD verification works."
        );

        // Act: End Call
        var summary = await _meetingService.EndMeetingRoomAsync(
            token,
            summaryNotes: "Agreed to proceed with Enterprise tier if COD integration is verified."
        );

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(token, summary.RoomToken);
        Assert.Equal("Yassine Ben Amor", summary.HostName);
        Assert.Equal("Mouna Trabelsi", summary.AttendeeName);
        Assert.True(summary.TotalDurationSeconds >= 0);
        Assert.False(string.IsNullOrEmpty(summary.TotalDurationFormatted));
        Assert.NotEmpty(summary.AttendanceHistory);
        Assert.Contains("Agreed to proceed", summary.SummaryNotes);

        // Follow-up task generation check
        Assert.Contains(summary.FollowUpTasks, task => task.Contains("Tunisian Dinars") || task.Contains("COD"));
        Assert.Contains(summary.FollowUpTasks, task => task.Contains("Agreed to proceed"));

        // Verify booking status was updated in DB
        var updatedBooking = await _db.CalendarBookingRecords.FirstOrDefaultAsync(b => b.Id == booking.Id);
        Assert.NotNull(updatedBooking);
        Assert.Equal(CalendarBookingStatus.Completed, updatedBooking.Status);
        Assert.Contains("[Call Completed]", updatedBooking.Notes);

        // Verify that ended room rejects further access token minting
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _meetingService.MintWebRtcAccessTokenAsync(token, "late_joiner", "attendee"));
    }

    [Fact]
    public async Task EndMeetingRoomAsync_NonExistentRoom_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _meetingService.EndMeetingRoomAsync("non-existent-room-999"));
    }

    [Fact]
    public async Task UpdateParticipantStateAsync_NonExistentRoom_ThrowsKeyNotFoundException()
    {
        var dummyState = new ParticipantMediaStateDto(true, true, false, false);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _meetingService.UpdateParticipantStateAsync("non-existent-room-999", "user1", dummyState));
    }

    [Fact]
    public async Task IngestLiveCallAudioChunkAsync_NonExistentRoom_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _meetingService.IngestLiveCallAudioChunkAsync("non-existent-room-999", "speaker1", new byte[160]));
    }

    [Fact]
    public async Task EvaluateInCallObjectionsAndBattleCards_MatchesSecurityAndAiReliabilityObjections()
    {
        var roomToken = $"room-{Guid.NewGuid().ToString()[..6]}";
        await _meetingService.CreateOrGetRoomAsync(Guid.NewGuid(), roomToken, "Host", "Attendee");

        var eval = await _meetingService.EvaluateInCallObjectionsAndBattleCardsAsync(
            roomToken,
            "We have strict security and gdpr data privacy rules. Also, can we trust the ai not to hallucinate?"
        );

        Assert.NotNull(eval);
        Assert.Contains(eval.MatchedBattleCards, c => c.ObjectionKey == "SecurityCompliance");
        Assert.Contains(eval.MatchedBattleCards, c => c.ObjectionKey == "AiReliability");
    }

    [Theory]
    [InlineData(45, "00:45")]
    [InlineData(872, "14:32")]
    [InlineData(3665, "01:01:05")]
    public void FormatDuration_FormatsVariousSecondRangesCorrectly(int seconds, string expected)
    {
        var formatted = VideoMeetingRoomService.FormatDuration(seconds);
        Assert.Equal(expected, formatted);
    }
}
