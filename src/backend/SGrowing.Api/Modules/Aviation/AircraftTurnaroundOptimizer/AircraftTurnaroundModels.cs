namespace SGrowing.Api.Modules.Aviation.AircraftTurnaroundOptimizer;

public record TurnaroundTask(
    string TaskId,
    string TaskType,
    double DurationMinutes,
    List<string> DependsOnTaskIds,
    bool IsCriticalPath = false);

public record FlightTurnaroundRequest(
    string FlightNumber,
    string AircraftType,
    string StandId,
    DateTimeOffset ActualInBlockTimeUtc,
    DateTimeOffset ScheduledDepartureUtc,
    List<TurnaroundTask> Tasks,
    double FuelRequiredLiters,
    int PassengerCount);

public record ScheduledRampActivity(
    string TaskId,
    string TaskType,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsOnCriticalPath);

public record FlightTurnaroundResult(
    string FlightNumber,
    DateTimeOffset TargetOffBlockTimeUtc,
    double TurnaroundDurationMinutes,
    bool WillMeetSlot,
    double SlotSlackMinutes,
    List<ScheduledRampActivity> Schedule,
    string BottleneckAnalysis);
