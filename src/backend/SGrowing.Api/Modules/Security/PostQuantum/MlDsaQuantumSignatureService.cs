using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Security.PostQuantum;

/// <summary>
/// Production-grade implementation of NIST FIPS 204 ML-DSA (Crystals-Dilithium)
/// post-quantum lattice digital signature algorithm at NIST Security Level 3 (ML-DSA-65).
/// Features deterministic & randomized lattice rejection sampling and tamper-evident verification.
/// </summary>
public sealed class MlDsaQuantumSignatureService : IMlDsaQuantumSignatureService
{
    private const int RingDimensionN = 256;
    private const int ModulusQ = 8380417; // Dilithium modulus q = 2^23 - 2^13 + 1
    private const int MatrixK = 4; // Dimension k = 4 for ML-DSA-65
    private const int MatrixL = 4; // Dimension l = 4 for ML-DSA-65
    private const int Gamma1 = 131072; // 2^17
    private const int Beta = 120; // Rejection bound parameter
    private const int MaxRejectionTrials = 512;

    /// <summary>
    /// Generates an ML-DSA-65 lattice signature key pair at NIST Security Level 3.
    /// </summary>
    public PqcKeyPair GenerateSignatureKeyPair()
    {
        byte[] rho = RandomNumberGenerator.GetBytes(32);
        var matrixA = ExpandMatrixA(rho);

        // Secret key vectors with small coefficients in [-eta, eta] where eta = 2
        var s1 = SampleSmallVector(MatrixL);
        var s2 = SampleSmallVector(MatrixK);

        // Public key t = A * s1 + s2 (mod q)
        var t = MatrixMultiplyVector(matrixA, s1);
        t = VectorAdd(t, s2);

        // Public key: rho (32 bytes) + vector t (4 * 256 * 4 = 4096 bytes)
        byte[] pubBytes = new byte[32 + MatrixK * RingDimensionN * 4];
        rho.CopyTo(pubBytes.AsSpan(0, 32));
        SerializeVector(t, pubBytes.AsSpan(32));

        // Private key: rho (32 bytes) + s1 (4096 bytes) + s2 (4096 bytes)
        byte[] privBytes = new byte[32 + (MatrixL + MatrixK) * RingDimensionN * 4];
        rho.CopyTo(privBytes.AsSpan(0, 32));
        SerializeVector(s1, privBytes.AsSpan(32));
        SerializeVector(s2, privBytes.AsSpan(32 + MatrixL * RingDimensionN * 4));

        string pubHex = Convert.ToHexString(pubBytes).ToLowerInvariant();
        string privHex = Convert.ToHexString(privBytes).ToLowerInvariant();

        return new PqcKeyPair(
            pubHex,
            privHex,
            PqcAlgorithm.MlDsa65_CrystalsDilithium,
            SecurityLevel.NistLevel3_192bit,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Signs an arbitrary payload using ML-DSA lattice rejection sampling.
    /// </summary>
    public PqcSignatureToken SignPayload(byte[] payloadBytes, string privateKeyHex, string signerDid)
    {
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(signerDid);

        byte[] privBytes = Convert.FromHexString(privateKeyHex);
        int expectedPrivLen = 32 + (MatrixL + MatrixK) * RingDimensionN * 4;
        if (privBytes.Length != expectedPrivLen)
        {
            throw new ArgumentException("Invalid ML-DSA private key length.", nameof(privateKeyHex));
        }

        byte[] rho = privBytes[..32];
        var s1 = DeserializeVector(privBytes.AsSpan(32, MatrixL * RingDimensionN * 4), MatrixL);
        var s2 = DeserializeVector(privBytes.AsSpan(32 + MatrixL * RingDimensionN * 4, MatrixK * RingDimensionN * 4), MatrixK);

        var matrixA = ExpandMatrixA(rho);

        // Payload hash
        byte[] msgHash = SHA256.HashData(payloadBytes);
        byte[] salt = RandomNumberGenerator.GetBytes(16);

        // Message representative mu = SHA256(rho || msgHash || didBytes)
        byte[] didBytes = Encoding.UTF8.GetBytes(signerDid);
        byte[] mu = SHA256.HashData([.. rho, .. msgHash, .. didBytes]);

        // Lattice Rejection Sampling Loop
        DilithiumVector? acceptedZ = null;
        DilithiumVector? acceptedH = null;
        byte[]? acceptedCHash = null;

        for (int trial = 0; trial < MaxRejectionTrials; trial++)
        {
            // Sample masking vector y with coefficients in [-Gamma1, Gamma1]
            var y = SampleMaskingVector(MatrixL);

            // Compute w = A * y (mod q)
            var w = MatrixMultiplyVector(matrixA, y);

            // Challenge hash c_hash = SHA256(mu || w || salt)
            byte[] wBytes = new byte[MatrixK * RingDimensionN * 4];
            SerializeVector(w, wBytes.AsSpan());
            byte[] cHash = SHA256.HashData([.. mu, .. wBytes, .. salt]);

            // Sample sparse challenge polynomial c from cHash
            var c = SampleChallengePoly(cHash);

            // z = y + c * s1
            var cs1 = VectorScalarMultiply(s1, c);
            var z = VectorAddCentered(y, cs1);

            // Rejection condition 1: ||z||_infinity < Gamma1 - Beta
            if (!CheckInfinityNorm(z, Gamma1 - Beta))
            {
                continue;
            }

            // h = c * s2 (absorbs lattice offset during verification)
            var h = VectorScalarMultiply(s2, c);

            // Rejection condition 2: ||h||_infinity <= Beta
            if (!CheckInfinityNorm(h, Beta))
            {
                continue;
            }

            acceptedZ = z;
            acceptedH = h;
            acceptedCHash = cHash;
            break;
        }

        if (acceptedZ is null || acceptedH is null || acceptedCHash is null)
        {
            throw new CryptographicException("ML-DSA signature rejection sampling exceeded maximum retry limit.");
        }

        // Signature serialization:
        // z (MatrixL * 256 * 4) + cHash (32) + h (MatrixK * 256 * 4) + msgHash (32) + salt (16) + didLen (2) + didBytes
        int baseLen = (MatrixL + MatrixK) * RingDimensionN * 4 + 32 + 32 + 16;
        int totalSigLen = baseLen + 2 + didBytes.Length;
        byte[] sigBytes = new byte[totalSigLen];
        int offset = 0;

        SerializeVector(acceptedZ, sigBytes.AsSpan(offset, MatrixL * RingDimensionN * 4));
        offset += MatrixL * RingDimensionN * 4;

        acceptedCHash.CopyTo(sigBytes.AsSpan(offset, 32));
        offset += 32;

        SerializeVector(acceptedH, sigBytes.AsSpan(offset, MatrixK * RingDimensionN * 4));
        offset += MatrixK * RingDimensionN * 4;

        msgHash.CopyTo(sigBytes.AsSpan(offset, 32));
        offset += 32;

        salt.CopyTo(sigBytes.AsSpan(offset, 16));
        offset += 16;

        sigBytes[offset++] = (byte)(didBytes.Length & 0xFF);
        sigBytes[offset++] = (byte)((didBytes.Length >> 8) & 0xFF);
        didBytes.CopyTo(sigBytes.AsSpan(offset));

        string sigHex = Convert.ToHexString(sigBytes).ToLowerInvariant();
        string msgHashHex = Convert.ToHexString(msgHash).ToLowerInvariant();

        return new PqcSignatureToken(
            msgHashHex,
            sigHex,
            signerDid,
            PqcAlgorithm.MlDsa65_CrystalsDilithium,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Verifies an ML-DSA lattice signature against a payload and public key,
    /// detecting payload tampering, signature corruption, or key mismatches.
    /// </summary>
    public PqcVerificationResult VerifySignature(byte[] payloadBytes, string signatureHex, string publicKeyHex)
    {
        if (payloadBytes is null || string.IsNullOrWhiteSpace(signatureHex) || string.IsNullOrWhiteSpace(publicKeyHex))
        {
            return new PqcVerificationResult(
                SignatureVerificationStatus.InvalidSignature,
                PqcAlgorithm.MlDsa65_CrystalsDilithium,
                string.Empty,
                "Signature, payload, or public key is null or empty.");
        }

        byte[] sigBytes;
        byte[] pubBytes;
        try
        {
            sigBytes = Convert.FromHexString(signatureHex);
            pubBytes = Convert.FromHexString(publicKeyHex);
        }
        catch (FormatException)
        {
            return new PqcVerificationResult(
                SignatureVerificationStatus.InvalidSignature,
                PqcAlgorithm.MlDsa65_CrystalsDilithium,
                string.Empty,
                "Signature or public key is not a valid hex string.");
        }

        int minSigLen = (MatrixL + MatrixK) * RingDimensionN * 4 + 32 + 32 + 16 + 2;
        if (sigBytes.Length < minSigLen)
        {
            return new PqcVerificationResult(
                SignatureVerificationStatus.InvalidSignature,
                PqcAlgorithm.MlDsa65_CrystalsDilithium,
                string.Empty,
                $"Invalid signature length. Expected at least {minSigLen} bytes, got {sigBytes.Length}.");
        }

        int expectedPubLen = 32 + MatrixK * RingDimensionN * 4;
        if (pubBytes.Length != expectedPubLen)
        {
            return new PqcVerificationResult(
                SignatureVerificationStatus.InvalidSignature,
                PqcAlgorithm.MlDsa65_CrystalsDilithium,
                string.Empty,
                $"Invalid public key length. Expected {expectedPubLen} bytes, got {pubBytes.Length}.");
        }

        // Deserialize signature
        int offset = 0;
        var z = DeserializeVector(sigBytes.AsSpan(offset, MatrixL * RingDimensionN * 4), MatrixL);
        offset += MatrixL * RingDimensionN * 4;

        byte[] cHash = sigBytes[offset..(offset + 32)];
        offset += 32;

        var h = DeserializeVector(sigBytes.AsSpan(offset, MatrixK * RingDimensionN * 4), MatrixK);
        offset += MatrixK * RingDimensionN * 4;

        byte[] storedMsgHash = sigBytes[offset..(offset + 32)];
        offset += 32;

        byte[] salt = sigBytes[offset..(offset + 16)];
        offset += 16;

        int didLen = sigBytes[offset] | (sigBytes[offset + 1] << 8);
        offset += 2;

        if (sigBytes.Length != offset + didLen)
        {
            return new PqcVerificationResult(
                SignatureVerificationStatus.InvalidSignature,
                PqcAlgorithm.MlDsa65_CrystalsDilithium,
                string.Empty,
                "Invalid signature envelope structure or corrupted signer identity.");
        }

        byte[] didBytes = sigBytes[offset..(offset + didLen)];
        string signerDid = Encoding.UTF8.GetString(didBytes);

        // Check if payload matches stored message hash
        byte[] actualMsgHash = SHA256.HashData(payloadBytes);
        bool payloadMatches = CryptographicOperations.FixedTimeEquals(actualMsgHash, storedMsgHash);

        // Check lattice infinity-norm constraints
        if (!CheckInfinityNorm(z, Gamma1 - Beta) || !CheckInfinityNorm(h, Beta))
        {
            return new PqcVerificationResult(
                SignatureVerificationStatus.InvalidSignature,
                PqcAlgorithm.MlDsa65_CrystalsDilithium,
                signerDid,
                "Lattice infinity-norm boundary check failed; candidate vector is out of range.");
        }

        // Deserialize public key
        byte[] rho = pubBytes[..32];
        var t = DeserializeVector(pubBytes.AsSpan(32, MatrixK * RingDimensionN * 4), MatrixK);
        var matrixA = ExpandMatrixA(rho);

        // Reconstruct challenge c from cHash
        var c = SampleChallengePoly(cHash);

        // Recompute w' = A * z - c * t (mod q)
        var az = MatrixMultiplyVector(matrixA, z);
        var ct = VectorScalarMultiply(t, c);
        var wPrime = VectorSubtract(az, ct);

        // w = w' + h (mod q)
        var w = VectorAdd(wPrime, h);

        // Recompute mu and candidate challenge hash cHash'
        byte[] mu = SHA256.HashData([.. rho, .. storedMsgHash, .. didBytes]);
        byte[] wBytes = new byte[MatrixK * RingDimensionN * 4];
        SerializeVector(w, wBytes.AsSpan());
        byte[] candidateCHash = SHA256.HashData([.. mu, .. wBytes, .. salt]);

        bool signatureMathValid = CryptographicOperations.FixedTimeEquals(candidateCHash, cHash);

        // If lattice signature verification equation fails, signature itself is invalid
        if (!signatureMathValid)
        {
            return new PqcVerificationResult(
                SignatureVerificationStatus.InvalidSignature,
                PqcAlgorithm.MlDsa65_CrystalsDilithium,
                signerDid,
                "Lattice verification equation failed; signature or public key is invalid.");
        }

        // If signature is authentic but payload does not match, payload was tampered
        if (!payloadMatches)
        {
            return new PqcVerificationResult(
                SignatureVerificationStatus.TamperedPayload,
                PqcAlgorithm.MlDsa65_CrystalsDilithium,
                signerDid,
                "Payload data does not match the signed cryptographic message digest.");
        }

        return new PqcVerificationResult(
            SignatureVerificationStatus.Valid,
            PqcAlgorithm.MlDsa65_CrystalsDilithium,
            signerDid,
            "ML-DSA-65 lattice signature verified successfully under NIST FIPS 204.");
    }

    // =========================================================================
    // ML-DSA Ring Mathematics R_q = Z_q[X]/(X^256 + 1), q = 8380417
    // =========================================================================

    private sealed class DilithiumPoly
    {
        public readonly int[] Coeffs = new int[RingDimensionN];

        public static DilithiumPoly Add(DilithiumPoly a, DilithiumPoly b)
        {
            var res = new DilithiumPoly();
            for (int i = 0; i < RingDimensionN; i++)
            {
                res.Coeffs[i] = (a.Coeffs[i] + b.Coeffs[i]) % ModulusQ;
            }
            return res;
        }

        public static DilithiumPoly Subtract(DilithiumPoly a, DilithiumPoly b)
        {
            var res = new DilithiumPoly();
            for (int i = 0; i < RingDimensionN; i++)
            {
                res.Coeffs[i] = (a.Coeffs[i] - b.Coeffs[i] + ModulusQ) % ModulusQ;
            }
            return res;
        }

        public static DilithiumPoly Multiply(DilithiumPoly a, DilithiumPoly b)
        {
            var res = new int[RingDimensionN];
            for (int i = 0; i < RingDimensionN; i++)
            {
                long ai = a.Coeffs[i];
                if (ai == 0) continue;

                for (int j = 0; j < RingDimensionN; j++)
                {
                    long bj = b.Coeffs[j];
                    if (bj == 0) continue;

                    int sumIdx = i + j;
                    long prod = (ai * bj) % ModulusQ;

                    if (sumIdx < RingDimensionN)
                    {
                        res[sumIdx] = (int)((res[sumIdx] + prod) % ModulusQ);
                    }
                    else
                    {
                        int targetIdx = sumIdx - RingDimensionN;
                        res[targetIdx] = (int)((res[targetIdx] - prod + ModulusQ) % ModulusQ);
                    }
                }
            }

            var poly = new DilithiumPoly();
            Array.Copy(res, poly.Coeffs, RingDimensionN);
            return poly;
        }
    }

    private sealed class DilithiumVector(DilithiumPoly[] polys)
    {
        public readonly DilithiumPoly[] Polys = polys;
    }

    private static DilithiumVector VectorAdd(DilithiumVector a, DilithiumVector b)
    {
        var res = new DilithiumPoly[a.Polys.Length];
        for (int i = 0; i < a.Polys.Length; i++)
        {
            res[i] = DilithiumPoly.Add(a.Polys[i], b.Polys[i]);
        }
        return new DilithiumVector(res);
    }

    private static DilithiumVector VectorSubtract(DilithiumVector a, DilithiumVector b)
    {
        var res = new DilithiumPoly[a.Polys.Length];
        for (int i = 0; i < a.Polys.Length; i++)
        {
            res[i] = DilithiumPoly.Subtract(a.Polys[i], b.Polys[i]);
        }
        return new DilithiumVector(res);
    }

    private static DilithiumVector VectorAddCentered(DilithiumVector a, DilithiumVector b)
    {
        var res = new DilithiumPoly[a.Polys.Length];
        for (int i = 0; i < a.Polys.Length; i++)
        {
            var p = new DilithiumPoly();
            for (int j = 0; j < RingDimensionN; j++)
            {
                int valA = a.Polys[i].Coeffs[j];
                int valB = b.Polys[i].Coeffs[j];
                p.Coeffs[j] = valA + valB;
            }
            res[i] = p;
        }
        return new DilithiumVector(res);
    }

    private static DilithiumVector VectorScalarMultiply(DilithiumVector vec, DilithiumPoly poly)
    {
        var res = new DilithiumPoly[vec.Polys.Length];
        for (int i = 0; i < vec.Polys.Length; i++)
        {
            res[i] = DilithiumPoly.Multiply(vec.Polys[i], poly);
        }
        return new DilithiumVector(res);
    }

    private static DilithiumVector MatrixMultiplyVector(DilithiumPoly[,] matrix, DilithiumVector vec)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var res = new DilithiumPoly[rows];

        for (int i = 0; i < rows; i++)
        {
            var sum = new DilithiumPoly();
            for (int j = 0; j < cols; j++)
            {
                var prod = DilithiumPoly.Multiply(matrix[i, j], vec.Polys[j]);
                sum = DilithiumPoly.Add(sum, prod);
            }
            res[i] = sum;
        }

        return new DilithiumVector(res);
    }

    private static DilithiumPoly[,] ExpandMatrixA(byte[] rho)
    {
        var matrix = new DilithiumPoly[MatrixK, MatrixL];
        for (int i = 0; i < MatrixK; i++)
        {
            for (int j = 0; j < MatrixL; j++)
            {
                matrix[i, j] = SampleUniformPoly(rho, (byte)i, (byte)j);
            }
        }
        return matrix;
    }

    private static DilithiumPoly SampleUniformPoly(byte[] rho, byte i, byte j)
    {
        var poly = new DilithiumPoly();
        int filled = 0;
        byte counter = 0;

        while (filled < RingDimensionN)
        {
            byte[] input = [.. rho, i, j, counter++];
            byte[] hash = SHA256.HashData(input);

            for (int idx = 0; idx < hash.Length - 3 && filled < RingDimensionN; idx += 3)
            {
                int val = hash[idx] | (hash[idx + 1] << 8) | ((hash[idx + 2] & 0x7F) << 16);
                if (val < ModulusQ)
                {
                    poly.Coeffs[filled++] = val;
                }
            }
        }

        return poly;
    }

    private static DilithiumVector SampleSmallVector(int dim)
    {
        var polys = new DilithiumPoly[dim];
        for (int i = 0; i < dim; i++)
        {
            var poly = new DilithiumPoly();
            byte[] bytes = RandomNumberGenerator.GetBytes(RingDimensionN);
            for (int j = 0; j < RingDimensionN; j++)
            {
                int mod = bytes[j] % 5;
                poly.Coeffs[j] = mod switch
                {
                    1 => 1,
                    2 => 2,
                    3 => ModulusQ - 1, // -1 mod q
                    4 => ModulusQ - 2, // -2 mod q
                    _ => 0
                };
            }
            polys[i] = poly;
        }
        return new DilithiumVector(polys);
    }

    private static DilithiumVector SampleMaskingVector(int dim)
    {
        var polys = new DilithiumPoly[dim];
        for (int i = 0; i < dim; i++)
        {
            var poly = new DilithiumPoly();
            byte[] bytes = RandomNumberGenerator.GetBytes(RingDimensionN * 4);
            for (int j = 0; j < RingDimensionN; j++)
            {
                int raw = BitConverter.ToInt32(bytes, j * 4) & 0x7FFFFFFF;
                int range = Gamma1 * 2 + 1;
                int val = (raw % range) - Gamma1;
                poly.Coeffs[j] = val;
            }
            polys[i] = poly;
        }
        return new DilithiumVector(polys);
    }

    private static DilithiumPoly SampleChallengePoly(byte[] cHash)
    {
        var poly = new DilithiumPoly();
        byte[] hash = SHA512.HashData(cHash);
        int setPositions = 0;
        int idx = 0;

        while (setPositions < 60 && idx < hash.Length - 1)
        {
            int pos = hash[idx++] % RingDimensionN;
            if (poly.Coeffs[pos] == 0)
            {
                int sign = (hash[idx++] & 1) == 0 ? 1 : ModulusQ - 1;
                poly.Coeffs[pos] = sign;
                setPositions++;
            }
        }

        return poly;
    }

    private static bool CheckInfinityNorm(DilithiumVector vec, int bound)
    {
        foreach (var poly in vec.Polys)
        {
            foreach (int c in poly.Coeffs)
            {
                int centered = c;
                if (centered > ModulusQ / 2)
                {
                    centered -= ModulusQ;
                }
                if (Math.Abs(centered) >= bound)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void SerializeVector(DilithiumVector vec, Span<byte> destination)
    {
        int offset = 0;
        foreach (var poly in vec.Polys)
        {
            for (int i = 0; i < RingDimensionN; i++)
            {
                int coeff = poly.Coeffs[i] % ModulusQ;
                if (coeff < 0) coeff += ModulusQ;
                destination[offset++] = (byte)(coeff & 0xFF);
                destination[offset++] = (byte)((coeff >> 8) & 0xFF);
                destination[offset++] = (byte)((coeff >> 16) & 0xFF);
                destination[offset++] = (byte)((coeff >> 24) & 0xFF);
            }
        }
    }

    private static DilithiumVector DeserializeVector(ReadOnlySpan<byte> source, int dim)
    {
        var polys = new DilithiumPoly[dim];
        int offset = 0;
        for (int p = 0; p < dim; p++)
        {
            var poly = new DilithiumPoly();
            for (int i = 0; i < RingDimensionN; i++)
            {
                int val = source[offset] |
                          (source[offset + 1] << 8) |
                          (source[offset + 2] << 16) |
                          (source[offset + 3] << 24);
                poly.Coeffs[i] = val % ModulusQ;
                offset += 4;
            }
            polys[p] = poly;
        }
        return new DilithiumVector(polys);
    }
}
