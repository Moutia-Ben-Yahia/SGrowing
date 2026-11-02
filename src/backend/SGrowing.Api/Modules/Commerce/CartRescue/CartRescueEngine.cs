using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Commerce.CartRescue;

public interface ICartRescueEngine
{
    EvaluateCartRescueResult EvaluateCart(EvaluateCartRescueRequest request);
    IReadOnlyList<CartRescueRecord> GetActiveRescues();
    DispatchCartNudgeResult DispatchNudge(DispatchCartNudgeRequest request);
}

public class CartRescueEngine : ICartRescueEngine
{
    private readonly ConcurrentDictionary<string, CartRescueRecord> _rescues = new();

    public CartRescueEngine()
    {
        var r1 = new CartRescueRecord(
            RescueId: "rescue_cart_8801",
            CartId: "cart_nordic_apparel_99",
            CustomerEmail: "elena@nordicstyle.se",
            TotalValue: 340.00m,
            CurrentStatus: "DispatchedWhatsApp",
            AppliedIncentive: IncentiveStrategy.FreeExpeditedShipping,
            CreatedAtUtc: DateTime.UtcNow.AddMinutes(-45)
        );

        var r2 = new CartRescueRecord(
            RescueId: "rescue_cart_8802",
            CartId: "cart_tech_gadgets_44",
            CustomerEmail: "marcus@berlintech.de",
            TotalValue: 890.00m,
            CurrentStatus: "Recovered",
            AppliedIncentive: IncentiveStrategy.HighMarginFreeGiftBundle,
            CreatedAtUtc: DateTime.UtcNow.AddHours(-2)
        );

        _rescues[r1.RescueId] = r1;
        _rescues[r2.RescueId] = r2;
    }

    public EvaluateCartRescueResult EvaluateCart(EvaluateCartRescueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var items = request.Items ?? new List<CartItem>();
        var totalValue = items.Sum(i => i.UnitPrice * i.Quantity);
        var blendedMargin = items.Any()
            ? items.Average(i => i.GrossMarginPercent)
            : 55.0m;

        IncentiveStrategy strategy;
        string description;
        decimal profitLift;

        if (totalValue >= 500m && blendedMargin >= 50m)
        {
            strategy = IncentiveStrategy.HighMarginFreeGiftBundle;
            description = "Complimentary Premium Carry Case ($45 Value, Cost $4.50) + Next-Day Delivery";
            profitLift = totalValue * 0.42m;
        }
        else if (totalValue >= 200m)
        {
            strategy = IncentiveStrategy.FreeExpeditedShipping;
            description = "Free Guaranteed Priority Courier Delivery (Save $18)";
            profitLift = totalValue * 0.35m;
        }
        else if (request.MinutesSinceAbandonment < 30)
        {
            strategy = IncentiveStrategy.ZeroDiscountValueNudge;
            description = "Inventory Low-Stock Reservation Lock (Zero Margin Concession)";
            profitLift = totalValue * 0.28m;
        }
        else
        {
            strategy = IncentiveStrategy.TimeLockedCashbackEscrow;
            description = "5% Instant Wallet Credit on Completion (Valid 4 Hours)";
            profitLift = totalValue * 0.22m;
        }

        var channel = !string.IsNullOrWhiteSpace(request.CustomerPhone) ? "WhatsApp" : "Email";

        var result = new EvaluateCartRescueResult(
            CartId: request.CartId,
            TotalCartValue: totalValue,
            BlendedMarginPercent: blendedMargin,
            RecoveryProbabilityWithoutIncentive: 0.24,
            RecommendedIncentive: strategy,
            IncentiveDescription: description,
            ExpectedGrossProfitLift: Math.Round(profitLift, 2),
            PreferredChannel: channel,
            EvaluatedAtUtc: DateTime.UtcNow
        );

        var rescueId = $"rescue_{Guid.NewGuid():N}"[..12];
        _rescues[rescueId] = new CartRescueRecord(
            RescueId: rescueId,
            CartId: request.CartId,
            CustomerEmail: request.CustomerEmail,
            TotalValue: totalValue,
            CurrentStatus: "Staged",
            AppliedIncentive: strategy,
            CreatedAtUtc: DateTime.UtcNow
        );

        return result;
    }

    public IReadOnlyList<CartRescueRecord> GetActiveRescues()
    {
        return _rescues.Values.OrderByDescending(r => r.CreatedAtUtc).ToList();
    }

    public DispatchCartNudgeResult DispatchNudge(DispatchCartNudgeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_rescues.TryGetValue(request.RescueId, out var existing))
        {
            var updated = existing with { CurrentStatus = $"Dispatched{request.Channel}" };
            _rescues[request.RescueId] = updated;

            var msg = $"Hi from your store! We saved your cart (${existing.TotalValue:N2}). Complete in the next 2 hours and unlock: {existing.AppliedIncentive}. Click to resume: https://app.sgrowing.io/cart/{existing.CartId}";

            return new DispatchCartNudgeResult(
                Dispatched: true,
                RescueId: request.RescueId,
                OutboundMessageText: msg,
                DispatchedAtUtc: DateTime.UtcNow
            );
        }

        throw new KeyNotFoundException($"Cart rescue record '{request.RescueId}' not found.");
    }
}
