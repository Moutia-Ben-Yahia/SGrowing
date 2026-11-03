using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Commerce.Tariffs;

public record HsCodeClassification(
    string HsCode,
    string Description,
    string ChapterHeading,
    double ConfidencePercentage
);

public record LandedCostBreakdown(
    string ItemSku,
    string HsCode,
    string DestinationCountry,
    decimal ProductPrice,
    decimal FreightShippingCost,
    decimal InsuranceCost,
    decimal CustomsDutyRatePercentage,
    decimal CustomsDutyAmount,
    decimal DestinationVatRatePercentage,
    decimal DestinationVatAmount,
    decimal TotalLandedCost,
    string Incoterm // "DDP" (Delivered Duty Paid)
);

public record CalculateLandedCostRequest(
    string ItemSku,
    string ProductDescription,
    string DestinationCountry,
    decimal ProductPrice,
    decimal ShippingCost
);
