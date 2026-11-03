using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.IoT.ColdChainSla;

/// <summary>
/// Autonomous cold-chain SLA arbiter evaluating carrier liabilities, calculating
/// spoilage risk percentages, and issuing tamper-proof penalty deduction certificates.
/// </summary>
public sealed class CarrierColdChainSlaArbiterService
{
    private readonly MeanKineticTemperatureCalculatorEngine _mktEngine;

    public CarrierColdChainSlaArbiterService(MeanKineticTemperatureCalculatorEngine mktEngine)
    {
        _mktEngine = mktEngine;
    }

    public ColdChainAuditReport AdjudicateShipment(AuditShipmentColdChainRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var policy = GetPolicyForCargo(request.CargoType);

        var (mkt, minTemp, maxTemp, excursionMins, maxShock) = _mktEngine.CalculateMkt(
            request.TelemetrySamples,
            policy.MinAllowedTempCelsius,
            policy.MaxAllowedTempCelsius
        );

        var isMktBreached = mkt > policy.CriticalMaxMktCelsius || minTemp < policy.MinAllowedTempCelsius;
        var isDurationBreached = excursionMins > policy.MaxPermissibleExcursionMinutes;
        var isShockBreached = maxShock > policy.MaxPermissibleShockG;

        var slaBreached = isMktBreached || isDurationBreached || isShockBreached;

        double spoilageRisk = 0.0;
        decimal penaltyAmount = 0.0m;
        string verdict;

        if (slaBreached)
        {
            if (isMktBreached || excursionMins >= 180)
            {
                // Total thermal denaturation -> 100% loss
                spoilageRisk = 100.0;
                penaltyAmount = request.TotalCargoInvoiceValueEur;
                verdict = $"TOTAL LOSS: Mean Kinetic Temperature ({mkt:F1}C > {policy.CriticalMaxMktCelsius:F1}C) or prolonged excursion ({excursionMins} min) caused biological denaturation. Full 100% invoice penalty ({penaltyAmount:F2} EUR) deducted from carrier remittance.";
            }
            else if (isDurationBreached)
            {
                spoilageRisk = 35.0;
                penaltyAmount = request.TotalCargoInvoiceValueEur * 0.35m;
                verdict = $"PARTIAL DAMAGE SLA: Excursion duration ({excursionMins} min > {policy.MaxPermissibleExcursionMinutes} min) breached SLA. 35% penalty ({penaltyAmount:F2} EUR) assessed.";
            }
            else // Shock only
            {
                spoilageRisk = 15.0;
                penaltyAmount = request.TotalCargoInvoiceValueEur * 0.15m;
                verdict = $"KINETIC SHOCK SLA: Excessive shock ({maxShock:F1}G > {policy.MaxPermissibleShockG}G) flagged. 15% handling penalty ({penaltyAmount:F2} EUR) deducted.";
            }
        }
        else
        {
            verdict = $"COMPLIANT: Shipment MKT ({mkt:F1}C) within specification [{policy.MinAllowedTempCelsius}C, {policy.MaxAllowedTempCelsius}C]. Zero penalties assessed.";
        }

        var auditId = $"COLD-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var now = DateTime.UtcNow;
        var rawProof = $"{auditId}|{request.ShipmentId}|{request.CarrierId}|{mkt}|{excursionMins}|{slaBreached}|{penaltyAmount}|{now:O}";
        var proofHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawProof))).ToLowerInvariant();

        return new ColdChainAuditReport(
            AuditId: auditId,
            ShipmentId: request.ShipmentId,
            CarrierId: request.CarrierId,
            CargoType: request.CargoType,
            MeanKineticTemperatureCelsius: mkt,
            MinObservedTempCelsius: minTemp,
            MaxObservedTempCelsius: maxTemp,
            TotalExcursionMinutes: excursionMins,
            MaxShockGObserved: maxShock,
            SlaBreached: slaBreached,
            SpoilageRiskPercent: spoilageRisk,
            CarrierPenaltyDeductionEur: Math.Round(penaltyAmount, 2),
            AdjudicationVerdict: verdict,
            ImmutableAuditProofHex: proofHash,
            AuditedAtUtc: now
        );
    }

    private static ColdChainSlaPolicy GetPolicyForCargo(ColdChainCargoType cargoType) => cargoType switch
    {
        ColdChainCargoType.VaccinesAndBiologics => new(ColdChainCargoType.VaccinesAndBiologics, 2.0, 8.0, CriticalMaxMktCelsius: 10.5, MaxPermissibleExcursionMinutes: 45, MaxPermissibleShockG: 3.5),
        ColdChainCargoType.FrozenSeafoodAndMeat => new(ColdChainCargoType.FrozenSeafoodAndMeat, -30.0, -18.0, CriticalMaxMktCelsius: -12.0, MaxPermissibleExcursionMinutes: 90, MaxPermissibleShockG: 5.0),
        ColdChainCargoType.InsulinInjectables => new(ColdChainCargoType.InsulinInjectables, 2.0, 8.0, CriticalMaxMktCelsius: 9.0, MaxPermissibleExcursionMinutes: 30, MaxPermissibleShockG: 2.5),
        _ => new(ColdChainCargoType.AmbientPharmaceuticals, 15.0, 25.0, CriticalMaxMktCelsius: 28.0, MaxPermissibleExcursionMinutes: 120, MaxPermissibleShockG: 5.0)
    };
}
