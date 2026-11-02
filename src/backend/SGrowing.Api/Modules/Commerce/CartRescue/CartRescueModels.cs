using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Commerce.CartRescue;

public enum IncentiveStrategy
{
    FreeExpeditedShipping,
    HighMarginFreeGiftBundle,
    TimeLockedCashbackEscrow,
    ZeroDiscountValueNudge
}

public record CartItem(
    string Sku,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal GrossMarginPercent
);

public record EvaluateCartRescueRequest(
    string CartId,
    string CustomerEmail,
    string? CustomerPhone,
    List<CartItem> Items,
    int MinutesSinceAbandonment
);

public record EvaluateCartRescueResult(
    string CartId,
    decimal TotalCartValue,
    decimal BlendedMarginPercent,
    double RecoveryProbabilityWithoutIncentive,
    IncentiveStrategy RecommendedIncentive,
    string IncentiveDescription,
    decimal ExpectedGrossProfitLift,
    string PreferredChannel, // WhatsApp, SMS, Email
    DateTime EvaluatedAtUtc
);

public record CartRescueRecord(
    string RescueId,
    string CartId,
    string CustomerEmail,
    decimal TotalValue,
    string CurrentStatus, // Staged, DispatchedWhatsApp, Recovered, Expired
    IncentiveStrategy AppliedIncentive,
    DateTime CreatedAtUtc
);

public record DispatchCartNudgeRequest(
    string RescueId,
    string Channel = "WhatsApp"
);

public record DispatchCartNudgeResult(
    bool Dispatched,
    string RescueId,
    string OutboundMessageText,
    DateTime DispatchedAtUtc
);
