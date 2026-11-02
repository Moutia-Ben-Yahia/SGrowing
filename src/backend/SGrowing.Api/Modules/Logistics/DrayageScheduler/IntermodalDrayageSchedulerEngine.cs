namespace SGrowing.Api.Modules.Logistics.DrayageScheduler;

/// <summary>
/// Autonomous intermodal port drayage scheduling engine.
/// Optimizes container pickups to eliminate demurrage fees while respecting driver HOS,
/// terminal gate congestion, and chassis compatibility.
/// </summary>
public sealed class IntermodalDrayageSchedulerEngine
{
    private const double BaseTurnTimeMinutes = 45.0;
    private const double AverageUrbanTruckSpeedKmh = 40.0;

    public DrayageSchedulePlan GenerateOptimalSchedule(ScheduleDrayageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = request.PlanningHorizonUtc;
        var availableTruckers = request.Truckers.Where(t => t.IsAvailable).ToList();
        var mutableSlots = request.GateSlots.Select(s => new MutableSlot(s)).ToList();

        // Sort containers by Slack (earliest expiry first, highest daily penalty rate breaking ties)
        var prioritizedContainers = request.Containers
            .OrderBy(c => c.FreeTimeExpiryUtc)
            .ThenByDescending(c => c.DailyDemurrageRateUsd)
            .ThenByDescending(c => c.PriorityUrgency)
            .ToList();

        var missions = new List<DrayageDispatchMission>();
        var unassigned = new List<string>();
        decimal totalDemurrageIncurred = 0m;
        decimal totalDemurrageSaved = 0m;

        foreach (var container in prioritizedContainers)
        {
            // 1. Find compatible trucker
            var matchedTruckerIndex = availableTruckers.FindIndex(t =>
                IsChassisCompatible(container, t.CurrentChassis) &&
                t.RemainingHoursOfServiceMinutes >= 120);

            if (matchedTruckerIndex < 0)
            {
                unassigned.Add(container.ContainerId);
                continue;
            }

            var trucker = availableTruckers[matchedTruckerIndex];

            // 2. Find eligible gate slot at matching terminal
            var transitMinutes = (int)Math.Ceiling((trucker.CurrentDistanceToTerminalKm / AverageUrbanTruckSpeedKmh) * 60.0);
            var earliestArrival = now.AddMinutes(transitMinutes);

            var slot = mutableSlots
                .Where(s => s.Slot.TerminalCode.Equals(container.TerminalCode, StringComparison.OrdinalIgnoreCase) &&
                            s.RemainingCapacity > 0 &&
                            s.Slot.WindowEndUtc >= earliestArrival)
                .OrderBy(s => s.Slot.WindowStartUtc)
                .FirstOrDefault();

            if (slot == null)
            {
                unassigned.Add(container.ContainerId);
                continue;
            }

            // Book slot
            slot.RemainingCapacity--;

            var scheduledPickup = earliestArrival > slot.Slot.WindowStartUtc ? earliestArrival : slot.Slot.WindowStartUtc;
            var estimatedTurnTime = (int)Math.Ceiling(BaseTurnTimeMinutes * slot.Slot.CongestionIndex);

            // Compute Demurrage Incurred
            decimal demurrage = 0m;
            if (scheduledPickup > container.FreeTimeExpiryUtc)
            {
                var overdueHours = (scheduledPickup - container.FreeTimeExpiryUtc).TotalHours;
                var overdueDays = (decimal)Math.Ceiling(overdueHours / 24.0);
                demurrage = overdueDays * container.DailyDemurrageRateUsd;
            }

            // Estimate avoided demurrage (assuming average 4-day port dwell delay if unassigned)
            var counterfactualOverdueDays = 4m;
            var counterfactualDemurrage = counterfactualOverdueDays * container.DailyDemurrageRateUsd;
            var saved = Math.Max(0m, counterfactualDemurrage - demurrage);

            totalDemurrageIncurred += demurrage;
            totalDemurrageSaved += saved;

            var mission = new DrayageDispatchMission(
                MissionId: $"DRY-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                ContainerId: container.ContainerId,
                TruckerId: trucker.TruckerId,
                SlotId: slot.Slot.SlotId,
                ScheduledPickupUtc: scheduledPickup,
                EstimatedTurnTimeMinutes: estimatedTurnTime,
                IncurredDemurrageUsd: demurrage,
                DispatchNotes: $"Assigned to {trucker.DriverName} via slot {slot.Slot.SlotId}. Turn: {estimatedTurnTime}m. Demurrage avoided: ${saved:F0}"
            );

            missions.Add(mission);

            // Deduct trucker HOS and mark busy for this mission
            var totalMissionTime = transitMinutes + estimatedTurnTime;
            availableTruckers[matchedTruckerIndex] = trucker with
            {
                RemainingHoursOfServiceMinutes = trucker.RemainingHoursOfServiceMinutes - totalMissionTime
            };
        }

        return new DrayageSchedulePlan(
            PlanId: $"DSP-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            TotalContainersScheduled: missions.Count,
            TotalDemurrageIncurredUsd: totalDemurrageIncurred,
            TotalDemurrageSavedUsd: totalDemurrageSaved,
            Missions: missions,
            UnassignedContainerIds: unassigned,
            GeneratedAtUtc: DateTime.UtcNow
        );
    }

    private static bool IsChassisCompatible(ContainerCargoRecord container, ChassisType chassis)
    {
        if (container.RequiresGensetReefer ||
            container.Size is ContainerSize.ReeferTwentyFoot or ContainerSize.ReeferFortyFoot)
        {
            return chassis == ChassisType.ReeferGensetChassis;
        }

        if (container.GrossWeightKg > 22000.0)
        {
            return chassis is ChassisType.TriAxleHeavy or ChassisType.ReeferGensetChassis;
        }

        return true;
    }

    private sealed class MutableSlot
    {
        public TerminalGateSlot Slot { get; }
        public int RemainingCapacity { get; set; }

        public MutableSlot(TerminalGateSlot slot)
        {
            Slot = slot;
            RemainingCapacity = slot.AvailableCapacity;
        }
    }
}
