namespace SGrowing.Api.Modules.Revenue.YieldManagement;

/// <summary>
/// Type of perishable enterprise capacity managed by the yield engine.
/// </summary>
public enum CapacityType
{
    ExpressDeliverySlot = 1,
    PalletStorageBay = 2,
    RefrigeratedTransportUnit = 3,
    AirCargoVolumeM3 = 4
}

/// <summary>
/// Booking fare tier code.
/// </summary>
public enum FareClassTier
{
    Y_FullFarePremium = 1,
    M_StandardCommercial = 2,
    B_DiscountEconomy = 3,
    Q_LastMinuteSuperSaver = 4
}

/// <summary>
/// Status of a dynamic spot auction lot.
/// </summary>
public enum AuctionLotStatus
{
    Active = 1,
    Settled = 2,
    ExpiredFloorReached = 3,
    Canceled = 4
}

/// <summary>
/// Fare class demand forecast profile.
/// </summary>
public sealed record FareClassDemandProfile(
    FareClassTier Tier,
    decimal FareDzd,
    double ForecastedDemandMean,
    double ForecastedDemandStdDev
);

/// <summary>
/// Calculated capacity protection levels for nested fare classes.
/// </summary>
public sealed record CapacityProtectionLevel(
    FareClassTier Tier,
    int ProtectedCapacityUnits,
    decimal FareDzd,
    double CutoffProbability
);

/// <summary>
/// Capacity pool container for a scheduled resource departure/interval.
/// </summary>
public sealed record CapacityResourcePool(
    string PoolId,
    CapacityType Type,
    int TotalCapacityUnits,
    int BookedUnits,
    DateTime DepartureOrCutoffUtc,
    IReadOnlyList<FareClassDemandProfile> FareClasses
)
{
    public int AvailableCapacityUnits => Math.Max(0, TotalCapacityUnits - BookedUnits);
    public double UtilizationRatio => TotalCapacityUnits > 0 ? (double)BookedUnits / TotalCapacityUnits : 1.0;
}

/// <summary>
/// Outcome of booking request evaluation against protected capacity allocations.
/// </summary>
public sealed record BookingEvaluationResult(
    string PoolId,
    FareClassTier RequestedTier,
    int RequestedUnits,
    bool Accepted,
    string DecisionReason,
    decimal TotalQuoteDzd,
    int RemainingAvailableUnits
);

/// <summary>
/// Spot capacity auction lot for expiring capacity sold via Dutch descending auction.
/// </summary>
public sealed record SpotCapacityAuctionLot(
    string LotId,
    CapacityType CapacityType,
    int QuantityUnits,
    decimal StartingReservePriceDzd,
    decimal CurrentPriceDzd,
    decimal MinimumFloorPriceDzd,
    int DecayIntervalSeconds,
    decimal PriceDecrementDzd,
    DateTime AuctionStartedAtUtc,
    DateTime AuctionExpiresAtUtc,
    AuctionLotStatus Status,
    string? WinningBidderId = null,
    DateTime? SettledAtUtc = null
);

/// <summary>
/// Calculated price elasticity of demand metrics.
/// </summary>
public sealed record PriceElasticityReport(
    CapacityType CapacityType,
    decimal BasePriceDzd,
    decimal OptimalRevenuePriceDzd,
    double PointElasticity,
    double CrossPriceElasticityWithSubstitute,
    string ElasticityClassification // Elastic, Inelastic, Unitary
);

/// <summary>
/// Request to evaluate dynamic yield pricing for an incoming booking request.
/// </summary>
public sealed record EvaluateBookingRequest(
    string PoolId,
    FareClassTier RequestedTier,
    int RequestedUnits
);

/// <summary>
/// Request to initiate a high-velocity Dutch auction for perishable spot capacity.
/// </summary>
public sealed record CreateSpotAuctionRequest(
    CapacityType CapacityType,
    int QuantityUnits,
    decimal StartingPriceDzd,
    decimal MinimumFloorPriceDzd,
    decimal DecrementDzd,
    int DecayIntervalSeconds,
    int DurationMinutes
);

/// <summary>
/// Bid submission payload for an active Dutch capacity auction lot.
/// </summary>
public sealed record SubmitAuctionBidRequest(
    string LotId,
    string BidderId
);
