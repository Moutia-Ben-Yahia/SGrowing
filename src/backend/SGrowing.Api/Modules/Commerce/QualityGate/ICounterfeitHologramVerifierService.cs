namespace SGrowing.Api.Modules.Commerce.QualityGate;

/// <summary>
/// Interface for optical security hologram and microtext matrix verification.
/// </summary>
public interface ICounterfeitHologramVerifierService
{
    /// <summary>
    /// Verifies the authenticity of a product security hologram scan and microtext matrix against emission ledgers.
    /// </summary>
    HologramVerificationFinding VerifyHologram(string serialCode, byte[] hologramScanBytes, string expectedMicrotext);

    /// <summary>
    /// Registers an authorized batch of serial matrix codes from a verified manufacturer or supplier.
    /// </summary>
    void RegisterManufacturerBatch(string supplierId, IEnumerable<string> validSerials);

    /// <summary>
    /// Registers known counterfeit or compromised serial numbers onto the global blacklist.
    /// </summary>
    void RegisterBlacklist(IEnumerable<string> blacklistedSerials);

    /// <summary>
    /// Adds a single serial code to the counterfeit blacklist.
    /// </summary>
    void BlacklistSerial(string serialCode);

    /// <summary>
    /// Checks whether a serial matrix code is recognized in any registered manufacturer batch.
    /// </summary>
    bool IsSerialKnown(string serialCode);

    /// <summary>
    /// Retrieves historical verification findings, optionally filtered by serial matrix code.
    /// </summary>
    IReadOnlyList<HologramVerificationFinding> GetVerificationHistory(string? serialCode = null);
}
