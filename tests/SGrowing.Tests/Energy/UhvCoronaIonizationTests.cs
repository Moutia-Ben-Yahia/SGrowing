namespace SGrowing.Tests.Energy;

using FluentAssertions;
using SGrowing.Api.Modules.Energy.UhvCoronaIonization;
using Xunit;

public class UhvCoronaIonizationTests
{
    private readonly UhvCoronaIonizationEngine _engine = new();

    [Fact]
    public void EvaluateCoronaDischarge_FairWeatherNominal_ReportsMinimalLossAndStandardNoise()
    {
        var geom = new UhvConductorBundle(
            TransmissionLineId: "UHV-CHANGZHI-NANYANG-1100KV",
            LineVoltageKv: 1100.0,
            SubconductorsPerBundle: 8,
            SubconductorRadiusCm: 1.5,
            BundleSpacingCm: 45.0,
            LineLengthKm: 650.0
        );

        var weather = new AtmosphericWeatherCondition(
            AmbientTempCelsius: 20.0,
            BarometricPressureTorr: 760.0,
            RelativeHumidityPercent: 45.0,
            PrecipitationRateMmPerHour: 0.0
        );

        var request = new CoronaEvaluationRequest(geom, weather);
        var profile = _engine.EvaluateCoronaDischarge(request);

        profile.TransmissionLineId.Should().Be("UHV-CHANGZHI-NANYANG-1100KV");
        profile.WeatherRegime.Should().Be("FAIR_WEATHER");
        profile.CoronaPowerLossKwPerKm.Should().BeLessThan(25.0);
        profile.AudibleNoiseDba.Should().BeLessThan(45.0);
        profile.GridAdvisory.Should().Contain("NOMINAL_GRID_TRANSMISSION");
    }

    [Fact]
    public void EvaluateCoronaDischarge_HeavyPrecipitation_TriggersVoltageTrimAdvisory()
    {
        var geom = new UhvConductorBundle(
            TransmissionLineId: "UHV-JINGMEN-1100KV",
            LineVoltageKv: 1100.0,
            SubconductorsPerBundle: 8,
            SubconductorRadiusCm: 1.5,
            BundleSpacingCm: 45.0,
            LineLengthKm: 600.0
        );

        // Monsoon downpour (25 mm/h rain)
        var weather = new AtmosphericWeatherCondition(
            AmbientTempCelsius: 18.0,
            BarometricPressureTorr: 755.0,
            RelativeHumidityPercent: 98.0,
            PrecipitationRateMmPerHour: 25.0
        );

        var request = new CoronaEvaluationRequest(geom, weather);
        var profile = _engine.EvaluateCoronaDischarge(request);

        profile.WeatherRegime.Should().Be("HEAVY_PRECIPITATION");
        profile.CoronaPowerLossKwPerKm.Should().BeGreaterThan(100.0);
        profile.TotalTransmissionLossMw.Should().BeGreaterThan(50.0);
        profile.GridAdvisory.Should().Contain("CORONA_LOSS_CRITICAL");
        profile.GridAdvisory.Should().Contain("trim bus voltage");
    }

    [Fact]
    public void EvaluateCoronaDischarge_HumidFog_ElevatesAudibleNoise()
    {
        var geom = new UhvConductorBundle();
        var weather = new AtmosphericWeatherCondition(10.0, 760.0, RelativeHumidityPercent: 92.0, PrecipitationRateMmPerHour: 0.0);

        var request = new CoronaEvaluationRequest(geom, weather);
        var profile = _engine.EvaluateCoronaDischarge(request);

        profile.WeatherRegime.Should().Be("HUMID_FOG");
        profile.CoronaPowerLossKwPerKm.Should().BeGreaterThan(30.0);
        profile.AudibleNoiseDba.Should().BeGreaterThan(50.0);
        profile.GridAdvisory.Should().Contain("CORONA_ELEVATED");
    }

    [Fact]
    public void EvaluateCoronaDischarge_AltitudePressureDrop_ReducesAirDensityDelta()
    {
        var geom = new UhvConductorBundle();
        // High altitude pass (e.g. 2500m elevation -> 560 Torr)
        var seaLevel = new AtmosphericWeatherCondition(15.0, 760.0, 50.0, 0.0);
        var highAltitude = new AtmosphericWeatherCondition(15.0, 560.0, 50.0, 0.0);

        var seaProfile = _engine.EvaluateCoronaDischarge(new CoronaEvaluationRequest(geom, seaLevel));
        var altProfile = _engine.EvaluateCoronaDischarge(new CoronaEvaluationRequest(geom, highAltitude));

        altProfile.RelativeAirDensityDelta.Should().BeLessThan(seaProfile.RelativeAirDensityDelta);
        altProfile.CriticalDisruptiveGradientKvPerCm.Should().BeLessThan(seaProfile.CriticalDisruptiveGradientKvPerCm);
    }
}
