namespace SGrowing.Api.Modules.Aviation.AircraftTurnaroundOptimizer;

public class AircraftTurnaroundOptimizerEngine
{
    public FlightTurnaroundResult OptimizeTurnaround(FlightTurnaroundRequest request)
    {
        var taskMap = request.Tasks.ToDictionary(t => t.TaskId);
        var endTimes = new Dictionary<string, DateTimeOffset>();
        var scheduledActivities = new List<ScheduledRampActivity>();

        // Topological scheduling
        var remaining = new List<TurnaroundTask>(request.Tasks);
        while (remaining.Count > 0)
        {
            var readyTask = remaining.FirstOrDefault(t => t.DependsOnTaskIds.All(dep => endTimes.ContainsKey(dep)));
            if (readyTask == null)
            {
                // Circular dependency or orphan; break to avoid infinite loop
                break;
            }

            remaining.Remove(readyTask);

            DateTimeOffset startTime = request.ActualInBlockTimeUtc;
            if (readyTask.DependsOnTaskIds.Count > 0)
            {
                startTime = readyTask.DependsOnTaskIds.Max(dep => endTimes[dep]);
            }

            // Adjust refueling duration if applicable (hydrant flow ~1200 L/min)
            double duration = readyTask.DurationMinutes;
            if (string.Equals(readyTask.TaskType, "Refueling", StringComparison.OrdinalIgnoreCase) && request.FuelRequiredLiters > 0)
            {
                duration = Math.Max(duration, Math.Round(request.FuelRequiredLiters / 1200.0, 1));
            }

            DateTimeOffset endTime = startTime.AddMinutes(duration);
            endTimes[readyTask.TaskId] = endTime;

            scheduledActivities.Add(new ScheduledRampActivity(
                TaskId: readyTask.TaskId,
                TaskType: readyTask.TaskType,
                StartUtc: startTime,
                EndUtc: endTime,
                IsOnCriticalPath: readyTask.IsCriticalPath
            ));
        }

        DateTimeOffset tobt = endTimes.Count > 0 ? endTimes.Values.Max() : request.ActualInBlockTimeUtc;
        double totalTurnaroundMinutes = Math.Round((tobt - request.ActualInBlockTimeUtc).TotalMinutes, 1);
        double slotSlack = Math.Round((request.ScheduledDepartureUtc - tobt).TotalMinutes, 1);
        bool willMeetSlot = slotSlack >= 0;

        string bottleneckAnalysis;
        if (willMeetSlot)
        {
            bottleneckAnalysis = $"On-Time: Turnaround completes {slotSlack:F1} min before scheduled departure slot.";
        }
        else
        {
            var longestTask = scheduledActivities.OrderByDescending(a => (a.EndUtc - a.StartUtc).TotalMinutes).FirstOrDefault();
            bottleneckAnalysis = $"Slot Delay Risk: TOBT breaches departure by {Math.Abs(slotSlack):F1} min. Primary pacing constraint: {longestTask?.TaskType ?? "Ramp congestion"}.";
        }

        return new FlightTurnaroundResult(
            FlightNumber: request.FlightNumber,
            TargetOffBlockTimeUtc: tobt,
            TurnaroundDurationMinutes: totalTurnaroundMinutes,
            WillMeetSlot: willMeetSlot,
            SlotSlackMinutes: slotSlack,
            Schedule: scheduledActivities,
            BottleneckAnalysis: bottleneckAnalysis
        );
    }
}
