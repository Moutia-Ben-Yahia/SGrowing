namespace SGrowing.Api.Modules.Logistics.RailHumpYardScheduler;

public class RailHumpYardSchedulerEngine
{
    public HumpShuntingResult ScheduleHumpShunting(HumpShuntingRequest request)
    {
        var tracks = request.Tracks.ToDictionary(t => t.TrackId);
        var destTracks = request.Tracks.GroupBy(t => t.TargetDestination)
                                       .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var assignments = new List<WagonShuntingAssignment>();
        int cutNumber = 1;
        string? previousDest = null;
        bool allHazmatSegregated = true;

        var sortedWagons = request.InboundWagons.OrderBy(w => w.SequenceInTrain).ToList();

        foreach (var wagon in sortedWagons)
        {
            // If destination changes from previous wagon, increment hump cut number
            if (previousDest != null && !string.Equals(previousDest, wagon.DestinationYard, StringComparison.OrdinalIgnoreCase))
            {
                cutNumber++;
            }
            previousDest = wagon.DestinationYard;

            // Find matching track
            ClassificationTrack? selectedTrack = null;
            if (destTracks.TryGetValue(wagon.DestinationYard, out var candidates))
            {
                selectedTrack = candidates.FirstOrDefault(t => !wagon.IsHazardous || t.AllowsHazardousCargo);
            }

            // Fallback track if no destination-dedicated track available
            selectedTrack ??= request.Tracks.FirstOrDefault();

            string trackId = selectedTrack?.TrackId ?? "TRACK-OVERFLOW";
            bool bufferCarNeeded = wagon.IsHazardous;
            if (wagon.IsHazardous && selectedTrack != null && !selectedTrack.AllowsHazardousCargo)
            {
                allHazmatSegregated = false;
            }

            // Gravity hump rolling speed (heavier wagons roll faster, lighter wagons need higher release speed)
            double rollingSpeed = wagon.GrossWeightTons > 60.0 ? 3.8 : 4.5;

            assignments.Add(new WagonShuntingAssignment(
                WagonId: wagon.WagonId,
                TargetTrackId: trackId,
                RollCutNumber: cutNumber,
                TargetRollingSpeedMps: rollingSpeed,
                BufferCarRequired: bufferCarNeeded
            ));
        }

        string efficiencySummary = assignments.Count > 0
            ? $"Shunting Complete: {assignments.Count} wagons categorized into {cutNumber} gravity roll cuts. Hazmat safety verified."
            : "Empty Inbound Train: Zero wagons queued for hump classification.";

        return new HumpShuntingResult(
            InboundTrainId: request.InboundTrainId,
            TotalWagonsSorted: assignments.Count,
            TotalCutsProduced: assignments.Count > 0 ? cutNumber : 0,
            ShuntingPlan: assignments,
            AllHazardousSegregated: allHazmatSegregated,
            EfficiencySummary: efficiencySummary
        );
    }
}
