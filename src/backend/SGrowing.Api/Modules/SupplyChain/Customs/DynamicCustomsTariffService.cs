namespace SGrowing.Api.Modules.SupplyChain.Customs;

/// <summary>
/// Dynamic landed customs tariff and tax calculation engine.
/// Evaluates CIF values, MFN and preferential trade agreements (EU-Algeria, GAFTA, AfCFTA),
/// De Minimis statutory exemptions, excise, VAT, and Algerian DAPS safeguard surcharges.
/// </summary>
public class DynamicCustomsTariffService(IHsCodeClassifierEngine classifierEngine) : IDynamicCustomsTariffService
{
    private readonly IHsCodeClassifierEngine _classifierEngine = classifierEngine ?? throw new ArgumentNullException(nameof(classifierEngine));

    // EU Member Country ISO Codes
    private static readonly HashSet<string> EuCountries =
    [
        "EU", "FR", "DE", "IT", "ES", "NL", "BE", "PL", "SE", "AT", "IE", "PT",
        "GR", "FI", "DK", "CZ", "RO", "HU", "SK", "BG", "HR", "LT", "SI", "LV",
        "EE", "CY", "LU", "MT"
    ];

    // GAFTA (Greater Arab Free Trade Area) Member Country ISO Codes
    private static readonly HashSet<string> GaftaCountries =
    [
        "DZ", "AE", "SA", "EG", "JO", "QA", "OM", "KW", "BH", "TN",
        "MA", "IQ", "LB", "LY", "SD", "SY", "YE", "PS"
    ];

    // AfCFTA (African Continental Free Trade Area) Active Participating States
    private static readonly HashSet<string> AfcftaCountries =
    [
        "DZ", "NG", "ZA", "KE", "GH", "RW", "EG", "MA", "ET", "CI",
        "SN", "CM", "TZ", "UG", "TN", "AO", "NA", "ZM", "ZW"
    ];

    /// <inheritdoc />
    public TariffCalculationResult CalculateLandedTariff(TariffCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.FobPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "FOB price cannot be negative.");
        }
        if (request.InternationalFreight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Freight cannot be negative.");
        }
        if (request.MarineInsurance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Marine insurance cannot be negative.");
        }

        // 1. Calculate CIF = FOB + Freight + Insurance
        var cif = Math.Round(request.FobPrice + request.InternationalFreight + request.MarineInsurance, 2, MidpointRounding.AwayFromZero);

        // 2. Evaluate De Minimis Threshold
        var deMinimis = CheckDeMinimisEligibility(request.DestinationCountry, cif, request.IsCommercial);

        // 3. Resolve HS code details and national tariff line
        var hsDetails = _classifierEngine.GetHsCodeDetails(request.HsCode);
        var normDest = NormalizeCountry(request.DestinationCountry);
        var tariffLine = ResolveTariffLine(hsDetails, normDest);

        // 4. Evaluate Preferential Trade Agreements (PTA)
        var pta = EvaluatePtaEligibility(request.OriginCountry, request.DestinationCountry, request.HsCode, request.HasCertificateOfOrigin);

        // 5. Determine Effective Customs Duty Rate
        decimal standardMfnRate = request.CustomDutyRate ?? tariffLine?.MfnDutyRate ?? hsDetails?.StandardMfnDutyRate ?? 0.10m;
        decimal effectiveDutyRate;

        if (deMinimis.IsExemptFromDuty)
        {
            effectiveDutyRate = 0.00m;
        }
        else if (pta.IsPtaApplicable)
        {
            effectiveDutyRate = pta.PreferentialDutyRate;
        }
        else
        {
            effectiveDutyRate = standardMfnRate;
        }

        var dutyAmount = Math.Round(cif * effectiveDutyRate, 2, MidpointRounding.AwayFromZero);

        // 6. DAPS Surcharge (Algeria specific safeguard duty)
        decimal dapsRate = 0.00m;
        decimal dapsAmount = 0.00m;

        if (normDest == "DZ" && !deMinimis.IsExemptFromDuty)
        {
            // If preferential trade agreement applies (e.g. EUR.1 or GAFTA), DAPS may be waived or determined by tariff line
            dapsRate = tariffLine?.DapsSurchargeRate ?? 0.00m;
            dapsAmount = Math.Round(cif * dapsRate, 2, MidpointRounding.AwayFromZero);
        }

        // 7. Excise Tax: (CIF + Duty) * ExciseRate
        var effectiveExciseRate = request.CustomExciseRate ?? hsDetails?.StandardExciseRate ?? 0.00m;
        var exciseTaxBase = cif + dutyAmount;
        var exciseAmount = Math.Round(exciseTaxBase * effectiveExciseRate, 2, MidpointRounding.AwayFromZero);

        // 8. VAT: (CIF + Duty + DAPS + Excise) * VatRate
        decimal effectiveVatRate;
        decimal vatAmount;

        if (deMinimis.IsExemptFromVat)
        {
            effectiveVatRate = 0.00m;
            vatAmount = 0.00m;
        }
        else
        {
            effectiveVatRate = request.CustomVatRate ?? tariffLine?.VatRate ?? hsDetails?.StandardVatRate ?? 0.19m;
            var vatTaxBase = cif + dutyAmount + dapsAmount + exciseAmount;
            vatAmount = Math.Round(vatTaxBase * effectiveVatRate, 2, MidpointRounding.AwayFromZero);
        }

        // 9. Customs Processing Fees
        decimal adminFee;
        decimal statisticalFee;

        if (deMinimis.IsExemptFromDuty && deMinimis.IsExemptFromVat)
        {
            adminFee = 0.00m;
            statisticalFee = 0.00m;
        }
        else
        {
            adminFee = 25.00m;
            // 0.4% statistical fee, minimum 5.00
            statisticalFee = Math.Max(5.00m, Math.Round(cif * 0.004m, 2, MidpointRounding.AwayFromZero));
        }

        var customsFeeTotal = adminFee + statisticalFee;

        // 10. Total Landed Cost: CIF + Duty + DAPS + Excise + VAT + CustomsFee
        var totalLandedCost = cif + dutyAmount + dapsAmount + exciseAmount + vatAmount + customsFeeTotal;

        return new TariffCalculationResult(
            FobPrice: request.FobPrice,
            InternationalFreight: request.InternationalFreight,
            MarineInsurance: request.MarineInsurance,
            CifValue: cif,
            DutyAmount: dutyAmount,
            EffectiveDutyRate: effectiveDutyRate,
            DapsAmount: dapsAmount,
            DapsRate: dapsRate,
            ExciseAmount: exciseAmount,
            ExciseRate: effectiveExciseRate,
            VatAmount: vatAmount,
            VatRate: effectiveVatRate,
            AdministrativeFee: adminFee,
            StatisticalFee: statisticalFee,
            CustomsFeeTotal: customsFeeTotal,
            TotalLandedCost: totalLandedCost,
            Currency: "USD",
            DeMinimisApplied: deMinimis.IsExemptFromDuty,
            PtaEvaluation: pta,
            CalculatedAtUtc: DateTime.UtcNow);
    }

    /// <inheritdoc />
    public DeMinimisEvaluationResult CheckDeMinimisEligibility(string destinationCountry, decimal cifValueUsd, bool isCommercial = true)
    {
        var normDest = NormalizeCountry(destinationCountry);

        switch (normDest)
        {
            case "DZ":
            {
                const decimal statutoryThreshold = 100.00m;
                // Algeria: Personal imports under $100 exempt from commercial duty
                if (!isCommercial && cifValueUsd <= statutoryThreshold)
                {
                    return new DeMinimisEvaluationResult(
                        IsExemptFromDuty: true,
                        IsExemptFromVat: false,
                        StatutoryThresholdUsd: statutoryThreshold,
                        CifValueUsd: cifValueUsd,
                        DestinationCountry: "DZ",
                        LegalBasis: "Code des Douanes Algériennes Art. 202 - Personal non-commercial shipments under statutory threshold (100 USD / ~10,000 DZD) exempt from commercial duty.");
                }

                return new DeMinimisEvaluationResult(
                    IsExemptFromDuty: false,
                    IsExemptFromVat: false,
                    StatutoryThresholdUsd: statutoryThreshold,
                    CifValueUsd: cifValueUsd,
                    DestinationCountry: "DZ",
                    LegalBasis: isCommercial
                        ? "Commercial import into Algeria: All commercial shipments are subject to standard customs duties regardless of value."
                        : $"Personal shipment value (${cifValueUsd:F2}) exceeds statutory threshold (${statutoryThreshold:F2}). Full duties apply.");
            }

            case var dest when IsEuCountry(dest):
            {
                // EU: Under €150 (~$165 USD) IOSS threshold exempt from customs duty, but subject to import VAT
                const decimal statutoryThreshold = 165.00m;
                if (cifValueUsd <= statutoryThreshold)
                {
                    return new DeMinimisEvaluationResult(
                        IsExemptFromDuty: true,
                        IsExemptFromVat: false,
                        StatutoryThresholdUsd: statutoryThreshold,
                        CifValueUsd: cifValueUsd,
                        DestinationCountry: normDest,
                        LegalBasis: "EU Council Regulation (EC) No 1186/2009 & EU VAT e-Commerce Package - Consignments not exceeding €150 (~$165 USD) exempt from customs duties; import VAT remains mandatory under IOSS.");
                }

                return new DeMinimisEvaluationResult(
                    IsExemptFromDuty: false,
                    IsExemptFromVat: false,
                    StatutoryThresholdUsd: statutoryThreshold,
                    CifValueUsd: cifValueUsd,
                    DestinationCountry: normDest,
                    LegalBasis: $"Consignment value (${cifValueUsd:F2}) exceeds EU €150 (~$165 USD) threshold. Full customs duties and import VAT apply.");
            }

            case var dest when IsGccCountry(dest):
            {
                // GCC / UAE: Under 300 AED (~$81.68 USD) exempt from duty
                const decimal statutoryThreshold = 81.68m;
                if (cifValueUsd <= statutoryThreshold)
                {
                    return new DeMinimisEvaluationResult(
                        IsExemptFromDuty: true,
                        IsExemptFromVat: false,
                        StatutoryThresholdUsd: statutoryThreshold,
                        CifValueUsd: cifValueUsd,
                        DestinationCountry: normDest,
                        LegalBasis: "GCC Unified Customs Law & UAE Customs Notice 05/2022 - Low value parcels under 300 AED (~$81.68 USD) exempt from 5% customs duty.");
                }

                return new DeMinimisEvaluationResult(
                    IsExemptFromDuty: false,
                    IsExemptFromVat: false,
                    StatutoryThresholdUsd: statutoryThreshold,
                    CifValueUsd: cifValueUsd,
                    DestinationCountry: normDest,
                    LegalBasis: $"Consignment value (${cifValueUsd:F2}) exceeds GCC 300 AED (~$81.68 USD) threshold. Standard 5% duty applies.");
            }

            default:
            {
                // Standard default (e.g. US Section 321 threshold $800)
                const decimal defaultThreshold = 800.00m;
                var isExempt = cifValueUsd <= defaultThreshold;
                return new DeMinimisEvaluationResult(
                    IsExemptFromDuty: isExempt,
                    IsExemptFromVat: isExempt,
                    StatutoryThresholdUsd: defaultThreshold,
                    CifValueUsd: cifValueUsd,
                    DestinationCountry: normDest,
                    LegalBasis: isExempt
                        ? $"Consignment below international baseline de minimis threshold (${defaultThreshold:F2})."
                        : $"Consignment exceeds international baseline de minimis threshold (${defaultThreshold:F2}).");
            }
        }
    }

    /// <inheritdoc />
    public PtaEvaluationResult EvaluatePtaEligibility(string originCountry, string destinationCountry, string hsCode, bool hasCertificateOfOrigin)
    {
        var normOrigin = NormalizeCountry(originCountry);
        var normDest = NormalizeCountry(destinationCountry);

        var hsDetails = _classifierEngine.GetHsCodeDetails(hsCode);
        var standardMfn = hsDetails?.StandardMfnDutyRate ?? 0.30m;

        // 1. EU-Algeria Association Agreement
        if ((IsEuCountry(normOrigin) && normDest == "DZ") || (normOrigin == "DZ" && IsEuCountry(normDest)))
        {
            var isIndustrial = IsIndustrialGood(hsCode);
            if (hasCertificateOfOrigin)
            {
                var prefDuty = isIndustrial ? 0.00m : Math.Round(standardMfn * 0.50m, 2);
                var discount = isIndustrial ? 100.0m : 50.0m;

                return new PtaEvaluationResult(
                    IsPtaApplicable: true,
                    AgreementName: "EU-Algeria Association Agreement",
                    StandardMfnDutyRate: standardMfn,
                    PreferentialDutyRate: prefDuty,
                    DutyDiscountPercentage: discount,
                    CertificateOfOriginVerified: true,
                    CertificateType: "EUR.1 Movement Certificate",
                    EligibilityReason: isIndustrial
                        ? "Eligible industrial goods under EU-Algeria Association Agreement: 100% duty reduction (0% preferential rate) with valid EUR.1 certificate."
                        : "Agri-food products under EU-Algeria Association Agreement: 50% preferential duty concession with valid EUR.1 certificate.");
            }

            return new PtaEvaluationResult(
                IsPtaApplicable: false,
                AgreementName: "EU-Algeria Association Agreement",
                StandardMfnDutyRate: standardMfn,
                PreferentialDutyRate: standardMfn,
                DutyDiscountPercentage: 0.0m,
                CertificateOfOriginVerified: false,
                CertificateType: "EUR.1 Movement Certificate",
                EligibilityReason: "Trade corridor qualifies under EU-Algeria Association Agreement, but preferential rate was denied due to missing EUR.1 Movement Certificate. Standard MFN rate applied.");
        }

        // 2. GAFTA (Greater Arab Free Trade Area)
        if (GaftaCountries.Contains(normOrigin) && GaftaCountries.Contains(normDest) && normOrigin != normDest)
        {
            if (hasCertificateOfOrigin)
            {
                return new PtaEvaluationResult(
                    IsPtaApplicable: true,
                    AgreementName: "GAFTA (Greater Arab Free Trade Area)",
                    StandardMfnDutyRate: standardMfn,
                    PreferentialDutyRate: 0.00m,
                    DutyDiscountPercentage: 100.0m,
                    CertificateOfOriginVerified: true,
                    CertificateType: "Arab League Certificate of Origin",
                    EligibilityReason: "Goods manufactured in Arab League state with certified Certificate of Origin qualify for 0% preferential duty under GAFTA.");
            }

            return new PtaEvaluationResult(
                IsPtaApplicable: false,
                AgreementName: "GAFTA (Greater Arab Free Trade Area)",
                StandardMfnDutyRate: standardMfn,
                PreferentialDutyRate: standardMfn,
                DutyDiscountPercentage: 0.0m,
                CertificateOfOriginVerified: false,
                CertificateType: "Arab League Certificate of Origin",
                EligibilityReason: "Both origin and destination participate in GAFTA, but mandatory Arab League Certificate of Origin was not provided. Standard MFN duty applies.");
        }

        // 3. AfCFTA (African Continental Free Trade Area)
        if (AfcftaCountries.Contains(normOrigin) && AfcftaCountries.Contains(normDest) && normOrigin != normDest)
        {
            if (hasCertificateOfOrigin)
            {
                return new PtaEvaluationResult(
                    IsPtaApplicable: true,
                    AgreementName: "AfCFTA (African Continental Free Trade Area)",
                    StandardMfnDutyRate: standardMfn,
                    PreferentialDutyRate: 0.00m,
                    DutyDiscountPercentage: 100.0m,
                    CertificateOfOriginVerified: true,
                    CertificateType: "AfCFTA Certificate of Origin",
                    EligibilityReason: "African originating goods eligible for phased tariff liberalization down to 0% preferential duty under AfCFTA protocol with certified proof of origin.");
            }

            return new PtaEvaluationResult(
                IsPtaApplicable: false,
                AgreementName: "AfCFTA (African Continental Free Trade Area)",
                StandardMfnDutyRate: standardMfn,
                PreferentialDutyRate: standardMfn,
                DutyDiscountPercentage: 0.0m,
                CertificateOfOriginVerified: false,
                CertificateType: "AfCFTA Certificate of Origin",
                EligibilityReason: "AfCFTA participating corridor detected, but AfCFTA Certificate of Origin is missing. Standard MFN rate applied.");
        }

        // 4. Default MFN (Most Favored Nation)
        return new PtaEvaluationResult(
            IsPtaApplicable: false,
            AgreementName: "WTO Most Favored Nation (MFN)",
            StandardMfnDutyRate: standardMfn,
            PreferentialDutyRate: standardMfn,
            DutyDiscountPercentage: 0.0m,
            CertificateOfOriginVerified: false,
            CertificateType: "None",
            EligibilityReason: "No bilateral or multilateral preferential trade agreement applies between these territories. Standard MFN customs tariff applies.");
    }

    #region Helper Methods

    private static string NormalizeCountry(string country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return string.Empty;
        }

        var trimmed = country.Trim().ToUpperInvariant();
        return trimmed switch
        {
            "ALGERIA" or "DZA" => "DZ",
            "FRANCE" or "FRA" => "FR",
            "GERMANY" or "DEU" => "DE",
            "ITALY" or "ITA" => "IT",
            "SPAIN" or "ESP" => "ES",
            "UNITED ARAB EMIRATES" or "UAE" or "ARE" => "AE",
            "SAUDI ARABIA" or "SAU" or "KSA" => "SA",
            "EGYPT" or "EGY" => "EG",
            "MOROCCO" or "MAR" => "MA",
            "TUNISIA" or "TUN" => "TN",
            "NIGERIA" or "NGA" => "NG",
            "SOUTH AFRICA" or "ZAF" => "ZA",
            _ => trimmed
        };
    }

    private static bool IsEuCountry(string code) =>
        EuCountries.Contains(code);

    private static bool IsGccCountry(string code) =>
        code is "GCC" or "AE" or "SA" or "QA" or "KW" or "OM" or "BH";

    private static bool IsIndustrialGood(string hsCode)
    {
        if (string.IsNullOrWhiteSpace(hsCode))
        {
            return true;
        }

        var digits = hsCode.Replace(".", string.Empty).Trim();
        if (digits.Length >= 2 && int.TryParse(digits[..2], out var chapter))
        {
            // Chapters 01-24 are agricultural/food; chapters 25-97 are industrial
            return chapter >= 25;
        }

        return true;
    }

    private static NationalTariffLine? ResolveTariffLine(HsCodeDetails? details, string normDest)
    {
        if (details is null)
        {
            return null;
        }

        if (normDest == "DZ")
        {
            return details.NationalTariffLines.FirstOrDefault(l => l.CountryCode == "DZ");
        }

        if (IsEuCountry(normDest))
        {
            return details.NationalTariffLines.FirstOrDefault(l => l.CountryCode == "EU");
        }

        if (IsGccCountry(normDest))
        {
            return details.NationalTariffLines.FirstOrDefault(l => l.CountryCode == "GCC");
        }

        return details.NationalTariffLines.FirstOrDefault();
    }

    #endregion
}
