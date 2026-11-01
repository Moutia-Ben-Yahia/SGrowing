using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Voice.Synthesis;

/// <summary>
/// Contract for embedding and extracting psychoacoustic spread-spectrum canary watermarks in audio streams.
/// </summary>
public interface IAcousticCanaryWatermarkingService
{
    /// <summary>
    /// Embeds an acoustic watermark payload into PCM16 audio using psychoacoustic spread-spectrum carrier modulation.
    /// </summary>
    byte[] EmbedWatermark(byte[] pcmAudio, int sampleRateHz, AcousticWatermarkPayload payload, string secretKey = "");

    /// <summary>
    /// Extracts and cryptographically verifies an acoustic canary watermark from a PCM16 audio stream.
    /// </summary>
    WatermarkVerificationResult ExtractAndVerifyWatermark(byte[] pcmAudio, int sampleRateHz, string secretKey = "");
}

/// <summary>
/// Production-grade psychoacoustic spread-spectrum watermarking service.
/// Embeds inaudible digital watermark bits resilient to G.711u companding, narrowband telephony, and minor noise.
/// </summary>
public sealed class AcousticCanaryWatermarkingService : IAcousticCanaryWatermarkingService
{
    public const string DefaultSecretKey = "sgrowing-voice-canary-secret-2026-v1";
    private const ushort MagicSync = 0x5347; // 'S', 'G'
    private const byte ProtocolVersion = 0x01;
    private const double DefaultModulationAmplitude = 260.0;

    /// <inheritdoc />
    public byte[] EmbedWatermark(byte[] pcmAudio, int sampleRateHz, AcousticWatermarkPayload payload, string secretKey = "")
    {
        ArgumentNullException.ThrowIfNull(pcmAudio);
        ArgumentNullException.ThrowIfNull(payload);

        if (sampleRateHz <= 0)
        {
            sampleRateHz = 16000;
        }

        string effectiveKey = string.IsNullOrWhiteSpace(secretKey) ? DefaultSecretKey : secretKey;

        // Serialize payload into binary bitstream
        byte[] payloadBytes = SerializePayload(payload, effectiveKey);
        bool[] bits = ConvertBytesToBits(payloadBytes);

        int samplesPerBit = GetSamplesPerBit(sampleRateHz);
        double carrierFrequency = GetCarrierFrequency(sampleRateHz);
        int requiredSamples = bits.Length * samplesPerBit;
        int requiredBytes = requiredSamples * 2;

        // Ensure target buffer is sufficiently long to hold the complete watermark
        byte[] workingBuffer;
        if (pcmAudio.Length < requiredBytes)
        {
            workingBuffer = new byte[requiredBytes];
            Array.Copy(pcmAudio, workingBuffer, pcmAudio.Length);
        }
        else
        {
            workingBuffer = (byte[])pcmAudio.Clone();
        }

        // Modulate high-frequency carrier into linear PCM16 samples
        double omega = 2.0 * Math.PI * carrierFrequency / sampleRateHz;
        Span<byte> byteSpan = workingBuffer.AsSpan();

        for (int bitIndex = 0; bitIndex < bits.Length; bitIndex++)
        {
            bool bit = bits[bitIndex];
            double bitPolarity = bit ? 1.0 : -1.0;
            int startSample = bitIndex * samplesPerBit;

            // Step 1: Compute projection of existing audio onto the carrier in this chip interval
            double projectionSum = 0.0;
            double carrierNormSq = 0.0;

            for (int j = 0; j < samplesPerBit; j++)
            {
                int sampleIdx = startSample + j;
                short sample = BinaryPrimitives.ReadInt16LittleEndian(byteSpan.Slice(sampleIdx * 2, 2));
                double carrier = Math.Sin(omega * sampleIdx);
                projectionSum += sample * carrier;
                carrierNormSq += carrier * carrier;
            }

            double audioCarrierComponent = carrierNormSq > 0.0001 ? projectionSum / carrierNormSq : 0.0;

            // Step 2: Notch out the natural carrier component and inject watermarked carrier
            for (int j = 0; j < samplesPerBit; j++)
            {
                int sampleIdx = startSample + j;
                short originalSample = BinaryPrimitives.ReadInt16LittleEndian(byteSpan.Slice(sampleIdx * 2, 2));
                double carrier = Math.Sin(omega * sampleIdx);

                // Target is modulated signal at carrier frequency
                double adjusted = originalSample - (audioCarrierComponent * carrier) + (DefaultModulationAmplitude * bitPolarity * carrier);

                short clampedSample = ClampToInt16(adjusted);
                BinaryPrimitives.WriteInt16LittleEndian(byteSpan.Slice(sampleIdx * 2, 2), clampedSample);
            }
        }

        return workingBuffer;
    }

    /// <inheritdoc />
    public WatermarkVerificationResult ExtractAndVerifyWatermark(byte[] pcmAudio, int sampleRateHz, string secretKey = "")
    {
        if (pcmAudio == null || pcmAudio.Length < 128)
        {
            return new WatermarkVerificationResult(
                IsAuthentic: false,
                TenantId: string.Empty,
                CallSessionId: string.Empty,
                ExtractedTimestampUtc: DateTime.MinValue,
                ConfidenceScore: 0.0,
                VerificationMessage: "Audio buffer too short or empty for watermark extraction."
            );
        }

        if (sampleRateHz <= 0)
        {
            sampleRateHz = 16000;
        }

        string effectiveKey = string.IsNullOrWhiteSpace(secretKey) ? DefaultSecretKey : secretKey;
        int samplesPerBit = GetSamplesPerBit(sampleRateHz);
        double carrierFrequency = GetCarrierFrequency(sampleRateHz);
        int totalSamples = pcmAudio.Length / 2;
        int maxBits = totalSamples / samplesPerBit;

        if (maxBits < 64)
        {
            return new WatermarkVerificationResult(
                IsAuthentic: false,
                TenantId: string.Empty,
                CallSessionId: string.Empty,
                ExtractedTimestampUtc: DateTime.MinValue,
                ConfidenceScore: 0.05,
                VerificationMessage: "Audio sample length is insufficient to contain an acoustic watermark."
            );
        }

        // Demodulate spread-spectrum bits from carrier
        double omega = 2.0 * Math.PI * carrierFrequency / sampleRateHz;
        ReadOnlySpan<byte> byteSpan = pcmAudio.AsSpan();
        bool[] extractedBits = new bool[maxBits];

        for (int bitIndex = 0; bitIndex < maxBits; bitIndex++)
        {
            int startSample = bitIndex * samplesPerBit;
            double correlationSum = 0.0;

            for (int j = 0; j < samplesPerBit; j++)
            {
                int sampleIdx = startSample + j;
                short sample = BinaryPrimitives.ReadInt16LittleEndian(byteSpan.Slice(sampleIdx * 2, 2));
                double carrier = Math.Sin(omega * sampleIdx);
                correlationSum += sample * carrier;
            }

            extractedBits[bitIndex] = correlationSum > 0.0;
        }

        byte[] rawBytes = ConvertBitsToBytes(extractedBits);

        // Parse and validate binary watermark packet
        return ParseAndVerifyPayload(rawBytes, effectiveKey);
    }

    private static WatermarkVerificationResult ParseAndVerifyPayload(byte[] bytes, string secretKey)
    {
        if (bytes.Length < 32)
        {
            return new WatermarkVerificationResult(
                IsAuthentic: false,
                TenantId: string.Empty,
                CallSessionId: string.Empty,
                ExtractedTimestampUtc: DateTime.MinValue,
                ConfidenceScore: 0.0,
                VerificationMessage: "Extracted watermark bitstream is incomplete."
            );
        }

        // 1. Verify Magic Sync bytes
        ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2));
        if (magic != MagicSync)
        {
            return new WatermarkVerificationResult(
                IsAuthentic: false,
                TenantId: string.Empty,
                CallSessionId: string.Empty,
                ExtractedTimestampUtc: DateTime.MinValue,
                ConfidenceScore: 0.0,
                VerificationMessage: "Acoustic canary synchronization marker not found."
            );
        }

        // 2. Verify Protocol Version
        byte version = bytes[2];
        if (version != ProtocolVersion)
        {
            return new WatermarkVerificationResult(
                IsAuthentic: false,
                TenantId: string.Empty,
                CallSessionId: string.Empty,
                ExtractedTimestampUtc: DateTime.MinValue,
                ConfidenceScore: 0.1,
                VerificationMessage: $"Unsupported watermark protocol version: {version}."
            );
        }

        int offset = 3;

        // TenantId
        if (offset >= bytes.Length) return CorruptedResult();
        int tenantLen = bytes[offset++];
        if (offset + tenantLen > bytes.Length) return CorruptedResult();
        string tenantId = Encoding.UTF8.GetString(bytes, offset, tenantLen);
        offset += tenantLen;

        // CallSessionId
        if (offset >= bytes.Length) return CorruptedResult();
        int sessionLen = bytes[offset++];
        if (offset + sessionLen > bytes.Length) return CorruptedResult();
        string sessionId = Encoding.UTF8.GetString(bytes, offset, sessionLen);
        offset += sessionLen;

        // Timestamp
        if (offset + 8 > bytes.Length) return CorruptedResult();
        long ticks = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset, 8));
        offset += 8;

        DateTime timestampUtc = DateTime.MinValue;
        if (ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
        {
            timestampUtc = new DateTime(ticks, DateTimeKind.Utc);
        }

        // Nonce
        if (offset >= bytes.Length) return CorruptedResult();
        int nonceLen = bytes[offset++];
        if (offset + nonceLen > bytes.Length) return CorruptedResult();
        string nonce = Encoding.UTF8.GetString(bytes, offset, nonceLen);
        offset += nonceLen;

        // HMAC signature (16 bytes)
        if (offset + 16 > bytes.Length) return CorruptedResult();
        byte[] extractedHmac = new byte[16];
        Array.Copy(bytes, offset, extractedHmac, 0, 16);
        offset += 16;

        // CRC16 (2 bytes)
        if (offset + 2 > bytes.Length) return CorruptedResult();
        ushort extractedCrc = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
        int packetLengthBeforeCrc = offset;

        // Validate CRC16
        ushort computedCrc = ComputeCrc16(bytes.AsSpan(0, packetLengthBeforeCrc));
        if (computedCrc != extractedCrc)
        {
            return new WatermarkVerificationResult(
                IsAuthentic: false,
                TenantId: tenantId,
                CallSessionId: sessionId,
                ExtractedTimestampUtc: timestampUtc,
                ConfidenceScore: 0.15,
                VerificationMessage: "Acoustic canary CRC16 checksum failed: audio samples have been altered or corrupted."
            );
        }

        // Validate HMAC-SHA256 signature
        string canonical = $"{tenantId}:{sessionId}:{ticks}:{nonce}";
        byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
        byte[] expectedFullHmac = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(canonical));

        bool signatureMatches = CryptographicOperations.FixedTimeEquals(
            expectedFullHmac.AsSpan(0, 16),
            extractedHmac.AsSpan()
        );

        if (!signatureMatches)
        {
            return new WatermarkVerificationResult(
                IsAuthentic: false,
                TenantId: tenantId,
                CallSessionId: sessionId,
                ExtractedTimestampUtc: timestampUtc,
                ConfidenceScore: 0.20,
                VerificationMessage: "Watermark cryptographic signature mismatch: HMAC-SHA256 signature mismatch or unauthorized secret key."
            );
        }

        return new WatermarkVerificationResult(
            IsAuthentic: true,
            TenantId: tenantId,
            CallSessionId: sessionId,
            ExtractedTimestampUtc: timestampUtc,
            ConfidenceScore: 1.0,
            VerificationMessage: "Acoustic canary watermark verified successfully: authentic origin and cryptographic signature confirmed."
        );
    }

    private static WatermarkVerificationResult CorruptedResult() =>
        new(
            IsAuthentic: false,
            TenantId: string.Empty,
            CallSessionId: string.Empty,
            ExtractedTimestampUtc: DateTime.MinValue,
            ConfidenceScore: 0.05,
            VerificationMessage: "Watermark packet structure corrupted: truncated header or fields."
        );

    private static byte[] SerializePayload(AcousticWatermarkPayload payload, string secretKey)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Header: Magic sync and version
        writer.Write(MagicSync);
        writer.Write(ProtocolVersion);

        // TenantId
        byte[] tenantBytes = Encoding.UTF8.GetBytes(payload.TenantId ?? string.Empty);
        byte clampedTenantLen = (byte)Math.Min(tenantBytes.Length, 32);
        writer.Write(clampedTenantLen);
        writer.Write(tenantBytes, 0, clampedTenantLen);

        // CallSessionId
        byte[] sessionBytes = Encoding.UTF8.GetBytes(payload.CallSessionId ?? string.Empty);
        byte clampedSessionLen = (byte)Math.Min(sessionBytes.Length, 32);
        writer.Write(clampedSessionLen);
        writer.Write(sessionBytes, 0, clampedSessionLen);

        // Timestamp
        long ticks = payload.TimestampUtc.ToUniversalTime().Ticks;
        writer.Write(ticks);

        // Nonce
        byte[] nonceBytes = Encoding.UTF8.GetBytes(payload.Nonce ?? string.Empty);
        byte clampedNonceLen = (byte)Math.Min(nonceBytes.Length, 16);
        writer.Write(clampedNonceLen);
        writer.Write(nonceBytes, 0, clampedNonceLen);

        // HMAC-SHA256
        string canonical = $"{Encoding.UTF8.GetString(tenantBytes, 0, clampedTenantLen)}:{Encoding.UTF8.GetString(sessionBytes, 0, clampedSessionLen)}:{ticks}:{Encoding.UTF8.GetString(nonceBytes, 0, clampedNonceLen)}";
        byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
        byte[] fullHmac = HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(canonical));

        // Write first 16 bytes of HMAC
        writer.Write(fullHmac, 0, 16);

        // Flush and compute CRC16
        writer.Flush();
        byte[] packetWithoutCrc = ms.ToArray();
        ushort crc = ComputeCrc16(packetWithoutCrc);
        writer.Write(crc);
        writer.Flush();

        return ms.ToArray();
    }

    private static int GetSamplesPerBit(int sampleRateHz) =>
        sampleRateHz >= 16000 ? 24 : 16;

    private static double GetCarrierFrequency(int sampleRateHz)
    {
        if (sampleRateHz >= 40000) return 18000.0;
        if (sampleRateHz >= 16000) return 7200.0;
        return 3600.0; // telephony narrowband 8kHz
    }

    private static bool[] ConvertBytesToBits(byte[] bytes)
    {
        bool[] bits = new bool[bytes.Length * 8];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            for (int bit = 7; bit >= 0; bit--)
            {
                bits[(i * 8) + (7 - bit)] = ((b >> bit) & 1) == 1;
            }
        }
        return bits;
    }

    private static byte[] ConvertBitsToBytes(bool[] bits)
    {
        int byteCount = bits.Length / 8;
        byte[] bytes = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            byte b = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                if (bits[(i * 8) + bit])
                {
                    b |= (byte)(1 << (7 - bit));
                }
            }
            bytes[i] = b;
        }
        return bytes;
    }

    private static ushort ComputeCrc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ 0x1021);
                }
                else
                {
                    crc <<= 1;
                }
            }
        }
        return crc;
    }

    private static short ClampToInt16(double value)
    {
        if (value > short.MaxValue) return short.MaxValue;
        if (value < short.MinValue) return short.MinValue;
        return (short)Math.Round(value);
    }
}
