using FluentAssertions;
using SGrowing.Api.Modules.IoT.ColdChainSla;
using Xunit;

namespace SGrowing.Tests.IoT;

public sealed class ColdChainSlaTests
{
    private readonly MeanKineticTemperatureCalculatorEngine _mktEngine = new();
    private readonly CarrierColdChainSlaArbiterService _arbiter;

    public ColdChainSlaTests()
    {
        _arbiter = new CarrierColdChainSlaArbiterService(_mktEngine);
    }

    #region MKT Calculation & Compliant Shipment Tests

    [Fact]
    public void AdjudicateShipment_NominalVaccinesColdChain_ReportsZeroPenaltiesAndCompliant()
    {
        var t0 = DateTime.UtcNow;
        var samples = new List<TemperatureSensorSample>();

        // 6 samples over 1 hour, hovering between 3.5C and 5.2C (well within [2C, 8C])
        for (var i = 0; i < 6; i++)
        {
            samples.Add(new TemperatureSensorSample(
                SensorId: "SENS-VACCINE-01",
                ShipmentId: "SHIP-ALGIERS-PHARMA",
                CarrierId: "CARRIER-YALIDINE-COLD",
                TimestampUtc: t0.AddMinutes(i * 10),
                TemperatureCelsius: 4.5 + (0.5 * (i % 2 == 0 ? 1 : -1)),
                RelativeHumidityPercent: 65.0,
                ShockGForce: 1.2
            ));
        }

        var req = new AuditShipmentColdChainRequest(
            ShipmentId: "SHIP-ALGIERS-PHARMA",
            CarrierId: "CARRIER-YALIDINE-COLD",
            CargoType: ColdChainCargoType.VaccinesAndBiologics,
            TotalCargoInvoiceValueEur: 50000.0m,
            TelemetrySamples: samples
        );

        var report = _arbiter.AdjudicateShipment(req);

        report.Should().NotBeNull();
        report.SlaBreached.Should().BeFalse();
        report.TotalExcursionMinutes.Should().Be(0);
        report.MeanKineticTemperatureCelsius.Should().BeInRange(4.0, 5.5);
        report.CarrierPenaltyDeductionEur.Should().Be(0.0m);
        report.AdjudicationVerdict.Should().StartWith("COMPLIANT");
        report.ImmutableAuditProofHex.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region Excursion & Spoilage Penalty Tests

    [Fact]
    public void AdjudicateShipment_SevereHeatExcursion_TriggersTotalLossAndFullCarrierPenalty()
    {
        var t0 = DateTime.UtcNow;
        var samples = new List<TemperatureSensorSample>();

        // Refrigeration unit failed: Temperature climbed to 28C for 3 hours (18 samples at 10m intervals)
        for (var i = 0; i < 18; i++)
        {
            samples.Add(new TemperatureSensorSample(
                SensorId: "SENS-INSULIN-09",
                ShipmentId: "SHIP-INSULIN-HOT",
                CarrierId: "CARRIER-REFRIG-FAIL",
                TimestampUtc: t0.AddMinutes(i * 10),
                TemperatureCelsius: 24.0 + (i * 0.4), // 24C to 30.8C
                RelativeHumidityPercent: 40.0,
                ShockGForce: 1.0
            ));
        }

        var req = new AuditShipmentColdChainRequest(
            ShipmentId: "SHIP-INSULIN-HOT",
            CarrierId: "CARRIER-REFRIG-FAIL",
            CargoType: ColdChainCargoType.InsulinInjectables,
            TotalCargoInvoiceValueEur: 80000.0m,
            TelemetrySamples: samples
        );

        var report = _arbiter.AdjudicateShipment(req);

        report.SlaBreached.Should().BeTrue();
        report.TotalExcursionMinutes.Should().Be(180);
        report.MeanKineticTemperatureCelsius.Should().BeGreaterThan(20.0);
        report.SpoilageRiskPercent.Should().Be(100.0);
        report.CarrierPenaltyDeductionEur.Should().Be(80000.0m, "Biological denaturation triggers full cargo invoice deduction.");
        report.AdjudicationVerdict.Should().StartWith("TOTAL LOSS");
    }

    [Fact]
    public void AdjudicateShipment_KineticShockExcess_AssessesHandlingPenalty()
    {
        var t0 = DateTime.UtcNow;
        // Perfect temperature (4C) but dropped pallet (6.5G shock)
        var samples = new List<TemperatureSensorSample>
        {
            new("SENS-01", "SHIP-SHOCKED", "CARRIER-DROP", t0, 4.0, 60.0, 6.5)
        };

        var req = new AuditShipmentColdChainRequest(
            ShipmentId: "SHIP-SHOCKED",
            CarrierId: "CARRIER-DROP",
            CargoType: ColdChainCargoType.VaccinesAndBiologics, // Max shock is 3.5G
            TotalCargoInvoiceValueEur: 20000.0m,
            TelemetrySamples: samples
        );

        var report = _arbiter.AdjudicateShipment(req);

        report.SlaBreached.Should().BeTrue();
        report.MaxShockGObserved.Should().Be(6.5);
        report.CarrierPenaltyDeductionEur.Should().Be(3000.0m); // 15% handling penalty
        report.AdjudicationVerdict.Should().StartWith("KINETIC SHOCK SLA");
    }

    #endregion
}
