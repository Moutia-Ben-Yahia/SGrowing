using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.TransferPricing;

public interface ITransferPricingEngine
{
    TransferPricingOverview GetOverview();
    CalculateMarkupResult CalculateMarkup(CalculateMarkupCommand command);
    GenerateInvoicesResult GenerateInvoices(GenerateInvoicesCommand command);
}

public class TransferPricingEngine : ITransferPricingEngine
{
    private readonly List<IntercompanyEntity> _entities =
    [
        new("ENT-US-HQ", "SGrowing Technologies Inc.", "UnitedStates_Delaware", "EIN-12-9844102", "Principal_RiskTaker", "USD"),
        new("ENT-IE-OPS", "SGrowing International Operations Ltd.", "Ireland_Dublin", "IE-9941028-V", "Distributor_LimitedRisk", "EUR"),
        new("ENT-SG-APAC", "SGrowing APAC Hub Pte. Ltd.", "Singapore", "UEN-202611984K", "SharedServices", "SGD"),
        new("ENT-UK-RND", "SGrowing R&D UK Limited", "UnitedKingdom_London", "GB-771928410", "RndServiceProvider", "GBP")
    ];

    private readonly List<TransferPricingAgreement> _agreements =
    [
        new("AGR-TP-01", "ENT-UK-RND", "ENT-US-HQ", "SoftwareDevelopmentCostSharing", 8.5m, 6.0m, 10.0m, "Compliant_ArmLength"),
        new("AGR-TP-02", "ENT-SG-APAC", "ENT-US-HQ", "ManagementServicesMarkup", 6.0m, 5.0m, 8.0m, "Compliant_ArmLength"),
        new("AGR-TP-03", "ENT-US-HQ", "ENT-IE-OPS", "IpLicensingRoyalty", 12.0m, 9.5m, 14.5m, "Compliant_ArmLength")
    ];

    private readonly List<IntercompanyInvoice> _invoices =
    [
        new("INV-IC-2026-001", "AGR-TP-01", "ENT-UK-RND", "ENT-US-HQ", "2026-M03", 850000m, 8.5m, 922250m, "USD", "NETPOOL-MAR-01", "Settled_NettingPool"),
        new("INV-IC-2026-002", "AGR-TP-02", "ENT-SG-APAC", "ENT-US-HQ", "2026-M03", 320000m, 6.0m, 339200m, "USD", "NETPOOL-MAR-01", "Settled_NettingPool"),
        new("INV-IC-2026-003", "AGR-TP-03", "ENT-US-HQ", "ENT-IE-OPS", "2026-M03", 1450000m, 12.0m, 1624000m, "EUR", "NETPOOL-MAR-01", "Approved_PendingSettlement")
    ];

    public TransferPricingOverview GetOverview()
    {
        var totalEntities = _entities.Count;
        var activeAgreements = _agreements.Count;
        var totalVolume = _invoices.Sum(i => i.TotalInvoiceAmount);
        var complianceRate = 100.0;

        return new TransferPricingOverview(
            totalEntities,
            activeAgreements,
            totalVolume,
            complianceRate,
            _entities,
            _agreements,
            _invoices
        );
    }

    public CalculateMarkupResult CalculateMarkup(CalculateMarkupCommand command)
    {
        var (markup, minArm, maxArm) = command.ServiceType switch
        {
            "SoftwareDevelopmentCostSharing" => (8.5m, 6.0m, 10.0m),
            "IpLicensingRoyalty" => (12.0m, 9.5m, 14.5m),
            _ => (6.0m, 5.0m, 8.0m)
        };

        var armPrice = Math.Round(command.BaseDirectCost * (1 + markup / 100m), 2);

        return new CalculateMarkupResult(
            $"CALC-TP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            command.ServiceType,
            command.BaseDirectCost,
            markup,
            armPrice,
            minArm,
            maxArm,
            true
        );
    }

    public GenerateInvoicesResult GenerateInvoices(GenerateInvoicesCommand command)
    {
        var batchId = $"BATCH-NET-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var gross = _invoices.Sum(i => i.TotalInvoiceAmount);
        var netPayable = Math.Round(gross * 0.28m, 2); // 72% netted out bilaterally

        return new GenerateInvoicesResult(
            batchId,
            command.BillingPeriod,
            _invoices.Count,
            gross,
            netPayable,
            $"FX-NETTING-OECD-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}"
        );
    }
}
