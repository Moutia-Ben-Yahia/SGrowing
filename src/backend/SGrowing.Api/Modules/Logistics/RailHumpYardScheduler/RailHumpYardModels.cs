namespace SGrowing.Api.Modules.Logistics.RailHumpYardScheduler;

public record FreightWagon(
    string WagonId,
    string CargoType,
    double GrossWeightTons,
    string DestinationYard,
    bool IsHazardous,
    int SequenceInTrain);

public record ClassificationTrack(
    string TrackId,
    string TargetDestination,
    double CapacityMeters,
    double CurrentOccupancyMeters,
    bool AllowsHazardousCargo);

public record HumpShuntingRequest(
    string InboundTrainId,
    List<FreightWagon> InboundWagons,
    List<ClassificationTrack> Tracks,
    double AverageWagonLengthMeters = 14.5);

public record WagonShuntingAssignment(
    string WagonId,
    string TargetTrackId,
    int RollCutNumber,
    double TargetRollingSpeedMps,
    bool BufferCarRequired);

public record HumpShuntingResult(
    string InboundTrainId,
    int TotalWagonsSorted,
    int TotalCutsProduced,
    List<WagonShuntingAssignment> ShuntingPlan,
    bool AllHazardousSegregated,
    string EfficiencySummary);
