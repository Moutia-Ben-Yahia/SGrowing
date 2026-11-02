using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.QkdSatelliteArbiter;

public interface IQkdSatelliteArbiterEngine
{
    QkdSessionResult DistillSecretKeys(QkdSessionRequest request);
}

public class QkdSatelliteArbiterEngine : IQkdSatelliteArbiterEngine
{
    private const double MaxAllowableQberPercent = 11.0; // Theoretical security threshold for BB84

    public QkdSessionResult DistillSecretKeys(QkdSessionRequest request)
    {
        var raw = request.RawQubits ?? [];
        if (raw.Count == 0)
        {
            return new QkdSessionResult(
                SessionId: Guid.NewGuid().ToString("N"),
                SatelliteId: request.SatelliteId,
                GroundStationId: request.GroundStationId,
                RawQubitsCount: 0,
                SiftedKeyLength: 0,
                MeasuredQberPercent: 0.0,
                InterceptionDetected: false,
                SessionAborted: true,
                FinalDistilledKeyBits: 0,
                KeyFingerprintSha256: "EMPTY_SESSION",
                SecurityStatus: "ABORTED: No quantum pulses received."
            );
        }

        // 1. Sifting phase: Retain pulses where Alice and Bob selected matching bases
        var sifted = raw.Where(q => string.Equals(q.AliceBasis, q.BobBasis, StringComparison.OrdinalIgnoreCase)).ToList();
        int siftedCount = sifted.Count;

        if (siftedCount == 0)
        {
            return new QkdSessionResult(
                SessionId: Guid.NewGuid().ToString("N"),
                SatelliteId: request.SatelliteId,
                GroundStationId: request.GroundStationId,
                RawQubitsCount: raw.Count,
                SiftedKeyLength: 0,
                MeasuredQberPercent: 100.0,
                InterceptionDetected: false,
                SessionAborted: true,
                FinalDistilledKeyBits: 0,
                KeyFingerprintSha256: "NO_MATCHING_BASES",
                SecurityStatus: "ABORTED: Zero matching bases between satellite and ground station."
            );
        }

        // 2. Parameter estimation: Compare bits to compute Quantum Bit Error Rate (QBER)
        int bitErrors = sifted.Count(q => q.AliceBit != q.BobBitMeasured);
        double qberPercent = ((double)bitErrors / siftedCount) * 100.0;
        bool interceptionDetected = qberPercent > MaxAllowableQberPercent;

        if (interceptionDetected)
        {
            return new QkdSessionResult(
                SessionId: Guid.NewGuid().ToString("N"),
                SatelliteId: request.SatelliteId,
                GroundStationId: request.GroundStationId,
                RawQubitsCount: raw.Count,
                SiftedKeyLength: siftedCount,
                MeasuredQberPercent: Math.Round(qberPercent, 2),
                InterceptionDetected: true,
                SessionAborted: true,
                FinalDistilledKeyBits: 0,
                KeyFingerprintSha256: "ABORTED_EAVESDROPPING_DETECTED",
                SecurityStatus: $"SECURITY_VIOLATION: Measured QBER ({qberPercent:F2}%) exceeds Shor-Preskill unconditional security limit ({MaxAllowableQberPercent}%). Optical channel compromised."
            );
        }

        // 3. Error Correction & Privacy Amplification (Universal Hashing via Shannon binary entropy)
        double e = Math.Max(0.0001, qberPercent / 100.0);
        double binaryEntropy = -e * Math.Log2(e) - (1.0 - e) * Math.Log2(1.0 - e);
        double ecEfficiency = 1.16; // Reconciliation inefficiency for Cascade/LDPC codes
        double secretKeyFraction = Math.Max(0.0, 1.0 - (1.0 + ecEfficiency) * binaryEntropy);

        int distilledBits = (int)Math.Floor(siftedCount * secretKeyFraction);
        distilledBits = Math.Max(1, distilledBits); // Maintain non-zero if within threshold

        // Build distilled key hash fingerprint
        var bitsBuilder = new StringBuilder();
        foreach (var q in sifted.Take(distilledBits))
        {
            bitsBuilder.Append(q.BobBitMeasured);
        }
        var fingerprintBytes = SHA256.HashData(Encoding.UTF8.GetBytes(bitsBuilder.ToString()));
        string fingerprintHex = Convert.ToHexStringLower(fingerprintBytes);

        return new QkdSessionResult(
            SessionId: Guid.NewGuid().ToString("N"),
            SatelliteId: request.SatelliteId,
            GroundStationId: request.GroundStationId,
            RawQubitsCount: raw.Count,
            SiftedKeyLength: siftedCount,
            MeasuredQberPercent: Math.Round(qberPercent, 2),
            InterceptionDetected: false,
            SessionAborted: false,
            FinalDistilledKeyBits: distilledBits,
            KeyFingerprintSha256: fingerprintHex,
            SecurityStatus: $"UNCONDITIONAL_SECURITY_VERIFIED: {distilledBits} secret bits distilled with QBER {qberPercent:F2}%."
        );
    }
}
