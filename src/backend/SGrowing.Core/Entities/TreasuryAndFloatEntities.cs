namespace SGrowing.Core.Entities;

public enum CarrierRemittanceFrequency
{
    Weekly = 1,
    BiWeekly = 2,
    Monthly = 3,
    OnDemand = 4
}

public class CarrierRemittanceScheduleConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CourierProviderId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public CarrierRemittanceFrequency Frequency { get; set; } = CarrierRemittanceFrequency.Weekly;
    public DayOfWeek RemittanceDayOfWeek { get; set; } = DayOfWeek.Tuesday;
    public int AverageLagDays { get; set; } = 7;
    public decimal ContractFeePerParcel { get; set; } = 7.0m;
    public bool IsActive { get; set; } = true;
}

public class CourierFloatValuation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public int InTransitParcelCount { get; set; }
    public decimal InTransitCashValue { get; set; }
    public int DeliveredUnremittedParcelCount { get; set; }
    public decimal DeliveredUnremittedCashValue { get; set; }
    public decimal EstimatedCourierFees { get; set; }
    public decimal NetReceivableFloatValue => Math.Max(0, (InTransitCashValue + DeliveredUnremittedCashValue) - EstimatedCourierFees);
    public decimal AverageDaysSalesOutstanding { get; set; } = 8.5m;
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record CashFlowForecastDay(
    DateTime ForecastDate,
    string CourierName,
    int ExpectedParcelCount,
    decimal ExpectedGrossCash,
    decimal ExpectedNetRemittance,
    string Confidence);
