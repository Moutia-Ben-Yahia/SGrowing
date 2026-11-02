namespace SGrowing.Api.Modules.Commerce.WarehouseSlotting;

/// <summary>
/// Autonomous warehouse slotting optimizer performing Pareto ABC velocity analysis,
/// ergonomic golden zone assignment, and pick-travel minimization.
/// </summary>
public sealed class DynamicSlottingOptimizerEngine
{
    public IReadOnlyList<SlottingAssignment> OptimizeSlotting(OptimizeSlottingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Profiles.Count == 0) return [];

        var totalPicks = request.Profiles.Sum(p => p.PicksPerDay);
        var sorted = request.Profiles.OrderByDescending(p => p.PicksPerDay).ToList();

        var cumulative = 0.0;
        var assignments = new List<SlottingAssignment>();

        var aisleCounter = 1;
        var bayCounter = 1;

        foreach (var profile in sorted)
        {
            cumulative += profile.PicksPerDay;
            var cumulativeShare = totalPicks > 0 ? (cumulative / totalPicks) : 1.0;

            var velocity = cumulativeShare switch
            {
                <= 0.85 => VelocityTier.FastA,
                <= 0.95 => VelocityTier.MediumB,
                _ => VelocityTier.SlowC
            };

            WarehouseRackZone assignedZone;
            double travelTime;
            string rationale;

            if (profile.WeightKg >= 15.0)
            {
                // Ergonomics rule: Heavy items (>15kg) must not be placed on overhead racks
                assignedZone = WarehouseRackZone.BottomLevel1;
                travelTime = velocity == VelocityTier.FastA ? 12.0 : 25.0;
                rationale = "Heavy item (>15kg) slotted at floor level for ergonomic lifting and pallet jack access.";
            }
            else if (velocity == VelocityTier.FastA)
            {
                assignedZone = WarehouseRackZone.GoldenZoneLevel2;
                travelTime = 8.5; // Shortest travel time closest to outbound packing dock
                rationale = "Fast-mover (Tier A) assigned to ergonomic golden zone (waist-to-shoulder) near staging dock.";
            }
            else if (velocity == VelocityTier.MediumB)
            {
                assignedZone = WarehouseRackZone.MezzanineBin;
                travelTime = 22.0;
                rationale = "Moderate velocity (Tier B) assigned to standard mezzanine pick shelves.";
            }
            else
            {
                assignedZone = WarehouseRackZone.TopLevel3;
                travelTime = 40.0;
                rationale = "Slow-mover (Tier C) relegated to high overhead racks to conserve prime golden zone capacity.";
            }

            var loc = $"A{aisleCounter:D2}-B{bayCounter:D2}-S{(int)assignedZone + 1}";
            bayCounter++;
            if (bayCounter > 10)
            {
                bayCounter = 1;
                aisleCounter++;
            }

            assignments.Add(new SlottingAssignment(
                Sku: profile.Sku,
                Velocity: velocity,
                AssignedZone: assignedZone,
                LocationAisleBayShelf: loc,
                EstimatedPickTravelTimeSeconds: travelTime,
                Rationale: rationale
            ));
        }

        return assignments;
    }
}
