using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using SGrowing.Api.Modules.Channels.WhatsApp.Companion;
using Xunit;

namespace SGrowing.Tests.Channels;

public class WhatsAppCompanionSocketServiceTests
{
    private readonly WhatsAppCompanionSocketService _service = new();
    private readonly Guid _testTenant = Guid.NewGuid();

    #region 1. 8-Digit Pairing Code Engine & Uniqueness Tests

    [Fact]
    public void Generate8DigitPairingCode_ReturnsValidFormat_WithHyphen()
    {
        var code = WhatsAppCompanionSocketService.Generate8DigitPairingCode();

        code.Should().NotBeNullOrWhiteSpace();
        code.Length.Should().Be(9); // 4 chars + hyphen + 4 chars = 9
        code[4].Should().Be('-');

        var rawChars = code.Replace("-", "");
        rawChars.Length.Should().Be(8);
        rawChars.Should().MatchRegex("^[23456789ABCDEFGHJKLMNPQRSTUVWXYZ]{8}$");
    }

    [Fact]
    public void Generate8DigitPairingCode_GeneratesUniqueCodesAcrossConsecutiveCalls()
    {
        var codes = new HashSet<string>();
        for (int i = 0; i < 500; i++)
        {
            var code = WhatsAppCompanionSocketService.Generate8DigitPairingCode();
            codes.Add(code).Should().BeTrue($"Code '{code}' should be unique across executions.");
        }
    }

    [Theory]
    [InlineData("+21698123456", "+21698123456")]
    [InlineData("0021698123456", "+21698123456")]
    [InlineData("21698123456", "+21698123456")]
    [InlineData("+1 (555) 234-5678", "+15552345678")]
    [InlineData("+33 6 12 34 56 78", "+33612345678")]
    public void NormalizePhoneNumber_WithValidE164_ReturnsCleanPlusFormat(string input, string expected)
    {
        var normalized = WhatsAppCompanionSocketService.NormalizePhoneNumber(input);
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("+abc12345")]
    [InlineData("+0123456789")]
    public void NormalizePhoneNumber_InvalidFormat_ThrowsArgumentException(string? invalidInput)
    {
        var act = () => WhatsAppCompanionSocketService.NormalizePhoneNumber(invalidInput);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task RequestPairingCodeAsync_CreatesSessionWith180sTtl()
    {
        var result = await _service.RequestPairingCodeAsync(_testTenant, "+21698111222", "Desktop Station Pro");

        result.Should().NotBeNull();
        result.SessionId.Should().NotBeEmpty();
        result.TenantId.Should().Be(_testTenant);
        result.PhoneNumber.Should().Be("+21698111222");
        result.PairingCode.Should().MatchRegex("^[2-9A-Z]{4}-[2-9A-Z]{4}$");
        result.TtlSecondsRemaining.Should().Be(180);
        result.State.Should().Be(CompanionSessionState.PairingCodeGenerated);
        result.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddSeconds(170));

        var session = await _service.GetSessionStatusAsync(result.SessionId);
        session.Should().NotBeNull();
        session!.PairingCode.Should().Be(result.PairingCode);
        session.State.Should().Be(CompanionSessionState.PairingCodeGenerated);
        session.SecondsRemaining.Should().BeInRange(175, 180);
        session.IsPairingCodeExpired.Should().BeFalse();
    }

    [Fact]
    public async Task RequestPairingCodeAsync_SetsInitialDeviceModelAndTelemetry()
    {
        var result = await _service.RequestPairingCodeAsync(_testTenant, "+21698333444", "Custom Device Lab");
        var session = await _service.GetSessionStatusAsync(result.SessionId);

        session!.DeviceModel.Should().Be("Custom Device Lab");
        session.Telemetry.Should().NotBeNull();
        session.Telemetry.BatteryPercentage.Should().Be(100);
        session.Telemetry.IsCharging.Should().BeTrue();
        session.Telemetry.NetworkType.Should().Be(CompanionNetworkType.WiFi);
        session.Telemetry.PingLatencyMs.Should().Be(12);
    }

    [Fact]
    public async Task RequestPairingCodeAsync_LogsPairingRequestPacket()
    {
        var result = await _service.RequestPairingCodeAsync(_testTenant, "+21698444555");
        var logs = _service.GetPacketLogs(result.SessionId);

        logs.Should().NotBeEmpty();
        logs.Should().Contain(l => l.FrameType == "PAIRING_REQUEST" && l.Direction == "OUTBOUND");
    }

    #endregion

    #region 2. Verification, Noise_XX Handshake & Lockout State Machine Tests

    [Fact]
    public async Task VerifyPairingCodeAsync_ValidCode_CompletesNoiseHandshakeAndDerivesKeys()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698555666");
        var verify = await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, pairing.PairingCode);

        verify.Success.Should().BeTrue();
        verify.State.Should().Be(CompanionSessionState.Connected);
        verify.SessionValidationToken.Should().NotBeNullOrWhiteSpace();
        verify.DerivedAesKeyBase64.Should().NotBeNullOrWhiteSpace();

        var session = await _service.GetSessionStatusAsync(pairing.SessionId);
        session!.State.Should().Be(CompanionSessionState.Connected);
        session.PairingCode.Should().BeNull();
        session.PairingCodeExpiresAtUtc.Should().BeNull();
        session.IdentityKeys.Should().NotBeNull();
        session.EphemeralKeys.Should().NotBeNull();
        session.AesSessionKey.Should().NotBeNull();
        session.AesSessionKey!.Length.Should().Be(32);
        session.SessionValidationToken.Should().Be(verify.SessionValidationToken);
    }

    [Fact]
    public async Task VerifyPairingCodeAsync_ValidCodeWithHyphensAndSpaces_NormalizesAndSucceeds()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698666777");
        var codeWithSpaces = $"  {pairing.PairingCode.ToLowerInvariant()}  ";

        var verify = await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, codeWithSpaces);

        verify.Success.Should().BeTrue();
        verify.State.Should().Be(CompanionSessionState.Connected);
    }

    [Fact]
    public async Task VerifyPairingCodeAsync_InvalidCode_DecrementsAttemptsRemaining()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698777888");
        var verify = await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "WRNG-CODE");

        verify.Success.Should().BeFalse();
        verify.AttemptsRemaining.Should().Be(2);
        verify.Message.Should().Contain("2 attempts remaining");

        var session = await _service.GetSessionStatusAsync(pairing.SessionId);
        session!.FailedVerificationAttempts.Should().Be(1);
        session.IsLockedOut.Should().BeFalse();
        session.State.Should().Be(CompanionSessionState.PairingCodeGenerated);
    }

    [Fact]
    public async Task VerifyPairingCodeAsync_SecondInvalidCode_DecrementsAttemptsRemaining()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698888999");
        await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "WRNG-0001");
        var verify2 = await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "WRNG-0002");

        verify2.Success.Should().BeFalse();
        verify2.AttemptsRemaining.Should().Be(1);
        verify2.Message.Should().Contain("1 attempts remaining");

        var session = await _service.GetSessionStatusAsync(pairing.SessionId);
        session!.FailedVerificationAttempts.Should().Be(2);
        session.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyPairingCodeAsync_ThirdInvalidCode_LocksOutCompanionDevice()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698999000");
        await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "FAIL-0001");
        await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "FAIL-0002");
        var verify3 = await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "FAIL-0003");

        verify3.Success.Should().BeFalse();
        verify3.AttemptsRemaining.Should().Be(0);
        verify3.State.Should().Be(CompanionSessionState.Disconnected);
        verify3.Message.Should().Contain("locked out");

        var session = await _service.GetSessionStatusAsync(pairing.SessionId);
        session!.FailedVerificationAttempts.Should().Be(3);
        session.IsLockedOut.Should().BeTrue();
        session.State.Should().Be(CompanionSessionState.Disconnected);
        session.DisconnectReason.Should().Be("MaxRetriesLockout");
    }

    [Fact]
    public async Task VerifyPairingCodeAsync_SubsequentAttemptWhenLockedOut_RejectedImmediately()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698111000");
        await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "FAIL-1");
        await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "FAIL-2");
        await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, "FAIL-3");

        // Try the actual valid code now that device is locked out
        var verify4 = await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, pairing.PairingCode);
        verify4.Success.Should().BeFalse();
        verify4.Message.Should().Contain("locked out");
    }

    [Fact]
    public async Task VerifyPairingCodeAsync_ExpiredCode_TransitionsToDisconnected()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698222000");

        // Manually simulate expired TTL
        var current = await _service.GetSessionStatusAsync(pairing.SessionId);
        var expired = current! with { PairingCodeExpiresAtUtc = DateTime.UtcNow.AddSeconds(-10) };
        // Save expired state by internal reflection or creating expired scenario
        var field = typeof(WhatsAppCompanionSocketService).GetField("_sessions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var sessionsDict = (System.Collections.Concurrent.ConcurrentDictionary<Guid, CompanionSession>)field!.GetValue(_service)!;
        sessionsDict[pairing.SessionId] = expired;

        var verify = await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, pairing.PairingCode);
        verify.Success.Should().BeFalse();
        verify.State.Should().Be(CompanionSessionState.Disconnected);
        verify.Message.Should().Contain("expired");

        var sessionAfter = await _service.GetSessionStatusAsync(pairing.SessionId);
        sessionAfter!.State.Should().Be(CompanionSessionState.Disconnected);
        sessionAfter.DisconnectReason.Should().Be("PairingCodeTtlExpired");
    }

    [Fact]
    public async Task VerifyPairingCodeAsync_UnknownSessionId_ReturnsNotFoundResult()
    {
        var nonExistent = Guid.NewGuid();
        var verify = await _service.VerifyPairingCodeAsync(_testTenant, nonExistent, "ABCD-EFGH");

        verify.Success.Should().BeFalse();
        verify.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task VerifyPairingCodeAsync_EmptyCode_ReturnsFailure()
    {
        var verify = await _service.VerifyPairingCodeAsync(_testTenant, Guid.NewGuid(), "");
        verify.Success.Should().BeFalse();
        verify.Message.Should().Contain("cannot be empty");
    }

    #endregion

    #region 3. Session Status, Listing & Disconnection Tests

    [Fact]
    public async Task GetSessionStatusAsync_ReturnsLiveSession()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698333000");
        var session = await _service.GetSessionStatusAsync(pairing.SessionId);

        session.Should().NotBeNull();
        session!.SessionId.Should().Be(pairing.SessionId);
        session.PhoneNumber.Should().Be("+21698333000");
    }

    [Fact]
    public async Task GetSessionStatusAsync_NonExistentSession_ReturnsNull()
    {
        var session = await _service.GetSessionStatusAsync(Guid.NewGuid());
        session.Should().BeNull();
    }

    [Fact]
    public async Task GetSessionsForTenantAsync_ReturnsSeededAndCreatedSessions()
    {
        var defaultTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var sessions = await _service.GetSessionsForTenantAsync(defaultTenant);

        sessions.Should().NotBeNull();
        sessions.Count.Should().BeGreaterThanOrEqualTo(3);
        sessions.Should().Contain(s => s.PhoneNumber == "+21671998801");
        sessions.Should().Contain(s => s.PhoneNumber == "+21671998802");
        sessions.Should().Contain(s => s.PhoneNumber == "+21671998803");
    }

    [Fact]
    public async Task DisconnectSessionAsync_ValidSession_TransitionsToDisconnected()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698444000");
        var disconnected = await _service.DisconnectSessionAsync(pairing.SessionId, "OperatorManualShutdown");

        disconnected.Should().BeTrue();
        var session = await _service.GetSessionStatusAsync(pairing.SessionId);
        session!.State.Should().Be(CompanionSessionState.Disconnected);
        session.DisconnectReason.Should().Be("OperatorManualShutdown");

        var logs = _service.GetPacketLogs(pairing.SessionId);
        logs.Should().Contain(l => l.FrameType == "SOCKET_DISCONNECT");
    }

    [Fact]
    public async Task DisconnectSessionAsync_NonExistentSession_ReturnsFalse()
    {
        var result = await _service.DisconnectSessionAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    #endregion

    #region 4. Reconnection Backoff Simulation Tests

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    [InlineData(4, 10)]
    [InlineData(5, 10)]
    [InlineData(20, 10)]
    public void CalculateReconnectionBackoff_EnforcesExponentialStepsAndTenSecondCap(int attempts, int expectedSeconds)
    {
        var backoff = _service.CalculateReconnectionBackoff(attempts);
        backoff.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    #endregion

    #region 5. Noise_XX Cryptography & Key Derivation Tests

    [Fact]
    public void GenerateCurve25519KeyPair_Produces32ByteKeys()
    {
        var keyPair = _service.GenerateCurve25519KeyPair();

        keyPair.Should().NotBeNull();
        keyPair.PublicKey.Length.Should().Be(32);
        keyPair.PrivateKey.Length.Should().Be(32);
        keyPair.PublicKeyBase64.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateCurve25519KeyPair_GeneratesDistinctKeyPairs()
    {
        var pair1 = _service.GenerateCurve25519KeyPair();
        var pair2 = _service.GenerateCurve25519KeyPair();

        pair1.PublicKey.Should().NotEqual(pair2.PublicKey);
        pair1.PrivateKey.Should().NotEqual(pair2.PrivateKey);
    }

    [Fact]
    public void DeriveAes256GcmKey_ComputesDeterministic32ByteKey()
    {
        var priv = new byte[32];
        var remotePub = new byte[32];
        RandomNumberGenerator.Fill(priv);
        RandomNumberGenerator.Fill(remotePub);
        var salt = Encoding.UTF8.GetBytes("WhatsAppNoiseTestSalt");

        var key1 = _service.DeriveAes256GcmKey(priv, remotePub, salt);
        var key2 = _service.DeriveAes256GcmKey(priv, remotePub, salt);

        key1.Length.Should().Be(32);
        key1.Should().Equal(key2);
    }

    [Fact]
    public void DeriveAes256GcmKey_DifferentRemoteKey_ProducesDifferentAesKey()
    {
        var priv = new byte[32];
        var remotePub1 = new byte[32];
        var remotePub2 = new byte[32];
        RandomNumberGenerator.Fill(priv);
        RandomNumberGenerator.Fill(remotePub1);
        RandomNumberGenerator.Fill(remotePub2);
        var salt = Encoding.UTF8.GetBytes("Salt");

        var key1 = _service.DeriveAes256GcmKey(priv, remotePub1, salt);
        var key2 = _service.DeriveAes256GcmKey(priv, remotePub2, salt);

        key1.Should().NotEqual(key2);
    }

    [Fact]
    public void SignSessionToken_CreatesTamperEvidentHmacToken()
    {
        var sessionId = Guid.NewGuid();
        var phone = "+21698123456";
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var token = _service.SignSessionToken(sessionId, phone, key);

        token.Should().NotBeNullOrWhiteSpace();
        token.Should().Contain(".");
        token.Should().StartWith(sessionId.ToString());
    }

    [Fact]
    public void VerifySessionToken_ValidToken_ReturnsTrue()
    {
        var sessionId = Guid.NewGuid();
        var phone = "+21698123456";
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var token = _service.SignSessionToken(sessionId, phone, key);
        var isValid = _service.VerifySessionToken(sessionId, phone, key, token);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifySessionToken_TamperedSignature_ReturnsFalse()
    {
        var sessionId = Guid.NewGuid();
        var phone = "+21698123456";
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var token = _service.SignSessionToken(sessionId, phone, key);
        var tampered = token.Substring(0, token.Length - 4) + "AAAA";

        var isValid = _service.VerifySessionToken(sessionId, phone, key, tampered);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifySessionToken_MismatchedPhone_ReturnsFalse()
    {
        var sessionId = Guid.NewGuid();
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var token = _service.SignSessionToken(sessionId, "+21698111111", key);
        var isValid = _service.VerifySessionToken(sessionId, "+21698999999", key, token);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifySessionToken_MismatchedSessionId_ReturnsFalse()
    {
        var sessionId1 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var token = _service.SignSessionToken(sessionId1, "+21698111111", key);
        var isValid = _service.VerifySessionToken(sessionId2, "+21698111111", key, token);

        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("singletokenwithoutdots")]
    [InlineData("one.two.three.four")]
    public void VerifySessionToken_MalformedToken_ReturnsFalse(string malformed)
    {
        var isValid = _service.VerifySessionToken(Guid.NewGuid(), "+21698111111", new byte[32], malformed);
        isValid.Should().BeFalse();
    }

    #endregion

    #region 6. WABinary Framing & ACK Tracker Tests

    [Fact]
    public void EncodeAndDecodeWABinaryFrame_PreservesAllProperties()
    {
        var originalPayload = Encoding.UTF8.GetBytes("{\"conversation\":\"Hello from WhatsApp Companion Bridge!\"}");
        var frame = new WABinaryFrame(
            Tag: "message",
            FrameId: "WA-FRAME-1001",
            EpochTimestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AckLevel: MessageAckLevel.AckServer,
            Payload: originalPayload,
            Attributes: new Dictionary<string, string>
            {
                ["to"] = "+21698123456@s.whatsapp.net",
                ["type"] = "chat",
                ["notify"] = "SGrowing Automated Engine"
            }
        );

        var encoded = _service.EncodeWABinaryFrame(frame);
        encoded.Should().NotBeNullOrEmpty();
        encoded[0].Should().Be(0x02); // 0x02 prefix

        var decoded = _service.DecodeWABinaryFrame(encoded);
        decoded.Tag.Should().Be(frame.Tag);
        decoded.FrameId.Should().Be(frame.FrameId);
        decoded.EpochTimestamp.Should().Be(frame.EpochTimestamp);
        decoded.AckLevel.Should().Be(frame.AckLevel);
        decoded.Attributes.Should().BeEquivalentTo(frame.Attributes);
        decoded.Payload.Should().Equal(originalPayload);
    }

    [Fact]
    public void DecodeWABinaryFrame_InvalidPrefixByte_ThrowsFormatException()
    {
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A };
        var act = () => _service.DecodeWABinaryFrame(invalidData);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public async Task SimulateMessageAckProgression_ServerToDeviceToRead_SucceedsSequentially()
    {
        var sessionId = Guid.NewGuid();
        var msgId = $"MSG-{Guid.NewGuid():N}";

        // 1. None -> AckServer (Single check)
        var ack1 = await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckServer);
        ack1.Success.Should().BeTrue();
        ack1.CurrentAck.Should().Be(MessageAckLevel.AckServer);

        // 2. AckServer -> AckDevice (Double check)
        var ack2 = await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckDevice);
        ack2.Success.Should().BeTrue();
        ack2.PreviousAck.Should().Be(MessageAckLevel.AckServer);
        ack2.CurrentAck.Should().Be(MessageAckLevel.AckDevice);

        // 3. AckDevice -> AckRead (Blue check)
        var ack3 = await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckRead);
        ack3.Success.Should().BeTrue();
        ack3.PreviousAck.Should().Be(MessageAckLevel.AckDevice);
        ack3.CurrentAck.Should().Be(MessageAckLevel.AckRead);
    }

    [Fact]
    public async Task SimulateMessageAckProgression_BackwardTransition_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var msgId = $"MSG-{Guid.NewGuid():N}";

        await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckRead);

        // Attempt backward transition to AckDevice
        var backward = await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckDevice);
        backward.Success.Should().BeFalse();
        backward.Message.Should().Contain("Cannot transition backwards");
        backward.CurrentAck.Should().Be(MessageAckLevel.AckRead);
    }

    [Fact]
    public async Task SimulateMessageAckProgression_VoiceNotePlayed_WhenIsVoiceNoteTrue_Succeeds()
    {
        var sessionId = Guid.NewGuid();
        var msgId = $"MSG-VN-{Guid.NewGuid():N}";

        await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckRead, isVoiceNote: true);
        var played = await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckPlayed, isVoiceNote: true);

        played.Success.Should().BeTrue();
        played.CurrentAck.Should().Be(MessageAckLevel.AckPlayed);
        played.Message.Should().Contain("voice note");
    }

    [Fact]
    public async Task SimulateMessageAckProgression_VoiceNotePlayed_WhenNotVoiceNote_IsRejected()
    {
        var sessionId = Guid.NewGuid();
        var msgId = $"MSG-TEXT-{Guid.NewGuid():N}";

        await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckRead, isVoiceNote: false);
        var played = await _service.SimulateMessageAckProgressionAsync(sessionId, msgId, MessageAckLevel.AckPlayed, isVoiceNote: false);

        played.Success.Should().BeFalse();
        played.Message.Should().Contain("only permitted for voice note");
    }

    #endregion

    #region 7. Binary Media Encryption & AES-256-GCM Decryption Tests

    [Fact]
    public void EncryptAndDecryptMedia_VoiceNote_RoundtripSucceedsWithValidChecksum()
    {
        var audioBytes = Encoding.UTF8.GetBytes("OggS simulated Opus voice note audio waveform packets...");
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var encrypted = _service.EncryptMediaPayload(audioBytes, "audio/ogg; codecs=opus", key);
        encrypted.CiphertextBase64.Should().NotBeNullOrWhiteSpace();
        encrypted.Sha256Checksum.Should().NotBeNullOrWhiteSpace();

        var payload = new CompanionMediaPayload(
            MimeType: encrypted.MimeType,
            CiphertextBase64: encrypted.CiphertextBase64,
            IvBase64: encrypted.IvBase64,
            TagBase64: encrypted.TagBase64,
            ExpectedSha256Checksum: encrypted.Sha256Checksum
        );

        var decrypted = _service.DecryptMediaPayload(payload, key);
        decrypted.Success.Should().BeTrue();
        decrypted.IsChecksumValid.Should().BeTrue();
        decrypted.DecryptedSizeBytes.Should().Be(audioBytes.Length);

        var decryptedBytes = Convert.FromBase64String(decrypted.DecryptedBase64!);
        decryptedBytes.Should().Equal(audioBytes);
    }

    [Fact]
    public void EncryptAndDecryptMedia_ImagePayload_RoundtripSucceedsWithValidChecksum()
    {
        var jpegBytes = new byte[1024];
        RandomNumberGenerator.Fill(jpegBytes);
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var encrypted = _service.EncryptMediaPayload(jpegBytes, "image/jpeg", key);
        var payload = new CompanionMediaPayload(
            MimeType: "image/jpeg",
            CiphertextBase64: encrypted.CiphertextBase64,
            IvBase64: encrypted.IvBase64,
            TagBase64: encrypted.TagBase64,
            ExpectedSha256Checksum: encrypted.Sha256Checksum
        );

        var decrypted = _service.DecryptMediaPayload(payload, key);
        decrypted.Success.Should().BeTrue();
        decrypted.IsChecksumValid.Should().BeTrue();
        decrypted.ComputedSha256Checksum.Should().Be(encrypted.Sha256Checksum);
    }

    [Fact]
    public void EncryptAndDecryptMedia_VideoPayload_RoundtripSucceedsWithValidChecksum()
    {
        var mp4Bytes = new byte[2048];
        RandomNumberGenerator.Fill(mp4Bytes);
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var encrypted = _service.EncryptMediaPayload(mp4Bytes, "video/mp4", key);
        var payload = new CompanionMediaPayload(
            MimeType: "video/mp4",
            CiphertextBase64: encrypted.CiphertextBase64,
            IvBase64: encrypted.IvBase64,
            TagBase64: encrypted.TagBase64,
            ExpectedSha256Checksum: encrypted.Sha256Checksum
        );

        var decrypted = _service.DecryptMediaPayload(payload, key);
        decrypted.Success.Should().BeTrue();
        decrypted.IsChecksumValid.Should().BeTrue();
    }

    [Fact]
    public void EncryptAndDecryptMedia_DocumentPayload_RoundtripSucceedsWithValidChecksum()
    {
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Invoice #TN-2026-9999 Cash on Delivery");
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var encrypted = _service.EncryptMediaPayload(pdfBytes, "application/pdf", key);
        var payload = new CompanionMediaPayload(
            MimeType: "application/pdf",
            CiphertextBase64: encrypted.CiphertextBase64,
            IvBase64: encrypted.IvBase64,
            TagBase64: encrypted.TagBase64,
            ExpectedSha256Checksum: encrypted.Sha256Checksum,
            FileName: "invoice.pdf"
        );

        var decrypted = _service.DecryptMediaPayload(payload, key);
        decrypted.Success.Should().BeTrue();
        decrypted.IsChecksumValid.Should().BeTrue();
        Convert.FromBase64String(decrypted.DecryptedBase64!).Should().Equal(pdfBytes);
    }

    [Fact]
    public void DecryptMediaPayload_CorruptedTag_FailsDecryptionGracefully()
    {
        var data = Encoding.UTF8.GetBytes("Secret payload");
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var encrypted = _service.EncryptMediaPayload(data, "application/pdf", key);
        var badTag = Convert.ToBase64String(new byte[16]); // All zeros tag

        var payload = new CompanionMediaPayload(
            MimeType: "application/pdf",
            CiphertextBase64: encrypted.CiphertextBase64,
            IvBase64: encrypted.IvBase64,
            TagBase64: badTag,
            ExpectedSha256Checksum: encrypted.Sha256Checksum
        );

        var result = _service.DecryptMediaPayload(payload, key);
        result.Success.Should().BeFalse();
        result.IsChecksumValid.Should().BeFalse();
        result.Error.Should().Contain("Authentication Tag mismatch");
    }

    [Fact]
    public void DecryptMediaPayload_ChecksumMismatch_ReturnsIsChecksumValidFalse()
    {
        var data = Encoding.UTF8.GetBytes("Secret payload");
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);

        var encrypted = _service.EncryptMediaPayload(data, "image/jpeg", key);
        var falseChecksum = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        var payload = new CompanionMediaPayload(
            MimeType: "image/jpeg",
            CiphertextBase64: encrypted.CiphertextBase64,
            IvBase64: encrypted.IvBase64,
            TagBase64: encrypted.TagBase64,
            ExpectedSha256Checksum: falseChecksum
        );

        var result = _service.DecryptMediaPayload(payload, key);
        result.Success.Should().BeTrue();
        result.IsChecksumValid.Should().BeFalse();
        result.ExpectedSha256Checksum.Should().Be(falseChecksum);
        result.ComputedSha256Checksum.Should().Be(encrypted.Sha256Checksum);
    }

    [Fact]
    public async Task DecryptSessionMediaAsync_ValidSessionKey_AppendsPacketLog()
    {
        var pairing = await _service.RequestPairingCodeAsync(_testTenant, "+21698111999");
        await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, pairing.PairingCode);

        var session = await _service.GetSessionStatusAsync(pairing.SessionId);
        var mediaBytes = Encoding.UTF8.GetBytes("Voice message audio note");
        var encrypted = _service.EncryptMediaPayload(mediaBytes, "audio/ogg; codecs=opus", session!.AesSessionKey);

        var payload = new CompanionMediaPayload(
            MimeType: "audio/ogg; codecs=opus",
            CiphertextBase64: encrypted.CiphertextBase64,
            IvBase64: encrypted.IvBase64,
            TagBase64: encrypted.TagBase64,
            ExpectedSha256Checksum: encrypted.Sha256Checksum
        );

        var decrypted = await _service.DecryptSessionMediaAsync(pairing.SessionId, payload);
        decrypted.Success.Should().BeTrue();
        decrypted.IsChecksumValid.Should().BeTrue();

        var logs = _service.GetPacketLogs(pairing.SessionId);
        logs.Should().Contain(l => l.FrameType == "MEDIA_CIPHER");
    }

    #endregion

    #region 8. Device Telemetry & Pre-Seeded Sessions Tests

    [Fact]
    public async Task Telemetry_RefreshTelemetryAsync_UpdatesPingAndBatteryWithinBounds()
    {
        var id1 = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var initial = await _service.GetTelemetryAsync(id1);
        initial.Should().NotBeNull();

        var refreshed = await _service.RefreshTelemetryAsync(id1);
        refreshed.Should().NotBeNull();
        refreshed.BatteryPercentage.Should().BeInRange(0, 100);
        refreshed.PingLatencyMs.Should().BeInRange(10, 120);
        refreshed.LastHeartbeatUtc.Should().BeAfter(DateTime.UtcNow.AddSeconds(-2));
    }

    [Fact]
    public async Task PreSeededSessions_ContainsAllThreeDefaultProductionLines()
    {
        var id1 = Guid.Parse("11111111-2222-3333-4444-555555555551");
        var id2 = Guid.Parse("11111111-2222-3333-4444-555555555552");
        var id3 = Guid.Parse("11111111-2222-3333-4444-555555555553");

        var session1 = await _service.GetSessionStatusAsync(id1);
        var session2 = await _service.GetSessionStatusAsync(id2);
        var session3 = await _service.GetSessionStatusAsync(id3);

        session1.Should().NotBeNull();
        session1!.PhoneNumber.Should().Be("+21671998801");
        session1.State.Should().Be(CompanionSessionState.Connected);
        session1.DeviceModel.Should().Contain("Primary Support Desk Line");

        session2.Should().NotBeNull();
        session2!.PhoneNumber.Should().Be("+21671998802");
        session2.State.Should().Be(CompanionSessionState.Syncing);
        session2.DeviceModel.Should().Contain("VIP Account Manager Companion");

        session3.Should().NotBeNull();
        session3!.PhoneNumber.Should().Be("+21671998803");
        session3.State.Should().Be(CompanionSessionState.Connected);
        session3.DeviceModel.Should().Contain("Automated Inbound Dispatcher");
    }

    #endregion

    #region 9. Thread-Safety & Concurrent Handshake Stress Test

    [Fact]
    public async Task ThreadSafety_SimultaneousHandshakes_Handles50ConcurrentUsersWithoutCollision()
    {
        var tasks = Enumerable.Range(100, 50).Select(async i =>
        {
            var phone = $"+21698{i:D6}";
            var pairing = await _service.RequestPairingCodeAsync(_testTenant, phone, $"LoadTester-{i}");
            pairing.Should().NotBeNull();
            pairing.PairingCode.Should().NotBeNullOrWhiteSpace();

            var verify = await _service.VerifyPairingCodeAsync(_testTenant, pairing.SessionId, pairing.PairingCode);
            verify.Success.Should().BeTrue();
            verify.State.Should().Be(CompanionSessionState.Connected);

            var session = await _service.GetSessionStatusAsync(pairing.SessionId);
            session.Should().NotBeNull();
            session!.State.Should().Be(CompanionSessionState.Connected);
            session.AesSessionKey.Should().NotBeNull();

            return pairing.SessionId;
        });

        var sessionIds = await Task.WhenAll(tasks);
        sessionIds.Length.Should().Be(50);
        sessionIds.Distinct().Count().Should().Be(50, "Every concurrent session ID must be unique.");
    }

    #endregion
}
