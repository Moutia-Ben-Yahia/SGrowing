using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.Asc606Revenue;

public enum ContractObligationType
{
    SoftwareSubscriptionLicense,
    ImplementationOnboarding,
    ContinuousSlaSupport
}

public record MonthlyAmortizationTranche(
    int MonthIndex,
    string MonthLabel,
    decimal RecognizedAmountUsd,
    decimal RemainingDeferredBalanceUsd,
    bool IsRecognized
);

public record RevenueContractSchedule(
    string ScheduleId,
    string CustomerId,
    string CustomerName,
    decimal TotalContractValueUsd,
    decimal CumulativeRecognizedUsd,
    decimal DeferredRevenueLiabilityUsd,
    int ContractDurationMonths,
    List<MonthlyAmortizationTranche> Tranches
);

public record RecognizeNextMonthRequest(
    string ScheduleId
);
