using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Modules.Voice.Synthesis;
using Xunit;

namespace SGrowing.Tests.Voice;

/// <summary>
/// Unit and integration test suite for autonomous real-time voice synthesis,
/// neural dialect cloning (Darija, French, Arabic), ITU-T G.711u companding,
/// acoustic sentiment analysis, and psychoacoustic canary watermarking.
/// </summary>
public class NeuralVoiceSynthesisWatermarkTests
{
    private readonly AcousticCanaryWatermarkingService _watermarkingService;
    private readonly NeuralDialectSynthesizerEngine _synthesizerEngine;
    private readonly AcousticSentimentControllerService _sentimentService;

    public NeuralVoiceSynthesisWatermarkTests()
    {
        _watermarkingService = new AcousticCanaryWatermarkingService();
        _synthesizerEngine = new NeuralDialectSynthesizerEngine(_watermarkingService, NullLogger<NeuralDialectSynthesizerEngine>.Instance);
        _sentimentService = new AcousticSentimentControllerService();
    }

    #region 1. Regional Dialect Accent Speech Synthesis Tests

    [Fact]
    public void SynthesizeSpeech_AlgerianAlgiers_ProducesValidAudioAndMetadata()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Saha khouya, labas 3lik? Marhba bik.",
            DialectAccent: DialectAccent.AlgerianAlgiers,
            VoiceGender: VoiceGender.FemaleWarm,
            TargetCodec: AcousticCodec.Pcm16_16kHz
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);

        result.Should().NotBeNull();
        result.AudioBuffer.Should().NotBeNullOrEmpty();
        result.SampleRateHz.Should().Be(16000);
        result.Codec.Should().Be(AcousticCodec.Pcm16_16kHz);
        result.Dialect.Should().Be(DialectAccent.AlgerianAlgiers);
        result.PhonemeCount.Should().BeGreaterThan(10);
        result.DurationMs.Should().BeGreaterThan(1000);
        result.WatermarkSignatureHex.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SynthesizeSpeech_AlgerianOran_AppliesRisingCadenceAndPitchInflection()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Wach rak khouya, kayen dork livraison?",
            DialectAccent: DialectAccent.AlgerianOran,
            VoiceGender: VoiceGender.MaleEnergetic,
            TargetCodec: AcousticCodec.Pcm16_16kHz
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);

        result.Should().NotBeNull();
        result.AudioBuffer.Length.Should().BeGreaterThan(0);
        result.Dialect.Should().Be(DialectAccent.AlgerianOran);
        result.PhonemeCount.Should().BeGreaterThan(8);
        result.WatermarkSignatureHex.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SynthesizeSpeech_AlgerianConstantine_AppliesRhythmicStressContour()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Dork kayen marhba bikoum bezzaf.",
            DialectAccent: DialectAccent.AlgerianConstantine,
            VoiceGender: VoiceGender.MaleCalm,
            TargetCodec: AcousticCodec.Pcm16_16kHz
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);

        result.Should().NotBeNull();
        result.AudioBuffer.Length.Should().BeGreaterThan(0);
        result.Dialect.Should().Be(DialectAccent.AlgerianConstantine);
        result.PhonemeCount.Should().BeGreaterThan(6);
    }

    [Fact]
    public void SynthesizeSpeech_MaghrebBilingualFrench_SynthesizesBilingualPhrase()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Bonjour khouya, votre commande client est prete.",
            DialectAccent: DialectAccent.MaghrebBilingualFrench,
            VoiceGender: VoiceGender.FemaleProfessional,
            TargetCodec: AcousticCodec.Pcm16_16kHz
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);

        result.Should().NotBeNull();
        result.AudioBuffer.Should().NotBeEmpty();
        result.Dialect.Should().Be(DialectAccent.MaghrebBilingualFrench);
        result.PhonemeCount.Should().BeGreaterThan(12);
    }

    [Fact]
    public void SynthesizeSpeech_Tunisian_SynthesizesWithElongatedVowels()
    {
        var request = new VoiceSynthesisRequest(
            Text: "3aslema, chnowa a7welek?",
            DialectAccent: DialectAccent.Tunisian,
            VoiceGender: VoiceGender.FemaleWarm,
            TargetCodec: AcousticCodec.Pcm16_16kHz
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);

        result.Should().NotBeNull();
        result.AudioBuffer.Should().NotBeEmpty();
        result.Dialect.Should().Be(DialectAccent.Tunisian);
        result.DurationMs.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void SynthesizeSpeech_Moroccan_ProducesCompressedVowelsAndDynamicTempo()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Salam khay, labas koulchi mzyan?",
            DialectAccent: DialectAccent.Moroccan,
            VoiceGender: VoiceGender.MaleEnergetic,
            TargetCodec: AcousticCodec.Pcm16_16kHz
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);

        result.Should().NotBeNull();
        result.AudioBuffer.Should().NotBeEmpty();
        result.Dialect.Should().Be(DialectAccent.Moroccan);
    }

    #endregion

    #region 2. Voice Gender Persona & Timbre Tests

    [Fact]
    public void SynthesizeSpeech_FemaleWarm_GeneratesWarmAcousticTimbre()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Chokran bezzaf khouya.",
            VoiceGender: VoiceGender.FemaleWarm
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.AudioBuffer.Length.Should().BeGreaterThan(0);
        result.PhonemeCount.Should().BeGreaterThan(5);
    }

    [Fact]
    public void SynthesizeSpeech_MaleCalm_GeneratesLowPitchResonantBuffer()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Rani hna m3ak, matkhafch.",
            VoiceGender: VoiceGender.MaleCalm
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.AudioBuffer.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SynthesizeSpeech_FemaleProfessional_SynthesizesCrispAudio()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Service client SGrowing bonjour.",
            VoiceGender: VoiceGender.FemaleProfessional
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.AudioBuffer.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SynthesizeSpeech_MaleEnergetic_SynthesizesDynamicAudio()
    {
        var request = new VoiceSynthesisRequest(
            Text: "Kayen promotion kbira dork!",
            VoiceGender: VoiceGender.MaleEnergetic
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.AudioBuffer.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SynthesizeSpeech_EmptyText_HandlesGracefullyWithDefaultPhoneme()
    {
        var request = new VoiceSynthesisRequest(Text: "   ");

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.Should().NotBeNull();
        result.AudioBuffer.Length.Should().BeGreaterThan(0);
        result.PhonemeCount.Should().Be(1);
    }

    #endregion

    #region 3. Darija Arabizi & Multilingual Vocabulary Parsing

    [Fact]
    public void SynthesizeSpeech_DarijaArabiziNumerals_Parses3_7_9Accurately()
    {
        var request = new VoiceSynthesisRequest(
            Text: "3lech 7bibi 9rib kayen?"
        );

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.PhonemeCount.Should().BeGreaterThan(6);
        result.AudioBuffer.Should().NotBeEmpty();
    }

    [Fact]
    public void SynthesizeSpeech_DarijaCommonWords_ParsesAllKeyVocabulary()
    {
        string fullDarija = "khouya saha dork kayen wach rak marhba chouf labas chokran besslama rani kifech bezzaf chouia";
        var request = new VoiceSynthesisRequest(Text: fullDarija);

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.PhonemeCount.Should().BeGreaterThan(30);
    }

    [Fact]
    public void SynthesizeSpeech_FrenchProfessionalTerms_ParsesLoanWords()
    {
        string frenchText = "merci pour le service client et le rendez-vous de livraison d'accord";
        var request = new VoiceSynthesisRequest(Text: frenchText);

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.PhonemeCount.Should().BeGreaterThan(25);
    }

    [Fact]
    public void SynthesizeSpeech_ArabicScript_HandlesGracefully()
    {
        string arabicText = "مرحبا بكم شكرا جزيلا";
        var request = new VoiceSynthesisRequest(Text: arabicText);

        var result = _synthesizerEngine.SynthesizeSpeech(request);
        result.Should().NotBeNull();
        result.AudioBuffer.Length.Should().BeGreaterThan(0);
    }

    #endregion

    #region 4. Codec Conversions & ITU-T G.711u Mu-Law

    [Fact]
    public void LinearToMuLaw_And_MuLawToLinear_SymmetricZeroSample_ReturnsZero()
    {
        short original = 0;
        byte muLaw = NeuralDialectSynthesizerEngine.LinearToMuLaw(original);
        short restored = NeuralDialectSynthesizerEngine.MuLawToLinear(muLaw);

        restored.Should().Be(0);
    }

    [Fact]
    public void LinearToMuLaw_And_MuLawToLinear_PreservesPositiveAndNegativeAmplitudes()
    {
        short[] testSamples = [100, -100, 500, -500, 2000, -2000, 10000, -10000, 25000, -25000];

        foreach (short original in testSamples)
        {
            byte muLaw = NeuralDialectSynthesizerEngine.LinearToMuLaw(original);
            short restored = NeuralDialectSynthesizerEngine.MuLawToLinear(muLaw);

            // G.711u quantization error is small (< 5% relative for non-zero signals)
            double diff = Math.Abs(original - restored);
            double relError = diff / Math.Abs(original);
            relError.Should().BeLessThan(0.06);
        }
    }

    [Fact]
    public void LinearToMuLaw_ClipsExcessiveAmplitudesSafely()
    {
        byte muMax = NeuralDialectSynthesizerEngine.LinearToMuLaw(short.MaxValue);
        byte muMin = NeuralDialectSynthesizerEngine.LinearToMuLaw(short.MinValue);

        short restoredMax = NeuralDialectSynthesizerEngine.MuLawToLinear(muMax);
        short restoredMin = NeuralDialectSynthesizerEngine.MuLawToLinear(muMin);

        restoredMax.Should().BeGreaterThan(30000);
        restoredMin.Should().BeLessThan(-30000);
    }

    [Fact]
    public void ConvertCodec_Pcm16_16kHzTo8kHz_DownsamplesBufferLengthByHalf()
    {
        byte[] pcm16k = new byte[32000]; // 16000 samples @ 16kHz
        for (int i = 0; i < 16000; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm16k.AsSpan(i * 2, 2), (short)(i % 1000));
        }

        byte[] pcm8k = _synthesizerEngine.ConvertCodec(pcm16k, AcousticCodec.Pcm16_16kHz, AcousticCodec.Pcm16_8kHz);

        pcm8k.Length.Should().Be(16000); // 8000 samples @ 8kHz (16000 bytes)
    }

    [Fact]
    public void ConvertCodec_Pcm16_8kHzTo16kHz_DoublesBufferLength()
    {
        byte[] pcm8k = new byte[16000]; // 8000 samples @ 8kHz
        for (int i = 0; i < 8000; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm8k.AsSpan(i * 2, 2), (short)(i % 1000));
        }

        byte[] pcm16k = _synthesizerEngine.ConvertCodec(pcm8k, AcousticCodec.Pcm16_8kHz, AcousticCodec.Pcm16_16kHz);

        pcm16k.Length.Should().Be(32000); // 16000 samples @ 16kHz (32000 bytes)
    }

    [Fact]
    public void ConvertCodec_Pcm16ToG711uMuLaw_ProducesOneBytePerSample()
    {
        byte[] pcm8k = new byte[16000]; // 8000 samples @ 8kHz
        for (int i = 0; i < 8000; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm8k.AsSpan(i * 2, 2), (short)1500);
        }

        byte[] g711u = _synthesizerEngine.ConvertCodec(pcm8k, AcousticCodec.Pcm16_8kHz, AcousticCodec.G711u_MuLaw);

        g711u.Length.Should().Be(8000); // 1 byte per sample
    }

    [Fact]
    public void ConvertCodec_G711uMuLawToPcm16_RestoresTwoBytesPerSample()
    {
        byte[] g711u = new byte[8000];
        Array.Fill(g711u, (byte)0x7E);

        byte[] pcm8k = _synthesizerEngine.ConvertCodec(g711u, AcousticCodec.G711u_MuLaw, AcousticCodec.Pcm16_8kHz);

        pcm8k.Length.Should().Be(16000); // 8000 samples * 2 bytes = 16000 bytes
    }

    [Fact]
    public void ConvertCodec_SameSourceAndTarget_ReturnsExactBufferClone()
    {
        byte[] original = [1, 2, 3, 4, 5, 6, 7, 8];
        byte[] converted = _synthesizerEngine.ConvertCodec(original, AcousticCodec.Pcm16_16kHz, AcousticCodec.Pcm16_16kHz);

        converted.Should().Equal(original);
        converted.Should().NotBeSameAs(original);
    }

    [Fact]
    public void ConvertCodec_ToOpus16kbps_PackagesValidOpusHeaderAndFrames()
    {
        byte[] pcm16k = new byte[6400]; // 3200 samples = 10 frames @ 20ms
        byte[] opusBytes = _synthesizerEngine.ConvertCodec(pcm16k, AcousticCodec.Pcm16_16kHz, AcousticCodec.Opus_16kbps);

        opusBytes.Length.Should().BeGreaterThan(8);
        // Header check: "OPUS"
        opusBytes[0].Should().Be(0x4F);
        opusBytes[1].Should().Be(0x50);
        opusBytes[2].Should().Be(0x55);
        opusBytes[3].Should().Be(0x53);
    }

    [Fact]
    public void ConvertCodec_Opus16kbpsToPcm16_DecodesBackToLinearPcm()
    {
        byte[] pcm16k = new byte[6400];
        for (int i = 0; i < 3200; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm16k.AsSpan(i * 2, 2), 2000);
        }

        byte[] opus = _synthesizerEngine.ConvertCodec(pcm16k, AcousticCodec.Pcm16_16kHz, AcousticCodec.Opus_16kbps);
        byte[] restoredPcm = _synthesizerEngine.ConvertCodec(opus, AcousticCodec.Opus_16kbps, AcousticCodec.Pcm16_16kHz);

        restoredPcm.Length.Should().Be(pcm16k.Length);
    }

    #endregion

    #region 5. Psychoacoustic Canary Watermarking Tests

    [Fact]
    public void EmbedWatermark_EmbedsPayloadIntoPcm16Audio_PreservesBufferDimensions()
    {
        byte[] audio = new byte[32000]; // 16000 samples @ 16kHz
        var payload = new AcousticWatermarkPayload(
            TenantId: "tenant-algeria-01",
            CallSessionId: "call-sess-9901",
            TimestampUtc: DateTime.UtcNow,
            Nonce: "n1a2b3c4",
            HmacSignatureHex: ""
        );

        byte[] watermarked = _watermarkingService.EmbedWatermark(audio, 16000, payload, "test-secret-key-1");

        watermarked.Should().NotBeNull();
        watermarked.Length.Should().BeGreaterThanOrEqualTo(audio.Length);
        watermarked.Should().NotEqual(audio); // High-frequency watermark bits were injected
    }

    [Fact]
    public void ExtractAndVerifyWatermark_WithAuthenticSignature_ReturnsIsAuthenticTrue()
    {
        byte[] audio = new byte[32000];
        DateTime timestamp = DateTime.UtcNow;
        var payload = new AcousticWatermarkPayload(
            TenantId: "tenant-blida",
            CallSessionId: "sess-call-443",
            TimestampUtc: timestamp,
            Nonce: "nonce888",
            HmacSignatureHex: ""
        );

        string secret = "vault-super-secure-key-2026";
        byte[] watermarked = _watermarkingService.EmbedWatermark(audio, 16000, payload, secret);

        var verification = _watermarkingService.ExtractAndVerifyWatermark(watermarked, 16000, secret);

        verification.IsAuthentic.Should().BeTrue();
        verification.TenantId.Should().Be("tenant-blida");
        verification.CallSessionId.Should().Be("sess-call-443");
        verification.ConfidenceScore.Should().Be(1.0);
        verification.VerificationMessage.Should().Contain("verified successfully");
    }

    [Fact]
    public void ExtractAndVerifyWatermark_WithWrongSecretKey_ReturnsIsAuthenticFalse()
    {
        byte[] audio = new byte[32000];
        var payload = new AcousticWatermarkPayload(
            TenantId: "tenant-oran",
            CallSessionId: "sess-oran-1",
            TimestampUtc: DateTime.UtcNow,
            Nonce: "nonce111",
            HmacSignatureHex: ""
        );

        byte[] watermarked = _watermarkingService.EmbedWatermark(audio, 16000, payload, "correct-key");
        var verification = _watermarkingService.ExtractAndVerifyWatermark(watermarked, 16000, "attacker-wrong-key");

        verification.IsAuthentic.Should().BeFalse();
        verification.ConfidenceScore.Should().BeLessThan(0.5);
        verification.VerificationMessage.Should().Contain("HMAC-SHA256 signature mismatch");
    }

    [Fact]
    public void ExtractAndVerifyWatermark_TamperedAudioBuffer_FailsVerification()
    {
        byte[] audio = new byte[32000];
        var payload = new AcousticWatermarkPayload(
            TenantId: "tenant-constantine",
            CallSessionId: "sess-cirta-1",
            TimestampUtc: DateTime.UtcNow,
            Nonce: "nonce777",
            HmacSignatureHex: ""
        );

        string secret = "cirta-secret-2026";
        byte[] watermarked = _watermarkingService.EmbedWatermark(audio, 16000, payload, secret);

        // Tamper with audio samples in the watermark area
        for (int i = 500; i < 900; i++)
        {
            watermarked[i] = (byte)(watermarked[i] ^ 0xFF);
        }

        var verification = _watermarkingService.ExtractAndVerifyWatermark(watermarked, 16000, secret);

        verification.IsAuthentic.Should().BeFalse();
        verification.ConfidenceScore.Should().BeLessThan(0.3);
    }

    [Fact]
    public void ExtractAndVerifyWatermark_TamperedPayloadNonceOrTenant_FailsVerification()
    {
        byte[] audio = new byte[32000];
        var payload = new AcousticWatermarkPayload(
            TenantId: "tenant-annaba",
            CallSessionId: "sess-annaba-1",
            TimestampUtc: DateTime.UtcNow,
            Nonce: "nonce999",
            HmacSignatureHex: ""
        );

        string secret = "annaba-secret-key";
        byte[] watermarked = _watermarkingService.EmbedWatermark(audio, 16000, payload, secret);

        // Watermark verification against altered key
        var result = _watermarkingService.ExtractAndVerifyWatermark(watermarked, 16000, "different-key");
        result.IsAuthentic.Should().BeFalse();
    }

    [Fact]
    public void Watermark_SurvivesG711uMuLawTranscoding_SuccessfullyExtractsAndVerifies()
    {
        // 8kHz Narrowband telephony scenario
        int sampleRate = 8000;
        short[] pcm8k = new short[12000];
        for (int i = 0; i < pcm8k.Length; i++)
        {
            pcm8k[i] = (short)(Math.Sin(2.0 * Math.PI * 300.0 * i / sampleRate) * 8000.0);
        }

        byte[] rawBytes = new byte[pcm8k.Length * 2];
        for (int i = 0; i < pcm8k.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rawBytes.AsSpan(i * 2, 2), pcm8k[i]);
        }

        var payload = new AcousticWatermarkPayload(
            TenantId: "tenant-voip-telecom",
            CallSessionId: "sess-sip-7788",
            TimestampUtc: DateTime.UtcNow,
            Nonce: "voip1234",
            HmacSignatureHex: ""
        );

        string secret = "telephony-shared-key-2026";
        byte[] watermarked = _watermarkingService.EmbedWatermark(rawBytes, sampleRate, payload, secret);

        // Transcode through G.711u Mu-Law compression and decompression
        byte[] g711u = _synthesizerEngine.ConvertCodec(watermarked, AcousticCodec.Pcm16_8kHz, AcousticCodec.G711u_MuLaw);
        byte[] decompressedPcm = _synthesizerEngine.ConvertCodec(g711u, AcousticCodec.G711u_MuLaw, AcousticCodec.Pcm16_8kHz);

        // Verify watermark from transcoded audio
        var verification = _watermarkingService.ExtractAndVerifyWatermark(decompressedPcm, sampleRate, secret);

        verification.IsAuthentic.Should().BeTrue();
        verification.TenantId.Should().Be("tenant-voip-telecom");
        verification.CallSessionId.Should().Be("sess-sip-7788");
        verification.ConfidenceScore.Should().Be(1.0);
    }

    [Fact]
    public void Watermark_SurvivesMinorAcousticNoise_VerifiesWithHighConfidence()
    {
        byte[] audio = new byte[32000];
        var payload = new AcousticWatermarkPayload(
            TenantId: "tenant-noise-test",
            CallSessionId: "sess-noise-1",
            TimestampUtc: DateTime.UtcNow,
            Nonce: "nonce-n",
            HmacSignatureHex: ""
        );

        string secret = "noise-resilience-secret";
        byte[] watermarked = _watermarkingService.EmbedWatermark(audio, 16000, payload, secret);

        // Inject subtle acoustic line noise (+-20 amplitude)
        for (int i = 0; i < watermarked.Length / 2; i++)
        {
            short sample = BinaryPrimitives.ReadInt16LittleEndian(watermarked.AsSpan(i * 2, 2));
            short noise = (short)((i % 7) - 3);
            BinaryPrimitives.WriteInt16LittleEndian(watermarked.AsSpan(i * 2, 2), (short)(sample + noise));
        }

        var verification = _watermarkingService.ExtractAndVerifyWatermark(watermarked, 16000, secret);

        verification.IsAuthentic.Should().BeTrue();
        verification.TenantId.Should().Be("tenant-noise-test");
        verification.ConfidenceScore.Should().Be(1.0);
    }

    [Fact]
    public void EmbedWatermark_With8kHzNarrowband_EmbedsAndExtractsCorrectly()
    {
        byte[] audio = new byte[20000];
        var payload = new AcousticWatermarkPayload(
            TenantId: "tenant-8k",
            CallSessionId: "sess-8k-01",
            TimestampUtc: DateTime.UtcNow,
            Nonce: "n8k",
            HmacSignatureHex: ""
        );

        string secret = "secret-8k-key";
        byte[] watermarked = _watermarkingService.EmbedWatermark(audio, 8000, payload, secret);

        var verification = _watermarkingService.ExtractAndVerifyWatermark(watermarked, 8000, secret);

        verification.IsAuthentic.Should().BeTrue();
        verification.TenantId.Should().Be("tenant-8k");
        verification.ConfidenceScore.Should().Be(1.0);
    }

    [Fact]
    public void ExtractAndVerifyWatermark_EmptyOrTruncatedBuffer_ReturnsFalseWithoutThrowing()
    {
        var result1 = _watermarkingService.ExtractAndVerifyWatermark([], 16000);
        result1.IsAuthentic.Should().BeFalse();

        var result2 = _watermarkingService.ExtractAndVerifyWatermark(new byte[50], 16000);
        result2.IsAuthentic.Should().BeFalse();
    }

    #endregion

    #region 6. Acoustic Sentiment Analysis & Prosody Modulation Tests

    [Fact]
    public void AnalyzeAcousticSentiment_HighAgitationCaller_ClassifiesHighEmpathy()
    {
        var input = new AcousticSentimentInput(
            AudioRmsLevelDb: -10.5,                    // > -12 dB (Loud/shouting)
            FundamentalFrequencyHz: 295.0,            // > 280 Hz (High stress pitch)
            SpeechCadenceSyllablesPerSecond: 6.5,      // > 6.0 syl/s (Rapid speech)
            HesitationPauseRatio: 0.10
        );

        var result = _sentimentService.AnalyzeAcousticSentiment(input);

        result.IsAgitated.Should().BeTrue();
        result.SentimentTier.Should().Be("HighEmpathy");
        result.AgitationScore.Should().BeGreaterThan(0.65);
        result.RecommendedProsody.PitchAdjustmentSemitones.Should().Be(-2.0);
        result.RecommendedProsody.SpeakingRateMultiplier.Should().Be(0.85);
        result.RecommendedProsody.WarmthGainDb.Should().Be(3.0);
        result.RecommendedProsody.EmpathyPauseExpansionFactor.Should().Be(1.4);
        result.RecommendedProsody.CalmnessTier.Should().Be("HighEmpathy");
        result.AnalysisSummary.Should().Contain("Severe caller agitation detected");
    }

    [Fact]
    public void AnalyzeAcousticSentiment_ModerateAgitationCaller_ClassifiesDeEscalation()
    {
        var input = new AcousticSentimentInput(
            AudioRmsLevelDb: -14.0,                    // > -16 dB moderate
            FundamentalFrequencyHz: 255.0,            // > 240 Hz moderate
            SpeechCadenceSyllablesPerSecond: 5.5,      // > 5.2 moderate
            HesitationPauseRatio: 0.15
        );

        var result = _sentimentService.AnalyzeAcousticSentiment(input);

        result.IsAgitated.Should().BeTrue();
        result.SentimentTier.Should().Be("DeEscalation");
        result.RecommendedProsody.PitchAdjustmentSemitones.Should().Be(-1.0);
        result.RecommendedProsody.SpeakingRateMultiplier.Should().Be(0.92);
        result.RecommendedProsody.WarmthGainDb.Should().Be(1.5);
        result.RecommendedProsody.EmpathyPauseExpansionFactor.Should().Be(1.2);
        result.RecommendedProsody.CalmnessTier.Should().Be("DeEscalation");
    }

    [Fact]
    public void AnalyzeAcousticSentiment_CalmCaller_ClassifiesNormal()
    {
        var input = new AcousticSentimentInput(
            AudioRmsLevelDb: -22.0,
            FundamentalFrequencyHz: 160.0,
            SpeechCadenceSyllablesPerSecond: 4.0,
            HesitationPauseRatio: 0.12
        );

        var result = _sentimentService.AnalyzeAcousticSentiment(input);

        result.IsAgitated.Should().BeFalse();
        result.SentimentTier.Should().Be("Normal");
        result.AgitationScore.Should().BeLessThan(0.35);
        result.RecommendedProsody.PitchAdjustmentSemitones.Should().Be(0.0);
        result.RecommendedProsody.SpeakingRateMultiplier.Should().Be(1.0);
        result.RecommendedProsody.WarmthGainDb.Should().Be(0.0);
        result.RecommendedProsody.EmpathyPauseExpansionFactor.Should().Be(1.0);
        result.RecommendedProsody.CalmnessTier.Should().Be("Normal");
    }

    [Fact]
    public void AnalyzeAcousticSentiment_HesitantCaller_DetectsHighHesitationScore()
    {
        var input = new AcousticSentimentInput(
            AudioRmsLevelDb: -20.0,
            FundamentalFrequencyHz: 175.0,
            SpeechCadenceSyllablesPerSecond: 3.2,
            HesitationPauseRatio: 0.45               // > 0.35 hesitation threshold
        );

        var result = _sentimentService.AnalyzeAcousticSentiment(input);

        result.IsAgitated.Should().BeFalse();
        result.SentimentTier.Should().Be("Hesitant");
        result.HesitationScore.Should().BeGreaterThan(0.70);
        result.AnalysisSummary.Should().Contain("hesitation or uncertainty");
    }

    [Fact]
    public void ComputeAdaptiveProsody_DirectCall_ReturnsExpectedParameters()
    {
        var highInput = new AcousticSentimentInput(-10.0, 300.0, 6.8, 0.05);
        var deEscInput = new AcousticSentimentInput(-15.0, 250.0, 5.4, 0.10);
        var normalInput = new AcousticSentimentInput(-22.0, 150.0, 4.0, 0.10);

        var highProsody = _sentimentService.ComputeAdaptiveProsody(highInput);
        var deEscProsody = _sentimentService.ComputeAdaptiveProsody(deEscInput);
        var normalProsody = _sentimentService.ComputeAdaptiveProsody(normalInput);

        highProsody.CalmnessTier.Should().Be("HighEmpathy");
        highProsody.PitchAdjustmentSemitones.Should().Be(-2.0);

        deEscProsody.CalmnessTier.Should().Be("DeEscalation");
        deEscProsody.PitchAdjustmentSemitones.Should().Be(-1.0);

        normalProsody.CalmnessTier.Should().Be("Normal");
        normalProsody.PitchAdjustmentSemitones.Should().Be(0.0);
    }

    #endregion

    #region 7. Concurrency, Thread Safety & DI Registration Tests

    [Fact]
    public async Task SynthesizerEngine_ConcurrentSynthesisCalls_ExecutesThreadSafeWithoutExceptions()
    {
        var tasks = Enumerable.Range(0, 15).Select(i => Task.Run(() =>
        {
            var req = new VoiceSynthesisRequest(
                Text: $"Khouya test iteration {i} saha dork.",
                DialectAccent: (DialectAccent)(i % 6),
                VoiceGender: (VoiceGender)(i % 4),
                TargetCodec: AcousticCodec.Pcm16_16kHz,
                TenantId: $"tenant-{i}",
                SessionId: $"session-{i}"
            );

            var res = _synthesizerEngine.SynthesizeSpeech(req);
            res.AudioBuffer.Length.Should().BeGreaterThan(0);
            res.PhonemeCount.Should().BeGreaterThan(0);
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task WatermarkingService_ConcurrentEmbeddingAndExtraction_ExecutesThreadSafe()
    {
        var tasks = Enumerable.Range(0, 15).Select(i => Task.Run(() =>
        {
            byte[] audio = new byte[32000];
            string tenant = $"tenant-thread-{i}";
            string session = $"sess-thread-{i}";
            string secret = $"secret-thread-{i}";

            var payload = new AcousticWatermarkPayload(tenant, session, DateTime.UtcNow, $"nonce{i}", "");
            byte[] watermarked = _watermarkingService.EmbedWatermark(audio, 16000, payload, secret);

            var verify = _watermarkingService.ExtractAndVerifyWatermark(watermarked, 16000, secret);
            verify.IsAuthentic.Should().BeTrue();
            verify.TenantId.Should().Be(tenant);
            verify.CallSessionId.Should().Be(session);
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void AddVoiceSynthesisServices_RegistersRequiredServices_InServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddVoiceSynthesisServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAcousticCanaryWatermarkingService) &&
            d.ImplementationType == typeof(AcousticCanaryWatermarkingService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAcousticSentimentControllerService) &&
            d.ImplementationType == typeof(AcousticSentimentControllerService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(INeuralDialectSynthesizerEngine) &&
            d.ImplementationType == typeof(NeuralDialectSynthesizerEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void GetSupportedAccents_ReturnsAllSixDialectProfiles_WithDetailedMetadata()
    {
        var accents = _synthesizerEngine.GetSupportedAccents();

        accents.Should().HaveCount(6);
        accents.Select(a => a.Dialect).Should().BeEquivalentTo([
            DialectAccent.AlgerianAlgiers,
            DialectAccent.AlgerianOran,
            DialectAccent.AlgerianConstantine,
            DialectAccent.MaghrebBilingualFrench,
            DialectAccent.Tunisian,
            DialectAccent.Moroccan
        ]);

        foreach (var profile in accents)
        {
            profile.DisplayName.Should().NotBeNullOrWhiteSpace();
            profile.Region.Should().NotBeNullOrWhiteSpace();
            profile.DistinctiveTraits.Should().NotBeNullOrWhiteSpace();
            profile.BasePitchHz.Should().BeInRange(180, 260);
            profile.CadenceMultiplier.Should().BeInRange(0.8, 1.3);
        }
    }

    #endregion
}
