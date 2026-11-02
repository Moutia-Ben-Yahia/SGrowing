namespace SGrowing.Api.Modules.Energy.BessArbitrage;

/// <summary>
/// Autonomous Battery Energy Storage System (BESS) peak shaving and TOU arbitrage optimization engine.
/// Minimizes grid electricity costs by shaving demand spikes and arbitrating day-ahead price spreads.
/// </summary>
public sealed class BessPeakShavingArbitrageEngine
{
    public BessArbitrageSchedulePlan OptimizeSchedule(OptimizeBessScheduleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var specs = request.Specs;
        var tariffs = request.Tariffs.OrderBy(t => t.HourOfDay).ToList();

        if (tariffs.Count != 24)
        {
            throw new ArgumentException("Full 24-hour tariff profile required for dispatch schedule.");
        }

        var peakDemandBefore = tariffs.Max(t => t.FacilityDemandKw);
        var averagePrice = tariffs.Average(t => (double)t.GridEnergyPricePerKwhEur);
        var lowPriceThreshold = (decimal)(averagePrice * 0.85);

        // Target shaved peak (seek ~20% shaving or max discharge limit)
        var targetPeak = Math.Max(peakDemandBefore - specs.MaxDischargePowerKw, peakDemandBefore * 0.80);

        var currentSoC = specs.InitialSoC;
        var etaSqrt = Math.Sqrt(specs.RoundTripEfficiency);

        var dispatches = new List<BessHourlyDispatch>();
        decimal totalCostBefore = 0m;
        decimal totalCostAfter = 0m;
        double totalDischargedKwh = 0.0;

        for (var h = 0; h < 24; h++)
        {
            var hour = tariffs[h];
            var demand = hour.FacilityDemandKw;
            var price = hour.GridEnergyPricePerKwhEur;

            var costWithout = (decimal)demand * price;
            totalCostBefore += costWithout;

            var mode = BatteryOperatingMode.Idle;
            var batteryKw = 0.0;
            var netGridKw = demand;

            // 1. Discharge logic: Peak demand spike or high tariff window
            if (demand > targetPeak || hour.IsPeakTariffWindow)
            {
                var neededKw = demand > targetPeak ? demand - targetPeak : Math.Min(demand * 0.5, specs.MaxDischargePowerKw);
                var dischargePower = Math.Min(neededKw, specs.MaxDischargePowerKw);

                // Available stored energy above min SoC
                var availableEnergyKwh = (currentSoC - specs.MinAllowedSoC) * specs.TotalCapacityKwh;
                var maxDeliveredKw = availableEnergyKwh * etaSqrt;

                dischargePower = Math.Min(dischargePower, maxDeliveredKw);

                if (dischargePower > 0.1)
                {
                    mode = BatteryOperatingMode.Discharge;
                    batteryKw = dischargePower;
                    netGridKw = demand - dischargePower;

                    // Battery internal energy depletion
                    var energyDepletedKwh = dischargePower / etaSqrt;
                    currentSoC -= (energyDepletedKwh / specs.TotalCapacityKwh);
                    totalDischargedKwh += dischargePower;
                }
            }
            // 2. Charge logic: Cheap off-peak window with headroom below max SoC
            else if (price <= lowPriceThreshold && currentSoC < specs.MaxAllowedSoC)
            {
                var headroomKwh = (specs.MaxAllowedSoC - currentSoC) * specs.TotalCapacityKwh;
                var maxChargeKw = headroomKwh / etaSqrt;
                var chargePower = Math.Min(specs.MaxChargePowerKw, maxChargeKw);

                if (chargePower > 0.1)
                {
                    mode = BatteryOperatingMode.Charge;
                    batteryKw = chargePower;
                    netGridKw = demand + chargePower; // Grid supplies facility + battery charging

                    // Battery internal energy replenishment
                    var energyStoredKwh = chargePower * etaSqrt;
                    currentSoC += (energyStoredKwh / specs.TotalCapacityKwh);
                }
            }

            // Clamp SoC
            currentSoC = Math.Clamp(currentSoC, specs.MinAllowedSoC, specs.MaxAllowedSoC);

            var costWith = (decimal)netGridKw * price;
            totalCostAfter += costWith;

            dispatches.Add(new BessHourlyDispatch(
                HourOfDay: h,
                GrossFacilityDemandKw: demand,
                Mode: mode,
                BatteryPowerKw: batteryKw,
                NetGridDemandKw: netGridKw,
                ResultingSoC: currentSoC,
                CostWithoutBessEur: costWithout,
                CostWithBessEur: costWith
            ));
        }

        var peakDemandAfter = dispatches.Max(d => d.NetGridDemandKw);
        var peakShaved = Math.Max(0.0, peakDemandBefore - peakDemandAfter);

        // Amortized daily peak demand charge savings
        var peakDemandChargeReduction = (decimal)peakShaved * (request.MonthlyPeakChargePerKwEur / 30m);
        var energyArbitrageSavings = totalCostBefore - totalCostAfter;
        var totalDailySavings = energyArbitrageSavings + peakDemandChargeReduction;

        return new BessArbitrageSchedulePlan(
            PlanId: $"BESS-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            BatteryId: specs.BatteryId,
            TotalEnergyCostBeforeEur: totalCostBefore,
            TotalEnergyCostAfterEur: totalCostAfter,
            PeakDemandChargeReductionEur: peakDemandChargeReduction,
            TotalDailyNetSavingsEur: totalDailySavings,
            PeakDemandBeforeKw: peakDemandBefore,
            PeakDemandAfterKw: peakDemandAfter,
            PeakShavedKw: peakShaved,
            TotalDischargedKwh: totalDischargedKwh,
            HourlySchedule: dispatches,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }
}
