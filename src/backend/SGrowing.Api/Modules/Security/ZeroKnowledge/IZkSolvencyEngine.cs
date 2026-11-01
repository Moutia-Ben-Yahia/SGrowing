using System.Numerics;

namespace SGrowing.Api.Modules.Security.ZeroKnowledge;

/// <summary>
/// Cryptographic engine performing Pedersen commitments, homomorphic operations,
/// zero-knowledge Bulletproof-style range proofs, and proof-of-solvency verification.
/// </summary>
public interface IZkSolvencyEngine
{
    /// <summary>
    /// Generates a Pedersen commitment C = g^v * h^r (mod p).
    /// </summary>
    PedersenCommitment Commit(decimal value, string? blindingFactorHex = null);

    /// <summary>
    /// Verifies that a commitment C opens to a specific value v with blinding factor r.
    /// </summary>
    bool VerifyCommitment(string commitmentHex, decimal value, string blindingFactorHex);

    /// <summary>
    /// Homomorphically adds two commitments: C(v1, r1) * C(v2, r2) = C(v1 + v2, r1 + r2) (mod p).
    /// </summary>
    PedersenCommitment AddCommitments(PedersenCommitment c1, PedersenCommitment c2);

    /// <summary>
    /// Homomorphically multiplies two commitment hexes: (c1 * c2) mod p.
    /// </summary>
    string CombineCommitmentsHex(string c1Hex, string c2Hex);

    /// <summary>
    /// Homomorphically divides two commitment hexes: (c1 * c2^-1) mod p = C(v1 - v2, r1 - r2).
    /// </summary>
    string SubtractCommitmentsHex(string c1Hex, string c2Hex);

    /// <summary>
    /// Generates a Bulletproof-style zero-knowledge range proof that value in [lowerBound, upperBound]
    /// without disclosing the value itself.
    /// </summary>
    BulletproofRangeProof GenerateRangeProof(decimal value, string blindingFactorHex, decimal lowerBound, decimal upperBound);

    /// <summary>
    /// Verifies a Bulletproof-style range proof strictly using public parameters, bounds, and commitment.
    /// </summary>
    bool VerifyRangeProof(BulletproofRangeProof proof);

    /// <summary>
    /// Generates a certified zero-knowledge solvency statement proving Assets >= Liabilities * ReserveRatio.
    /// Throws InvalidOperationException if tenant is insolvent.
    /// </summary>
    ZkSolvencyStatement GenerateSolvencyProof(
        string tenantId,
        decimal totalAssets,
        decimal totalLiabilities,
        double requiredReserveRatio = 1.0,
        string? assetsBlindingHex = null,
        string? liabilitiesBlindingHex = null);

    /// <summary>
    /// Verifies a zero-knowledge solvency statement without revealing underlying balances.
    /// </summary>
    bool VerifySolvencyStatement(ZkSolvencyStatement statement);

    /// <summary>
    /// Returns public group generators, prime modulus, and subgroup order.
    /// </summary>
    (BigInteger GeneratorG, BigInteger GeneratorH, BigInteger ModulusP, BigInteger OrderQ) GetGroupParameters();

    /// <summary>
    /// Returns operational health, cryptography metrics, and proof counters.
    /// </summary>
    ZkEngineHealthResponse GetHealth();
}
