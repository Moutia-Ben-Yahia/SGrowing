namespace SGrowing.Api.Modules.Insurance.ParametricUnderwriting;

/// <summary>
/// Autonomous parametric catastrophe insurance adjudication and Solvency II capital adequacy oracle engine.
/// Computes physical attenuation curves and triggers automated claims settlements.
/// </summary>
public sealed class ParametricCatastropheOracleEngine
{
    private const double EarthRadiusKm = 6371.0;
    private const double SolvencyIiMinimumRatio = 150.0; // 150% capital coverage requirement

    public IReadOnlyList<ParametricClaimSettlement> AdjudicateEvent(AdjudicateEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var evt = request.OracleEvent;
        var settlements = new List<ParametricClaimSettlement>();

        foreach (var policy in request.ActivePolicies.Where(p => p.IsActive && p.Peril == evt.Peril))
        {
            var distanceKm = CalculateHaversineDistanceKm(evt.EpicenterLocation, policy.Location);

            // Compute attenuation based on peril physics
            var decayConstant = policy.Peril switch
            {
                CatastropheRiskType.EarthquakeRichter => 0.0075,
                CatastropheRiskType.TropicalCycloneWindKmh => 0.0050,
                CatastropheRiskType.ExtremePrecipitationMm => 0.0060,
                _ => 0.0040
            };

            var effectiveIntensity = evt.RawIntensityValue * Math.Exp(-decayConstant * distanceKm);
            var isTriggered = effectiveIntensity >= policy.TriggerThreshold;

            decimal payoutAmount = 0m;
            string summary;

            if (isTriggered)
            {
                var ceilingSpread = Math.Max(0.1, policy.CatastrophicCeiling - policy.TriggerThreshold);
                var scalingRatio = (effectiveIntensity - policy.TriggerThreshold) / ceilingSpread;
                var payoutFraction = Math.Clamp(scalingRatio, 0.25, 1.0); // Minimum 25% payout once trigger breached

                payoutAmount = policy.MaxSumInsuredEur * (decimal)payoutFraction;
                summary = $"PARAMETRIC PAYOUT APPROVED: Epicenter distance {distanceKm:F1}km. Attenuated intensity {effectiveIntensity:F2} exceeded threshold {policy.TriggerThreshold:F2}. Payout: {payoutFraction * 100:F1}% of sum insured.";
            }
            else
            {
                summary = $"NO PAYOUT: Epicenter distance {distanceKm:F1}km. Attenuated intensity {effectiveIntensity:F2} below trigger threshold {policy.TriggerThreshold:F2}.";
            }

            settlements.Add(new ParametricClaimSettlement(
                ClaimId: $"CLM-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                PolicyId: policy.PolicyId,
                TenantId: policy.TenantId,
                DistanceToEpicenterKm: Math.Round(distanceKm, 2),
                EffectiveLocalIntensity: Math.Round(effectiveIntensity, 2),
                TriggerThreshold: policy.TriggerThreshold,
                PayoutTriggered: isTriggered,
                SettlementAmountEur: payoutAmount,
                AdjudicationSummary: summary,
                SettledAtUtc: DateTime.UtcNow
            ));
        }

        return settlements;
    }

    public SolvencyAdequacyAuditResult AuditSolvencyAdequacy(AuditSolvencyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var totalSumInsured = request.ActivePolicies.Where(p => p.IsActive).Sum(p => p.MaxSumInsuredEur);

        // Solvency II standard formula for Catastrophe Risk: Probable Maximum Loss (1-in-200 year event) = 25% of active exposure
        var stressPml = totalSumInsured * 0.25m;
        var solvencyRatio = stressPml > 0 ? (double)(request.CapitalReservesEur / stressPml) * 100.0 : 999.0;
        var isCompliant = solvencyRatio >= SolvencyIiMinimumRatio;

        return new SolvencyAdequacyAuditResult(
            PoolId: request.PoolId,
            TotalCapitalReservesEur: request.CapitalReservesEur,
            TotalSumInsuredExposureEur: totalSumInsured,
            StressTestPmlEur: stressPml,
            SolvencyRatioPercent: Math.Round(solvencyRatio, 2),
            IsSolvencyIiCompliant: isCompliant,
            AuditedAtUtc: DateTime.UtcNow
        );
    }

    private static double CalculateHaversineDistanceKm(AssetGeoLocation a, AssetGeoLocation b)
    {
        var dLat = (b.Latitude - a.Latitude) * Math.PI / 180.0;
        var dLon = (b.Longitude - a.Longitude) * Math.PI / 180.0;
        var lat1 = a.Latitude * Math.PI / 180.0;
        var lat2 = b.Latitude * Math.PI / 180.0;

        var h = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        return 2.0 * EarthRadiusKm * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1.0 - h));
    }
}
