namespace SGrowing.Api.Modules.Agriculture.PrecisionAgrigenomics;

public class PrecisionAgrigenomicsEngine
{
    public AgrigenomicsPrescriptionResult GeneratePrescriptions(AgrigenomicsPrescriptionRequest request)
    {
        var surveysMap = request.DroneSurveys.ToDictionary(s => s.ZoneId);
        var prescriptions = new List<ZonePrescription>();

        // Simplified FAO-56 Penman-Monteith daily evapotranspiration (ET0 in mm/day)
        var w = request.Weather;
        double vaporPressureDeficit = (1.0 - (w.RelativeHumidityPercent / 100.0)) * 2.5;
        double et0 = Math.Round(Math.Max(1.0, (0.025 * w.TemperatureCelsius + 0.15) * (w.SolarRadiationMjPerM2 * 0.18) + (w.WindSpeedMps * 0.35 * vaporPressureDeficit)), 2);

        double totalArea = 0.0;
        double totalNitrogenKg = 0.0;

        foreach (var zone in request.FieldZones)
        {
            totalArea += zone.AreaHectares;
            surveysMap.TryGetValue(zone.ZoneId, out var survey);

            double ndvi = survey?.Ndvi ?? 0.60;
            double ndre = survey?.Ndre ?? 0.35;

            string vigor;
            double nitrogenKgPerHa;
            double phosphorusKgPerHa;
            double potassiumKgPerHa;

            if (ndvi >= 0.75)
            {
                vigor = "Dense Canopy: Optimal Vigor";
                nitrogenKgPerHa = 60.0; // Maintenance dose
                phosphorusKgPerHa = 30.0;
                potassiumKgPerHa = 40.0;
            }
            else if (ndvi >= 0.50)
            {
                vigor = "Moderate Vegetative Vigor";
                nitrogenKgPerHa = 100.0; // Standard top-dressing
                phosphorusKgPerHa = 40.0;
                potassiumKgPerHa = 50.0;
            }
            else
            {
                vigor = "Stressed: Chlorotic Deficiency";
                // High nitrogen remediation to restore chlorophyll synthesis
                nitrogenKgPerHa = 140.0;
                phosphorusKgPerHa = 55.0;
                potassiumKgPerHa = 70.0;
            }

            // Adjust yield prediction from vegetative indices
            double yieldFactor = Math.Clamp(0.50 + (ndvi * 0.65), 0.50, 1.40);
            double predictedYield = Math.Round(zone.HistoricalYieldTonsPerHa * yieldFactor, 2);

            prescriptions.Add(new ZonePrescription(
                ZoneId: zone.ZoneId,
                CropType: zone.CropType,
                PredictedYieldTonsPerHa: predictedYield,
                NitrogenKgPerHa: nitrogenKgPerHa,
                PhosphorusKgPerHa: phosphorusKgPerHa,
                PotassiumKgPerHa: potassiumKgPerHa,
                EvapotranspirationMmPerDay: et0,
                VigorStatus: vigor
            ));

            totalNitrogenKg += nitrogenKgPerHa * zone.AreaHectares;
        }

        decimal totalCost = Math.Round((decimal)(totalNitrogenKg * request.NitrogenPricePerKgEur), 2);

        string advice;
        if (request.FieldZones.Any(z => surveysMap.TryGetValue(z.ZoneId, out var s) && s.Ndvi < 0.50))
        {
            advice = "Targeted Remediation: Chlorotic zones identified. Targeted variable-rate N prevents field-wide over-application.";
        }
        else
        {
            advice = "Field Uniformity High: Balanced canopy maintenance active. Nitrogen runoff minimized under EU Green Deal thresholds.";
        }

        return new AgrigenomicsPrescriptionResult(
            TotalZonesEvaluated: request.FieldZones.Count,
            TotalAreaHectares: Math.Round(totalArea, 2),
            TotalNitrogenRequiredKg: Math.Round(totalNitrogenKg, 2),
            TotalFertilizerCostEur: totalCost,
            Prescriptions: prescriptions,
            AgroEcologicalAdvice: advice
        );
    }
}
