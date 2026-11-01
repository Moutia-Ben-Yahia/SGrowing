namespace SGrowing.Api.Modules.SupplyChain.Customs;

/// <summary>
/// Dynamic landed customs tariff and tax calculation engine.
/// Evaluates CIF values, MFN and preferential trade agreements (EU-Algeria, GAFTA, AfCFTA),
/// De Minimis statutory exemptions, excise, VAT, and Algerian DAPS safeguard surcharges.
/// </summary>
public interface IDynamicCustomsTariffService
{
    /// <summary>
    /// Calculates the comprehensive landed cost breakdown for an import shipment.
    /// </summary>
    TariffCalculationResult CalculateLandedTariff(TariffCalculationRequest request);

    /// <summary>
    /// Evaluates whether an import qualifies for De Minimis duty and/or VAT exemption.
    /// </summary>
    DeMinimisEvaluationResult CheckDeMinimisEligibility(string destinationCountry, decimal cifValueUsd, bool isCommercial = true);

    /// <summary>
    /// Evaluates eligibility under bilateral or multilateral Preferential Trade Agreements (PTA).
    /// </summary>
    PtaEvaluationResult EvaluatePtaEligibility(string originCountry, string destinationCountry, string hsCode, bool hasCertificateOfOrigin);
}
