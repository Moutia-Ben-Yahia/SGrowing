namespace SGrowing.Api.Modules.SupplyChain.HsClassification;

/// <summary>
/// Cross-border landed cost & duty calculator evaluating CIF valuation,
/// preferential trade agreements (AfCFTA, GAFTA, EU Association), and cascading tax schedules.
/// </summary>
public sealed class CrossBorderDutyCalculatorService
{
    private readonly HsTariffClassificationEngine _tariffEngine;

    public CrossBorderDutyCalculatorService(HsTariffClassificationEngine tariffEngine)
    {
        _tariffEngine = tariffEngine;
    }

    public LandedCostBreakdown CalculateLandedCost(LandedCostCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entry = _tariffEngine.GetEntry(request.HsCode) ?? new HsTariffEntry(
            HsCode: request.HsCode,
            Chapter: request.HsCode.Length >= 2 ? request.HsCode[..2] : "00",
            Heading: request.HsCode.Length >= 4 ? request.HsCode[..4] : "0000",
            Subheading: request.HsCode.Length >= 6 ? request.HsCode[4..] : "00",
            Description: request.ProductDescription,
            Keywords: [],
            StandardMfnDutyRatePercent: 10.0m,
            VatRatePercent: 19.0m
        );

        // Valuation: CIF = FOB + Freight + Insurance
        var cif = request.FobValueEur + request.InternationalFreightEur + request.MarineInsuranceEur;

        // Determine Applicable Duty Rate under Preferential Trade Agreements
        var dutyRate = entry.StandardMfnDutyRatePercent;
        var note = "Standard WTO Most Favored Nation (MFN) tariff applied.";

        if (request.HasCertifiedProofOfOrigin)
        {
            switch (request.TradeAgreement)
            {
                case PreferentialTradeAgreement.AfCfta:
                    // AfCFTA preferential tariff elimination (90% of non-sensitive tariff lines are 0%)
                    dutyRate = 0.0m;
                    note = "AfCFTA Rule of Origin preferential 100% duty exemption granted.";
                    break;

                case PreferentialTradeAgreement.Gafta:
                    // Greater Arab Free Trade Area duty free
                    dutyRate = 0.0m;
                    note = "GAFTA preferential zero-tariff clearance granted.";
                    break;

                case PreferentialTradeAgreement.EuAlgeriaAssociation:
                    // Industrial goods (Chapter 25-97) are generally 0%; Agriculture 50% reduction
                    var isAgro = entry.Chapter is "01" or "02" or "03" or "04" or "07" or "08" or "15" or "16";
                    dutyRate = isAgro ? (entry.StandardMfnDutyRatePercent * 0.5m) : 0.0m;
                    note = isAgro
                        ? "EU-Algeria Association Agreement 50% agricultural tariff reduction applied."
                        : "EU-Algeria Association Agreement industrial tariff dismantling (0% duty) applied.";
                    break;
            }
        }
        else if (request.TradeAgreement != PreferentialTradeAgreement.NoneWtoMfn)
        {
            note = $"Proof of origin missing. Full WTO MFN rate applied despite claiming {request.TradeAgreement}.";
        }

        var dutyAmount = cif * (dutyRate / 100.0m);
        var exciseAmount = (cif + dutyAmount) * (entry.ExciseTaxRatePercent / 100.0m);
        var vatAmount = (cif + dutyAmount + exciseAmount) * (entry.VatRatePercent / 100.0m);

        var totalCustomsAndTaxes = dutyAmount + exciseAmount + vatAmount;
        var totalLandedCost = cif + totalCustomsAndTaxes;

        return new LandedCostBreakdown(
            CalculationId: $"LND-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            HsCode: request.HsCode,
            FobValueEur: request.FobValueEur,
            FreightEur: request.InternationalFreightEur,
            InsuranceEur: request.MarineInsuranceEur,
            CifValueEur: Math.Round(cif, 2),
            ApplicableDutyRatePercent: dutyRate,
            CustomsDutyEur: Math.Round(dutyAmount, 2),
            ExciseTaxEur: Math.Round(exciseAmount, 2),
            VatAmountEur: Math.Round(vatAmount, 2),
            TotalCustomsAndTaxesEur: Math.Round(totalCustomsAndTaxes, 2),
            TotalLandedCostEur: Math.Round(totalLandedCost, 2),
            AppliedTradeAgreement: request.HasCertifiedProofOfOrigin ? request.TradeAgreement : PreferentialTradeAgreement.NoneWtoMfn,
            TariffDutyNotes: note,
            CalculatedAtUtc: DateTime.UtcNow
        );
    }
}
