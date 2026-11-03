using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.PayrollTaxWithholding;

public sealed class PayrollTaxWithholdingEngine
{
    private readonly ConcurrentDictionary<string, TaxJurisdictionProfile> _jurisdictions = new();
    private readonly ConcurrentDictionary<string, StatutoryFilingRecord> _filings = new();
    private readonly ConcurrentBag<SovereignFilingReceipt> _receiptsHistory = new();

    public PayrollTaxWithholdingEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var us = new TaxJurisdictionProfile("US-FED-CA", "United States (California)", "USD", 24.0, 7.65, 4.0, "Internal Revenue Service / EDD", "MONTHLY");
        var uk = new TaxJurisdictionProfile("GB-PAYE", "United Kingdom", "GBP", 20.0, 8.0, 3.0, "HM Revenue & Customs (HMRC)", "MONTHLY");
        var de = new TaxJurisdictionProfile("DE-LOHN", "Germany", "EUR", 28.5, 9.3, 9.3, "Bundeszentralamt für Steuern", "MONTHLY");
        var sg = new TaxJurisdictionProfile("SG-CPF", "Singapore", "SGD", 15.0, 20.0, 17.0, "Inland Revenue Authority of Singapore / CPF", "MONTHLY");

        _jurisdictions[us.JurisdictionCode] = us;
        _jurisdictions[uk.JurisdictionCode] = uk;
        _jurisdictions[de.JurisdictionCode] = de;
        _jurisdictions[sg.JurisdictionCode] = sg;

        var f1 = new StatutoryFilingRecord(
            "FIL-US-2026-M10",
            us.JurisdictionCode,
            "2026-10",
            345600m,
            "USD",
            "IRS-EFTPS-998822",
            "CONFIRMED_REMITTED",
            DateTime.UtcNow.AddDays(-15)
        );

        var f2 = new StatutoryFilingRecord(
            "FIL-GB-2026-M10",
            uk.JurisdictionCode,
            "2026-10",
            189200m,
            "GBP",
            "HMRC-FPS-445511",
            "CONFIRMED_REMITTED",
            DateTime.UtcNow.AddDays(-14)
        );

        _filings[f1.FilingId] = f1;
        _filings[f2.FilingId] = f2;
    }

    public PayrollTaxOverview GetOverview()
    {
        var jurList = _jurisdictions.Values.ToList();
        var filingList = _filings.Values.ToList();

        return new PayrollTaxOverview(
            jurList.Count,
            1280000m,
            3840000m,
            2,
            jurList,
            filingList
        );
    }

    public WithholdingCalculationResult CalculateWithholding(CalculateWithholdingRequest request)
    {
        var profile = _jurisdictions.TryGetValue(request.JurisdictionCode, out var jur) ? jur : _jurisdictions.Values.First();

        var incomeTax = Math.Round(request.GrossPayAmount * (decimal)(profile.IncomeTaxRatePct / 100.0), 2);
        var socialSecurity = Math.Round(request.GrossPayAmount * (decimal)(profile.SocialSecurityRatePct / 100.0), 2);
        var pension = Math.Round(request.GrossPayAmount * (decimal)(profile.PensionEmployerRatePct / 100.0), 2);
        var netPay = request.GrossPayAmount - incomeTax - socialSecurity - pension;
        var employerSocial = Math.Round(request.GrossPayAmount * (decimal)((profile.SocialSecurityRatePct + profile.PensionEmployerRatePct) / 100.0), 2);

        return new WithholdingCalculationResult(
            $"CALC-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            request.EmployeeId,
            profile.JurisdictionCode,
            request.GrossPayAmount,
            incomeTax,
            socialSecurity,
            pension,
            netPay,
            employerSocial,
            profile.Currency,
            DateTime.UtcNow
        );
    }

    public SovereignFilingReceipt GenerateFilingReceipt(GenerateFilingReceiptRequest request)
    {
        var receipt = new SovereignFilingReceipt(
            $"RCP-TAX-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            request.JurisdictionCode,
            request.TaxPeriod,
            request.TotalTaxRemitted,
            $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}",
            $"AUTH-SOV-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            "REMITTANCE_FINALIZED",
            DateTime.UtcNow
        );

        _receiptsHistory.Add(receipt);
        return receipt;
    }
}
