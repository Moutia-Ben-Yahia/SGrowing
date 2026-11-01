namespace SGrowing.Api.Modules.Security.ZeroKnowledge;

/// <summary>
/// Immutable in-memory Merkle tree aggregating all tenant solvency commitments.
/// Enables independent verification of reserve inclusion without revealing competitor data.
/// </summary>
public interface IMerkleSolvencyLedgerService
{
    /// <summary>
    /// Registers or updates a tenant's verified zero-knowledge solvency statement in the Merkle tree.
    /// </summary>
    void RegisterSolvencyStatement(ZkSolvencyStatement statement);

    /// <summary>
    /// Retrieves the current cryptographic Merkle root hash of the solvency ledger.
    /// </summary>
    string GetSolvencyRoot();

    /// <summary>
    /// Generates a zero-knowledge Merkle inclusion proof for a specific tenant.
    /// </summary>
    MerkleSolvencyProof GenerateInclusionProof(string tenantId);

    /// <summary>
    /// Verifies that a Merkle inclusion proof correctly connects a leaf to the expected root.
    /// </summary>
    bool VerifyInclusionProof(MerkleSolvencyProof proof);

    /// <summary>
    /// Retrieves full ledger summary with root hash, total tenant count, and tenant identifiers.
    /// </summary>
    MerkleSolvencyRootResponse GetLedgerSummary();

    /// <summary>
    /// Resets all registered statements in the ledger (testing utility).
    /// </summary>
    void Clear();
}
