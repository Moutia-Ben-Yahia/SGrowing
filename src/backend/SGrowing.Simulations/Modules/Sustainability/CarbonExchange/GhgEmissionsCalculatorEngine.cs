namespace SGrowing.Api.Modules.Sustainability.CarbonExchange;

/// <summary>
/// Autonomous GHG Protocol emissions calculator computing Scope 1 direct combustion,
/// Scope 2 electricity consumption (location- and market-based), Scope 3 freight ton-km,
/// and EU CBAM border carbon tariff liabilities.
/// </summary>
public sealed class GhgEmissionsCalculatorEngine
{
    // IPCC / DEFRA standard emission factors (kg CO2e per unit)
    public const double DieselFactorKgPerLiter = 2.68;
    public const double GridElectricityFactorKgPerKwh = 0.475; // Standard North African / Southern Mediterranean mix

    public const double DieselTruckTonKmFactor = 0.095;
    public const double ElectricFreightTonKmFactor = 0.018;
    public const double RailTonKmFactor = 0.024;
    public const double MaritimeTonKmFactor = 0.015;
    public const double AirCargoTonKmFactor = 0.602;

    public GhgAssessmentResult CalculateFootprint(CalculateFootprintRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var items = new List<CarbonFootprintItem>();

        // Scope 1: Diesel combustion in internal fleet
        var s1Emissions = request.DieselLitersBurned * DieselFactorKgPerLiter;
        items.Add(new CarbonFootprintItem(
            ItemId: $"S1-{Guid.NewGuid():N}"[..8],
            Scope: EmissionScope.Scope1DirectFuel,
            Description: "Diesel fuel fleet combustion",
            ActivityValue: request.DieselLitersBurned,
            Unit: "Liters",
            EmissionFactorKgCo2ePerUnit: DieselFactorKgPerLiter,
            TotalEmissionsKgCo2e: s1Emissions
        ));

        // Scope 2: Grid electricity
        var grossS2 = request.GridElectricityKwh * GridElectricityFactorKgPerKwh;
        var coverage = Math.Clamp(request.GridRecCoverageRatio, 0.0, 1.0);
        var s2Offset = grossS2 * coverage;
        var netS2 = grossS2 - s2Offset;

        items.Add(new CarbonFootprintItem(
            ItemId: $"S2-{Guid.NewGuid():N}"[..8],
            Scope: EmissionScope.Scope2PurchasedElectricity,
            Description: "Purchased electricity (Grid mix)",
            ActivityValue: request.GridElectricityKwh,
            Unit: "kWh",
            EmissionFactorKgCo2ePerUnit: GridElectricityFactorKgPerKwh,
            TotalEmissionsKgCo2e: grossS2
        ));

        // Scope 3: Logistics ton-kilometers
        var s3Factor = request.FreightMode switch
        {
            TransportModeEmissions.ElectricFreight => ElectricFreightTonKmFactor,
            TransportModeEmissions.FreightRail => RailTonKmFactor,
            TransportModeEmissions.MaritimeContainer => MaritimeTonKmFactor,
            TransportModeEmissions.AirCargo => AirCargoTonKmFactor,
            _ => DieselTruckTonKmFactor
        };

        var s3Emissions = request.FreightTonKilometers * s3Factor;
        items.Add(new CarbonFootprintItem(
            ItemId: $"S3-{Guid.NewGuid():N}"[..8],
            Scope: EmissionScope.Scope3LogisticsSupplyChain,
            Description: $"Freight transport ({request.FreightMode})",
            ActivityValue: request.FreightTonKilometers,
            Unit: "Ton-km",
            EmissionFactorKgCo2ePerUnit: s3Factor,
            TotalEmissionsKgCo2e: s3Emissions
        ));

        var totalGross = s1Emissions + grossS2 + s3Emissions;
        var netEmissions = s1Emissions + netS2 + s3Emissions;

        // EU CBAM Border Adjustment: Tariff on emissions exceeding benchmark
        // Embedded emissions in tonnes = netEmissions / 1000
        var netTonnes = netEmissions / 1000.0;
        var excessTonnes = Math.Max(0.0, netTonnes - request.EmbeddedEmissionsBenchmarkTonne);
        var cbamTax = excessTonnes * request.EuEtsPriceEurPerTonne;

        return new GhgAssessmentResult(
            AssessmentId: $"GHG-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            TenantId: request.TenantId,
            TimestampUtc: DateTime.UtcNow,
            Scope1TotalKgCo2e: s1Emissions,
            Scope2TotalKgCo2e: grossS2,
            Scope3TotalKgCo2e: s3Emissions,
            TotalGrossEmissionsKgCo2e: totalGross,
            Scope2RecOffsetKgCo2e: s2Offset,
            NetEmissionsKgCo2e: netEmissions,
            CbamTaxExposureEur: Math.Round(cbamTax, 2),
            LineItems: items
        );
    }
}
