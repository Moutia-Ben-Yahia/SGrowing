namespace SGrowing.Api.Modules.Sustainability.CircularRemanufacturing;

/// <summary>
/// Autonomous circular economy reverse remanufacturing and material disassembly yield engine.
/// Evaluates component Weibull reliability curves and calculates optimal disassembly economics.
/// </summary>
public sealed class DisassemblyYieldOptimizationEngine
{
    public ProductRemanufacturingYieldReport AnalyzeYield(AnalyzeProductYieldRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var recommendations = new List<DisassemblyPathRecommendation>();
        var totalLaborMinutes = 0;
        decimal totalGrossRecovery = 0m;
        decimal totalRemanufacturingCost = 0m;
        var totalCarbonAvoided = 0.0;
        var highValueYieldCount = 0;

        foreach (var c in request.Components)
        {
            totalLaborMinutes += c.DisassemblyLaborMinutes;
            var laborCost = Math.Round(((decimal)c.DisassemblyLaborMinutes / 60m) * request.HourlyLaborRateEur, 2);

            // 1. Calculate Weibull Reliability R(t) = exp(-(t/eta)^beta)
            var t = Math.Max(0.0, c.OperatingHoursUsed);
            var eta = Math.Max(1.0, c.WeibullScaleEtaHours);
            var beta = Math.Max(0.1, c.WeibullShapeBeta);
            var reliability = Math.Exp(-Math.Pow(t / eta, beta));

            PartDisposition disposition;
            decimal grossValue;
            decimal partCost;
            double carbonSaved;
            string rationale;

            if (reliability >= 0.85)
            {
                disposition = PartDisposition.DirectReuse;
                grossValue = c.ResaleValueEur;
                partCost = laborCost;
                carbonSaved = c.VirginMaterialCarbonKg * 0.95;
                rationale = $"HIGH RELIABILITY (R={reliability:F2} >= 0.85): Cleared for direct certified reuse.";
                highValueYieldCount++;
            }
            else if (reliability >= 0.50 && (c.ResaleValueEur - c.RefurbishCostEur) > c.RecyclingMeltValueEur)
            {
                disposition = PartDisposition.RefurbishRemanufacture;
                grossValue = c.ResaleValueEur;
                partCost = laborCost + c.RefurbishCostEur;
                carbonSaved = c.VirginMaterialCarbonKg * 0.80;
                rationale = $"MODERATE FATIGUE (R={reliability:F2}): Economically justified for factory refurbishing.";
                highValueYieldCount++;
            }
            else
            {
                disposition = PartDisposition.MaterialRecyclingScrap;
                grossValue = c.RecyclingMeltValueEur;
                partCost = laborCost;
                carbonSaved = c.VirginMaterialCarbonKg * 0.50;
                rationale = $"ADVANCED WEAR (R={reliability:F2} < 0.50): Route to material shredding and melt recycling.";
            }

            var netValue = grossValue - partCost;
            totalGrossRecovery += grossValue;
            totalRemanufacturingCost += partCost;
            totalCarbonAvoided += carbonSaved;

            recommendations.Add(new DisassemblyPathRecommendation(
                PartNumber: c.PartNumber,
                PartName: c.PartName,
                WeibullReliabilityScore: Math.Round(reliability, 3),
                RecommendedDisposition: disposition,
                NetEconomicValueEur: netValue,
                AvoidedVirginCarbonKgCo2: Math.Round(carbonSaved, 2),
                EngineeringRationale: rationale
            ));
        }

        var netProfit = totalGrossRecovery - totalRemanufacturingCost;
        var yieldPercent = request.Components.Count > 0
            ? ((double)highValueYieldCount / request.Components.Count) * 100.0
            : 0.0;

        return new ProductRemanufacturingYieldReport(
            ProductSerialNumber: request.ProductSerialNumber,
            ModelNumber: request.ModelNumber,
            TotalDisassemblyLaborMinutes: totalLaborMinutes,
            TotalGrossRecoveryValueEur: totalGrossRecovery,
            TotalRemanufacturingCostEur: totalRemanufacturingCost,
            NetCircularProfitEur: netProfit,
            OverallCircularYieldPercent: Math.Round(yieldPercent, 1),
            TotalVirginCarbonAvoidedKg: Math.Round(totalCarbonAvoided, 2),
            PartRecommendations: recommendations,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }
}
