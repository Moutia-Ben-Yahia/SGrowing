namespace SGrowing.Api.Modules.Logistics.Routing;

/// <summary>
/// Physics-based energy simulator and charging stop coordinator for electric commercial freight fleets.
/// Accounts for rolling resistance, aerodynamic drag, elevation gradients, regenerative braking,
/// and battery thermal load in extreme desert/mountain environments.
/// </summary>
public sealed class EvFleetEnergyRoutingService
{
    private const double Gravity = 9.81;
    private const double AirDensityKgM3 = 1.225;
    private const double DragCoefficient = 0.50; // Optimized commercial freight aero profile
    private const double FrontalAreaM2 = 8.0;
    private const double RollingResistanceCoeff = 0.007;
    private const double DrivetrainEfficiency = 0.92;
    private const double FastChargerPowerKw = 150.0;
    private const decimal CommercialElectricityRateDzdPerKwh = 24.50m;

    /// <summary>
    /// Computes full energy consumption, regenerative capture, and optimal charging stops along an EV route.
    /// </summary>
    public EvEnergyRoutePlan CalculateEvEnergyPlan(CalculateEvEnergyRouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var baseRoute = request.BaseRoute;
        var vehicle = request.VehicleProfile;

        var currentSoCKwh = vehicle.CurrentSoCKwh;
        var initialSoCPercent = vehicle.SoCPercent;
        var totalConsumedKwh = 0.0;
        var totalRegeneratedKwh = 0.0;
        var chargingStops = new List<EvChargingStop>();
        var rangeDepletedRisk = false;

        // Auxiliary power draw based on ambient temperature (AC in desert heat or battery pre-heating in mountain freeze)
        var auxPowerKw = vehicle.AuxiliaryPowerKw;
        if (vehicle.AmbientTemperatureCelsius > 40.0 || vehicle.AmbientTemperatureCelsius < 2.0)
        {
            auxPowerKw += 2.0; // Extra 2.0 kW for thermal management
        }

        foreach (var leg in baseRoute.Legs)
        {
            // Only road legs consume EV truck battery directly (rail/sea use multimodal external power)
            if (leg.Mode != TransportMode.RoadFreight)
            {
                continue;
            }

            var velocityMs = (leg.DistanceKm / Math.Max(leg.DurationHours, 0.1)) * (1000.0 / 3600.0);
            var durationSeconds = leg.DurationHours * 3600.0;
            var massKg = vehicle.TotalMassKg;

            // Highway rolling topography average net gradient ~0.1%
            var gradientPercent = 0.1;
            var slopeAngleRad = Math.Atan(gradientPercent / 100.0);

            // Forces
            var fRoll = RollingResistanceCoeff * massKg * Gravity * Math.Cos(slopeAngleRad);
            var fAero = 0.5 * AirDensityKgM3 * DragCoefficient * FrontalAreaM2 * Math.Pow(velocityMs, 2);
            var fGravity = massKg * Gravity * Math.Sin(slopeAngleRad);

            var fTotal = fRoll + fAero + fGravity;
            var auxEnergyKwh = auxPowerKw * leg.DurationHours;

            double legTractionKwh;
            if (fTotal > 0)
            {
                // Traction mode (consuming power)
                var mechanicalPowerWatts = fTotal * velocityMs;
                var electricalPowerWatts = mechanicalPowerWatts / DrivetrainEfficiency;
                legTractionKwh = (electricalPowerWatts * durationSeconds) / 3_600_000.0;

                var legTotalConsumption = legTractionKwh + auxEnergyKwh;
                totalConsumedKwh += legTotalConsumption;

                // Check if battery is low before or during leg
                var reserveKwh = vehicle.BatteryCapacityKwh * (request.MinimumReserveSoCPercent / 100.0);
                if (currentSoCKwh <= reserveKwh || currentSoCKwh < legTotalConsumption)
                {
                    // Schedule fast-charging stop before exhaustion
                    var targetSoCKwh = vehicle.BatteryCapacityKwh * 0.85; // Charge up to 85%
                    var energyNeededKwh = Math.Max(0.0, targetSoCKwh - Math.Max(0.0, currentSoCKwh));
                    var chargingDurationHours = energyNeededKwh / FastChargerPowerKw;
                    var chargingDurationMinutes = Math.Round(chargingDurationHours * 60.0, 1);
                    var costDzd = (decimal)energyNeededKwh * CommercialElectricityRateDzdPerKwh;

                    chargingStops.Add(new EvChargingStop(
                        HubNodeId: leg.FromNodeId,
                        ArrivalSoCPercent: Math.Round((currentSoCKwh / vehicle.BatteryCapacityKwh) * 100.0, 1),
                        TargetSoCPercent: 85.0,
                        EnergyChargedKwh: Math.Round(energyNeededKwh, 2),
                        ChargingTimeMinutes: chargingDurationMinutes,
                        ChargingCostDzd: costDzd
                    ));

                    currentSoCKwh = targetSoCKwh;
                }

                currentSoCKwh -= legTotalConsumption;
            }
            else
            {
                // Downhill descent: regenerative braking
                var recoveredWatts = Math.Abs(fTotal) * velocityMs * vehicle.RegenerativeEfficiencyRatio;
                var legRegeneratedKwh = (recoveredWatts * durationSeconds) / 3_600_000.0;

                totalRegeneratedKwh += legRegeneratedKwh;
                totalConsumedKwh += auxEnergyKwh;
                currentSoCKwh += (legRegeneratedKwh - auxEnergyKwh);

                // Cannot charge above 100% capacity
                currentSoCKwh = Math.Min(currentSoCKwh, vehicle.BatteryCapacityKwh);
            }

            // Final check on leg completion
            if (currentSoCKwh < 0.0)
            {
                rangeDepletedRisk = true;
                currentSoCKwh = 0.0;
            }
        }

        var finalSoCPercent = Math.Clamp((currentSoCKwh / vehicle.BatteryCapacityKwh) * 100.0, 0.0, 100.0);
        var netEnergyKwh = totalConsumedKwh - totalRegeneratedKwh;

        return new EvEnergyRoutePlan(
            RouteId: baseRoute.RouteId,
            VehicleId: vehicle.VehicleId,
            StartingSoCPercent: Math.Round(initialSoCPercent, 1),
            EndingSoCPercent: Math.Round(finalSoCPercent, 1),
            TotalEnergyConsumedKwh: Math.Round(totalConsumedKwh, 2),
            TotalEnergyRegeneratedKwh: Math.Round(totalRegeneratedKwh, 2),
            NetEnergyKwh: Math.Round(netEnergyKwh, 2),
            RequiredChargingStops: chargingStops,
            RangeDepletedRisk: rangeDepletedRisk
        );
    }
}
