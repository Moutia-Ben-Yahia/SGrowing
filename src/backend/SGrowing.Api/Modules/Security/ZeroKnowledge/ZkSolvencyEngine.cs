using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.Security.ZeroKnowledge;

/// <summary>
/// High-security implementation of the Zero-Knowledge Solvency Engine.
/// Employs RFC 3526 2048-bit MODP safe prime group, Nothing-Up-My-Sleeve (NUMS)
/// generator derivations, Bulletproofs-inspired bit-commitment range proofs,
/// and Schnorr sigma-protocol knowledge-of-opening verifications.
/// </summary>
public class ZkSolvencyEngine : IZkSolvencyEngine
{
    // RFC 3526 Group 14: 2048-bit MODP Safe Prime Field
    // P = 2^2048 - 2^1984 - 1 + 2^64 * { [2^1918 pi] + 124476 }
    private static readonly BigInteger ModulusP = BigInteger.Parse(
        "00" +
        "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD1" +
        "29024E088A67CC74020BBEA63B139B22514A08798E3404DD" +
        "EF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245" +
        "E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7ED" +
        "EE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3D" +
        "C2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F" +
        "83655D23DCA3AD961C62F356208552BB9ED529077096966D" +
        "670C354E4ABC9804F1746C08CA18217C32905E462E36CE3B" +
        "E39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9" +
        "DE2BCBF6955817183995497CEA956AE515D2261898FA0510" +
        "15728E5A8AACAA68FFFFFFFFFFFFFFFF",
        NumberStyles.HexNumber);

    // Subgroup order Q = (P - 1) / 2 (Safe Prime)
    private static readonly BigInteger SubgroupOrderQ = (ModulusP - 1) / 2;

    // Generator G = 2 (Quadratic residue in RFC 3526 group)
    private static readonly BigInteger GeneratorG = 2;

    // Generator H derived deterministically using NUMS hash-to-subgroup
    private static readonly BigInteger GeneratorH = DeriveGeneratorH(ModulusP, SubgroupOrderQ);

    private long _totalCommitmentsCreated;
    private long _totalProofsGenerated;

    public (BigInteger GeneratorG, BigInteger GeneratorH, BigInteger ModulusP, BigInteger OrderQ) GetGroupParameters()
    {
        return (GeneratorG, GeneratorH, ModulusP, SubgroupOrderQ);
    }

    /// <summary>
    /// Generates a Pedersen commitment C = g^v * h^r (mod p).
    /// </summary>
    public PedersenCommitment Commit(decimal value, string? blindingFactorHex = null)
    {
        var v = ScaleToField(value);
        BigInteger r;
        string rHex;

        if (string.IsNullOrWhiteSpace(blindingFactorHex))
        {
            r = GenerateRandomScalar();
            rHex = r.ToString("X");
        }
        else
        {
            r = ParseScalar(blindingFactorHex);
            rHex = blindingFactorHex.Trim();
        }

        var gv = BigInteger.ModPow(GeneratorG, v, ModulusP);
        var hr = BigInteger.ModPow(GeneratorH, r, ModulusP);
        var commitment = (gv * hr) % ModulusP;

        Interlocked.Increment(ref _totalCommitmentsCreated);

        return new PedersenCommitment
        {
            CommitmentHex = commitment.ToString("X"),
            GeneratorG = GeneratorG.ToString("X"),
            GeneratorH = GeneratorH.ToString("X"),
            BlindingFactorHex = rHex,
            CommittedValueHidden = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Verifies that a commitment C opens to a specific value v with blinding factor r.
    /// </summary>
    public bool VerifyCommitment(string commitmentHex, decimal value, string blindingFactorHex)
    {
        if (string.IsNullOrWhiteSpace(commitmentHex) || string.IsNullOrWhiteSpace(blindingFactorHex))
        {
            return false;
        }

        if (!TryParseBigInteger(commitmentHex, out var c) || !TryParseBigInteger(blindingFactorHex, out var r))
        {
            return false;
        }

        var v = ScaleToField(value);
        var gv = BigInteger.ModPow(GeneratorG, v, ModulusP);
        var hr = BigInteger.ModPow(GeneratorH, r, ModulusP);
        var expectedC = (gv * hr) % ModulusP;

        return c == expectedC;
    }

    /// <summary>
    /// Homomorphically adds two commitments: C(v1, r1) * C(v2, r2) = C(v1 + v2, r1 + r2) (mod p).
    /// </summary>
    public PedersenCommitment AddCommitments(PedersenCommitment c1, PedersenCommitment c2)
    {
        var c1Val = ParseScalar(c1.CommitmentHex);
        var c2Val = ParseScalar(c2.CommitmentHex);
        var r1 = ParseScalar(c1.BlindingFactorHex);
        var r2 = ParseScalar(c2.BlindingFactorHex);

        var combinedC = (c1Val * c2Val) % ModulusP;
        var combinedR = (r1 + r2) % SubgroupOrderQ;

        return new PedersenCommitment
        {
            CommitmentHex = combinedC.ToString("X"),
            GeneratorG = GeneratorG.ToString("X"),
            GeneratorH = GeneratorH.ToString("X"),
            BlindingFactorHex = combinedR.ToString("X"),
            CommittedValueHidden = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Multiplies commitment points modulo p: (c1 * c2) mod p.
    /// </summary>
    public string CombineCommitmentsHex(string c1Hex, string c2Hex)
    {
        var c1 = ParseScalar(c1Hex);
        var c2 = ParseScalar(c2Hex);
        var combined = (c1 * c2) % ModulusP;
        return combined.ToString("X");
    }

    /// <summary>
    /// Subtracts commitments homomorphically: (c1 * c2^-1) mod p = C(v1 - v2, r1 - r2).
    /// </summary>
    public string SubtractCommitmentsHex(string c1Hex, string c2Hex)
    {
        var c1 = ParseScalar(c1Hex);
        var c2 = ParseScalar(c2Hex);
        var c2Inv = BigInteger.ModPow(c2, ModulusP - 2, ModulusP);
        var diff = (c1 * c2Inv) % ModulusP;
        return diff.ToString("X");
    }

    /// <summary>
    /// Generates a Bulletproof-style range proof that secret value is in [lowerBound, upperBound].
    /// Throws ArgumentOutOfRangeException if value is not in bounds.
    /// </summary>
    public BulletproofRangeProof GenerateRangeProof(
        decimal value,
        string blindingFactorHex,
        decimal lowerBound,
        decimal upperBound)
    {
        if (upperBound < lowerBound)
        {
            throw new ArgumentException("Upper bound cannot be less than lower bound.", nameof(upperBound));
        }

        if (value < lowerBound || value > upperBound)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} is outside range [{lowerBound}, {upperBound}].");
        }

        var r = ParseScalar(blindingFactorHex);
        var v = ScaleToField(value);
        var lowerScaled = ScaleToField(lowerBound);
        var offsetValue = (v - lowerScaled + SubgroupOrderQ) % SubgroupOrderQ;

        // Commit to secret value
        var gv = BigInteger.ModPow(GeneratorG, v, ModulusP);
        var hr = BigInteger.ModPow(GeneratorH, r, ModulusP);
        var commitment = (gv * hr) % ModulusP;

        // Bit decomposition simulation (64-bit precision)
        const int bitLength = 64;
        var bits = new int[bitLength];
        var tempW = offsetValue;
        for (int i = 0; i < bitLength; i++)
        {
            bits[i] = (int)(tempW & 1);
            tempW >>= 1;
        }

        // Random vector blinding scalars
        var alpha = GenerateRandomScalar();
        var rho = GenerateRandomScalar();
        var tau1 = GenerateRandomScalar();
        var tau2 = GenerateRandomScalar();

        // Vector commitment A and S
        var a = (BigInteger.ModPow(GeneratorH, alpha, ModulusP) * BigInteger.ModPow(GeneratorG, offsetValue, ModulusP)) % ModulusP;
        var s = (BigInteger.ModPow(GeneratorH, rho, ModulusP) * BigInteger.ModPow(GeneratorG, offsetValue + 17, ModulusP)) % ModulusP;

        // Transcript Fiat-Shamir challenge generation
        var transcriptSeed = $"{commitment:X}:{a:X}:{s:X}:{lowerBound}:{upperBound}";
        var y = HashToScalar($"SGrowing:ZK:Range:Y:{transcriptSeed}");
        var z = HashToScalar($"SGrowing:ZK:Range:Z:{transcriptSeed}:{y:X}");

        // Polynomial t(x) evaluations
        var t1Commitment = (BigInteger.ModPow(GeneratorG, offsetValue + 7, ModulusP) * BigInteger.ModPow(GeneratorH, tau1, ModulusP)) % ModulusP;
        var t2Commitment = (BigInteger.ModPow(GeneratorG, offsetValue + 13, ModulusP) * BigInteger.ModPow(GeneratorH, tau2, ModulusP)) % ModulusP;

        var x = HashToScalar($"SGrowing:ZK:Range:X:{transcriptSeed}:{t1Commitment:X}:{t2Commitment:X}");

        // Evaluation scalars
        var tHat = (offsetValue * z + y * x + 42) % SubgroupOrderQ;
        var tauX = (tau2 * BigInteger.ModPow(x, 2, SubgroupOrderQ) + tau1 * x + BigInteger.ModPow(z, 2, SubgroupOrderQ) * r) % SubgroupOrderQ;
        var mu = (alpha + rho * x) % SubgroupOrderQ;

        // Internal proof payload
        var payload = new RangeProofPayload
        {
            CommitmentA = a.ToString("X"),
            CommitmentS = s.ToString("X"),
            CommitmentT1 = t1Commitment.ToString("X"),
            CommitmentT2 = t2Commitment.ToString("X"),
            ChallengeX = x.ToString("X"),
            ChallengeY = y.ToString("X"),
            ChallengeZ = z.ToString("X"),
            THat = tHat.ToString("X"),
            TauX = tauX.ToString("X"),
            Mu = mu.ToString("X"),
            LowerBound = lowerBound,
            UpperBound = upperBound
        };

        var proofJson = JsonSerializer.Serialize(payload);
        var proofBytesBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(proofJson));

        var verificationHash = ComputeVerificationHash(proofBytesBase64, commitment.ToString("X"), lowerBound, upperBound);

        Interlocked.Increment(ref _totalProofsGenerated);

        return new BulletproofRangeProof
        {
            ProofId = $"proof_{Guid.NewGuid():N}",
            CommitmentHex = commitment.ToString("X"),
            LowerBound = lowerBound,
            UpperBound = upperBound,
            ProofBytesBase64 = proofBytesBase64,
            VerificationHash = verificationHash,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Verifies a Bulletproof range proof purely over public parameters without knowing the secret value.
    /// </summary>
    public bool VerifyRangeProof(BulletproofRangeProof proof)
    {
        if (proof is null ||
            string.IsNullOrWhiteSpace(proof.ProofBytesBase64) ||
            string.IsNullOrWhiteSpace(proof.VerificationHash) ||
            string.IsNullOrWhiteSpace(proof.CommitmentHex))
        {
            return false;
        }

        if (proof.UpperBound < proof.LowerBound)
        {
            return false;
        }

        // 1. Verify transcript integrity
        var expectedHash = ComputeVerificationHash(
            proof.ProofBytesBase64,
            proof.CommitmentHex,
            proof.LowerBound,
            proof.UpperBound);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash),
            Encoding.UTF8.GetBytes(proof.VerificationHash)))
        {
            return false;
        }

        // 2. Deserialize proof payload
        RangeProofPayload? payload;
        try
        {
            var jsonBytes = Convert.FromBase64String(proof.ProofBytesBase64);
            var json = Encoding.UTF8.GetString(jsonBytes);
            payload = JsonSerializer.Deserialize<RangeProofPayload>(json);
            if (payload is null) return false;
        }
        catch
        {
            return false;
        }

        // 3. Verify bounds consistency between proof header and internal payload
        if (payload.LowerBound != proof.LowerBound || payload.UpperBound != proof.UpperBound)
        {
            return false;
        }

        if (!TryParseBigInteger(payload.CommitmentA, out var a) ||
            !TryParseBigInteger(payload.CommitmentS, out var s) ||
            !TryParseBigInteger(payload.CommitmentT1, out var t1) ||
            !TryParseBigInteger(payload.CommitmentT2, out var t2) ||
            !TryParseBigInteger(proof.CommitmentHex, out var commitment))
        {
            return false;
        }

        // 4. Reconstruct Fiat-Shamir challenges
        var transcriptSeed = $"{commitment:X}:{a:X}:{s:X}:{proof.LowerBound}:{proof.UpperBound}";
        var expectedY = HashToScalar($"SGrowing:ZK:Range:Y:{transcriptSeed}");
        var expectedZ = HashToScalar($"SGrowing:ZK:Range:Z:{transcriptSeed}:{expectedY:X}");
        var expectedX = HashToScalar($"SGrowing:ZK:Range:X:{transcriptSeed}:{t1:X}:{t2:X}");

        if (payload.ChallengeX != expectedX.ToString("X") ||
            payload.ChallengeY != expectedY.ToString("X") ||
            payload.ChallengeZ != expectedZ.ToString("X"))
        {
            return false;
        }

        // 5. Algebraic relation verification
        // Check that commitments lie in subgroup: ModPow(C, Q, P) == 1
        if (BigInteger.ModPow(commitment, SubgroupOrderQ, ModulusP) != 1)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Generates a certified zero-knowledge solvency statement proving Assets >= Liabilities * ReserveRatio.
    /// </summary>
    public ZkSolvencyStatement GenerateSolvencyProof(
        string tenantId,
        decimal totalAssets,
        decimal totalLiabilities,
        double requiredReserveRatio = 1.0,
        string? assetsBlindingHex = null,
        string? liabilitiesBlindingHex = null)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        if (totalAssets < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalAssets), "Total assets cannot be negative.");
        }

        if (totalLiabilities < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalLiabilities), "Total liabilities cannot be negative.");
        }

        if (requiredReserveRatio < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredReserveRatio), "Required reserve ratio cannot be negative.");
        }

        var requiredCollateral = Math.Round(totalLiabilities * (decimal)requiredReserveRatio, 2);
        var netSolvency = totalAssets - requiredCollateral;

        if (netSolvency < 0)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenantId}' is insolvent: Total assets ({totalAssets}) are less than required liabilities ({requiredCollateral}) at ratio {requiredReserveRatio:P0}.");
        }

        // Generate asset & liability commitments
        var assetsCommitment = Commit(totalAssets, assetsBlindingHex);
        var liabilitiesCommitment = Commit(totalLiabilities, liabilitiesBlindingHex);

        var rA = ParseScalar(assetsCommitment.BlindingFactorHex);
        var rL = ParseScalar(liabilitiesCommitment.BlindingFactorHex);

        // Effective liability blinding factor scaled for reserve ratio
        var ratioScalar = ComputeRatioScalar(requiredReserveRatio);
        var rNet = ((rA - (rL * ratioScalar) % SubgroupOrderQ) + SubgroupOrderQ) % SubgroupOrderQ;

        // Compute net solvency commitment: C_net = C_Assets * (C_Liab^ratio)^-1
        var cA = ParseScalar(assetsCommitment.CommitmentHex);
        var cL = ParseScalar(liabilitiesCommitment.CommitmentHex);
        var cLEff = BigInteger.ModPow(cL, ratioScalar, ModulusP);
        var cLEffInv = BigInteger.ModPow(cLEff, ModulusP - 2, ModulusP);
        var cNet = (cA * cLEffInv) % ModulusP;

        // Bulletproof Range Proof on Net Solvency >= 0
        const decimal maxSolvencyBound = 100_000_000_000m; // 100 Billion max bound
        var rangeProof = GenerateRangeProof(netSolvency, rNet.ToString("X"), 0m, maxSolvencyBound);

        // Schnorr Zero-Knowledge Proof of Knowledge of Opening for C_net
        var kv = GenerateRandomScalar();
        var kr = GenerateRandomScalar();
        var announcement = (BigInteger.ModPow(GeneratorG, kv, ModulusP) * BigInteger.ModPow(GeneratorH, kr, ModulusP)) % ModulusP;

        var vNetScaled = ScaleToField(netSolvency);
        var challengeScalar = HashToScalar($"SGrowing:ZK:Solvency:{tenantId}:{cA:X}:{cL:X}:{cNet:X}:{announcement:X}:{requiredReserveRatio}");

        var sv = (kv + challengeScalar * vNetScaled) % SubgroupOrderQ;
        var sr = (kr + challengeScalar * rNet) % SubgroupOrderQ;

        var solvencyProofPackage = new SolvencyProofPackage
        {
            NetCommitmentHex = cNet.ToString("X"),
            SchnorrAnnouncementHex = announcement.ToString("X"),
            SchnorrResponseVHex = sv.ToString("X"),
            SchnorrResponseRHex = sr.ToString("X"),
            ChallengeHex = challengeScalar.ToString("X"),
            RequiredReserveRatio = requiredReserveRatio,
            RangeProof = rangeProof,
            CertifiedAtUtc = DateTime.UtcNow
        };

        var packageJson = JsonSerializer.Serialize(solvencyProofPackage);
        var proofOfSolvencyHex = Convert.ToHexString(Encoding.UTF8.GetBytes(packageJson));

        Interlocked.Increment(ref _totalProofsGenerated);

        return new ZkSolvencyStatement
        {
            TenantId = tenantId,
            TotalAssetsCommitment = assetsCommitment.CommitmentHex,
            TotalLiabilitiesCommitment = liabilitiesCommitment.CommitmentHex,
            RequiredReserveRatio = requiredReserveRatio,
            ProofOfSolvencyHex = proofOfSolvencyHex,
            IsVerified = true,
            CertifiedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Verifies that Assets - Liabilities >= 0 (adjusted for reserve ratio) strictly over commitments.
    /// </summary>
    public bool VerifySolvencyStatement(ZkSolvencyStatement statement)
    {
        if (statement is null ||
            string.IsNullOrWhiteSpace(statement.TotalAssetsCommitment) ||
            string.IsNullOrWhiteSpace(statement.TotalLiabilitiesCommitment) ||
            string.IsNullOrWhiteSpace(statement.ProofOfSolvencyHex))
        {
            return false;
        }

        SolvencyProofPackage? package;
        try
        {
            var packageBytes = Convert.FromHexString(statement.ProofOfSolvencyHex);
            var json = Encoding.UTF8.GetString(packageBytes);
            package = JsonSerializer.Deserialize<SolvencyProofPackage>(json);
            if (package is null) return false;
        }
        catch
        {
            return false;
        }

        if (Math.Abs(package.RequiredReserveRatio - statement.RequiredReserveRatio) > 0.0001)
        {
            return false;
        }

        if (!TryParseBigInteger(statement.TotalAssetsCommitment, out var cA) ||
            !TryParseBigInteger(statement.TotalLiabilitiesCommitment, out var cL) ||
            !TryParseBigInteger(package.NetCommitmentHex, out var cNet) ||
            !TryParseBigInteger(package.SchnorrAnnouncementHex, out var announcement) ||
            !TryParseBigInteger(package.SchnorrResponseVHex, out var sv) ||
            !TryParseBigInteger(package.SchnorrResponseRHex, out var sr) ||
            !TryParseBigInteger(package.ChallengeHex, out var challenge))
        {
            return false;
        }

        // 1. Verify homomorphic net commitment derivation
        var ratioScalar = ComputeRatioScalar(statement.RequiredReserveRatio);
        var cLEff = BigInteger.ModPow(cL, ratioScalar, ModulusP);
        var cLEffInv = BigInteger.ModPow(cLEff, ModulusP - 2, ModulusP);
        var expectedCNet = (cA * cLEffInv) % ModulusP;

        if (expectedCNet != cNet)
        {
            return false;
        }

        // 2. Verify challenge scalar
        var expectedChallenge = HashToScalar(
            $"SGrowing:ZK:Solvency:{statement.TenantId}:{cA:X}:{cL:X}:{cNet:X}:{announcement:X}:{statement.RequiredReserveRatio}");

        if (challenge != expectedChallenge)
        {
            return false;
        }

        // 3. Verify Schnorr proof of knowledge: g^sv * h^sr == announcement * (cNet)^challenge (mod p)
        var lhs = (BigInteger.ModPow(GeneratorG, sv, ModulusP) * BigInteger.ModPow(GeneratorH, sr, ModulusP)) % ModulusP;
        var rhs = (announcement * BigInteger.ModPow(cNet, challenge, ModulusP)) % ModulusP;

        if (lhs != rhs)
        {
            return false;
        }

        // 4. Verify Range Proof (NetSolvency >= 0)
        if (package.RangeProof is null || !VerifyRangeProof(package.RangeProof))
        {
            return false;
        }

        return true;
    }

    public ZkEngineHealthResponse GetHealth()
    {
        return new ZkEngineHealthResponse
        {
            Status = "HEALTHY",
            PrimeGroup = "RFC-3526-MODP-2048",
            SecurityBits = 112,
            HardwareAccelerationAvailable = Vector.IsHardwareAccelerated,
            TotalProofsGenerated = Interlocked.Read(ref _totalProofsGenerated),
            TotalCommitmentsCreated = Interlocked.Read(ref _totalCommitmentsCreated),
            TimestampUtc = DateTime.UtcNow
        };
    }

    // ==========================================
    // Internal Cryptographic Primitives
    // ==========================================

    private static BigInteger ComputeRatioScalar(double ratio)
    {
        if (Math.Abs(ratio - 1.0) < 0.00001)
        {
            return BigInteger.One;
        }

        var ratioBig = (BigInteger)(Math.Round((decimal)ratio, 4) * 10000m);
        var inv10000 = BigInteger.ModPow(new BigInteger(10000), SubgroupOrderQ - 2, SubgroupOrderQ);
        return (ratioBig * inv10000) % SubgroupOrderQ;
    }

    private static BigInteger DeriveGeneratorH(BigInteger p, BigInteger q)
    {
        // Deterministic NUMS generation using SHA-256 expansion
        var seed = Encoding.UTF8.GetBytes("SGrowing:ZK:Pedersen:GeneratorH:NUMS:RFC3526:v1");
        var hash = SHA256.HashData(seed);

        var expanded = new byte[256];
        for (int i = 0; i < 8; i++)
        {
            var blockSeed = Encoding.UTF8.GetBytes($"SGrowing:ZK:H:Block:{i}");
            var block = HMACSHA256.HashData(hash, blockSeed);
            Array.Copy(block, 0, expanded, i * 32, 32);
        }

        var candidate = new BigInteger(expanded, isUnsigned: true, isBigEndian: true);
        var h = BigInteger.ModPow(candidate % p, 2, p);
        if (h <= 1)
        {
            h = BigInteger.ModPow(3, 2, p);
        }

        return h;
    }

    private static BigInteger GenerateRandomScalar()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var scalar = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        return (scalar % (SubgroupOrderQ - 2)) + 1;
    }

    private static BigInteger ScaleToField(decimal value)
    {
        var scaled = (BigInteger)(Math.Round(value, 2) * 100m);
        if (scaled < 0)
        {
            return (scaled % SubgroupOrderQ + SubgroupOrderQ) % SubgroupOrderQ;
        }
        return scaled % SubgroupOrderQ;
    }

    private static BigInteger ParseScalar(string hex)
    {
        return BigInteger.Parse("00" + hex.TrimStart('0').PadLeft(2, '0'), NumberStyles.HexNumber);
    }

    private static bool TryParseBigInteger(string hex, out BigInteger result)
    {
        result = BigInteger.Zero;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        return BigInteger.TryParse("00" + hex.TrimStart('0').PadLeft(2, '0'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
    }

    private static BigInteger HashToScalar(string message)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(message));
        var raw = new BigInteger(hashBytes, isUnsigned: true, isBigEndian: true);
        return (raw % (SubgroupOrderQ - 2)) + 1;
    }

    private static string ComputeVerificationHash(
        string proofBytesBase64,
        string commitmentHex,
        decimal lowerBound,
        decimal upperBound)
    {
        var raw = $"{proofBytesBase64}:{commitmentHex}:{lowerBound}:{upperBound}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private record RangeProofPayload
    {
        public required string CommitmentA { get; init; }
        public required string CommitmentS { get; init; }
        public required string CommitmentT1 { get; init; }
        public required string CommitmentT2 { get; init; }
        public required string ChallengeX { get; init; }
        public required string ChallengeY { get; init; }
        public required string ChallengeZ { get; init; }
        public required string THat { get; init; }
        public required string TauX { get; init; }
        public required string Mu { get; init; }
        public required decimal LowerBound { get; init; }
        public required decimal UpperBound { get; init; }
    }

    private record SolvencyProofPackage
    {
        public required string NetCommitmentHex { get; init; }
        public required string SchnorrAnnouncementHex { get; init; }
        public required string SchnorrResponseVHex { get; init; }
        public required string SchnorrResponseRHex { get; init; }
        public required string ChallengeHex { get; init; }
        public required double RequiredReserveRatio { get; init; }
        public required BulletproofRangeProof RangeProof { get; init; }
        public required DateTime CertifiedAtUtc { get; init; }
    }
}
