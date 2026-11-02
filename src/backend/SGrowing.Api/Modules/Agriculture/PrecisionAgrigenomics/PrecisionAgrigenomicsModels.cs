namespace SGrowing.Api.Modules.Agriculture.PrecisionAgrigenomics;

public record FarmFieldZone(
    string ZoneId,
    string CropType,
    double AreaHectares,
    double SoilPh,
    double SoilMoistureFraction,
    double HistoricalYieldTonsPerHa);

public record DroneVegetativeIndices(
    string ZoneId,
    double Ndvi,
    double Ndre,
    double Savi,
    DateTimeOffset SurveyUtc);

public record WeatherObservation(
    double TemperatureCelsius,
    double RelativeHumidityPercent,
    double SolarRadiationMjPerM2,
    double WindSpeedMps);

public record AgrigenomicsPrescriptionRequest(
    List<FarmFieldZone> FieldZones,
    List<DroneVegetativeIndices> DroneSurveys,
    WeatherObservation Weather,
    double NitrogenPricePerKgEur = 1.20);

public record ZonePrescription(
    string ZoneId,
    string CropType,
    double PredictedYieldTonsPerHa,
    double NitrogenKgPerHa,
    double PhosphorusKgPerHa,
    double PotassiumKgPerHa,
    double EvapotranspirationMmPerDay,
    string VigorStatus);

public record AgrigenomicsPrescriptionResult(
    int TotalZonesEvaluated,
    double TotalAreaHectares,
    double TotalNitrogenRequiredKg,
    decimal TotalFertilizerCostEur,
    List<ZonePrescription> Prescriptions,
    string AgroEcologicalAdvice);
