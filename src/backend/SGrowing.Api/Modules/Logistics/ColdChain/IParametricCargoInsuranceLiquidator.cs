namespace SGrowing.Api.Modules.Logistics.ColdChain;

/// <summary>
/// Autonomous zero-touch parametric cargo insurance claim liquidation service.
/// Automatically verifies decentralized telemetry oracle feeds, voids buyer COD obligations,
/// and executes instant smart claim payouts.
/// </summary>
public interface IParametricCargoInsuranceLiquidator
{
    /// <summary>
    /// Processes IoT environmental and impact telemetry to evaluate and autonomously liquidate
    /// parametric insurance claims upon threshold breach.
    /// </summary>
    ParametricInsuranceClaim ProcessTelemetryForClaims(
        string consignmentId,
        CargoThermalProfile profile,
        decimal policyPayoutDzd,
        string recipientId,
        string? secretKey = null,
        string? policyNumber = null);

    /// <summary>
    /// Retrieves a specific insurance claim by its identifier.
    /// </summary>
    ParametricInsuranceClaim? GetClaim(string claimId);

    /// <summary>
    /// Lists all claims that have been automatically approved and liquidated.
    /// </summary>
    IReadOnlyList<ParametricInsuranceClaim> ListLiquidatedClaims();

    /// <summary>
    /// Retrieves all claims associated with a specific consignment.
    /// </summary>
    IReadOnlyList<ParametricInsuranceClaim> GetClaimsForConsignment(string consignmentId);

    /// <summary>
    /// Cryptographically verifies the authenticity of an autonomous liquidation certificate.
    /// </summary>
    bool VerifyLiquidationCertificate(ParametricInsuranceClaim claim, string? secretKey = null);

    /// <summary>
    /// Resets internal claim registry for maintenance or testing.
    /// </summary>
    void Clear();
}
