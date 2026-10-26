using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface ICustomerReputationService
{
    Task<ReputationEvaluationResult> EvaluateCustomerReputationAsync(Guid tenantId, string customerPhone, string? email = null);
    Task<MicroDepositPaymentLink> GenerateMicroDepositLinkAsync(Guid tenantId, Guid orderId, string customerPhone, decimal depositAmount = 10.0m, string provider = "Flouci");
    Task<CodOrder> RecordMicroDepositSettlementAsync(Guid tenantId, Guid linkId);
}

public class CustomerReputationService : ICustomerReputationService
{
    private readonly SGrowingDbContext _db;

    public CustomerReputationService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<ReputationEvaluationResult> EvaluateCustomerReputationAsync(Guid tenantId, string customerPhone, string? email = null)
    {
        var cleanPhone = customerPhone.Trim();

        // 1. Gather all historical orders for this phone
        var orders = await _db.CodOrders
            .Where(o => o.TenantId == tenantId && (o.PhoneNumber == cleanPhone || (o.AlternatePhone != null && o.AlternatePhone == cleanPhone)))
            .ToListAsync();

        int totalOrders = orders.Count;
        int deliveredOrders = orders.Count(o => o.Status == CodOrderStatus.Delivered);
        int returnedOrders = orders.Count(o => o.Status == CodOrderStatus.Returned);
        int cancelledOrders = orders.Count(o => o.Status == CodOrderStatus.Cancelled);
        decimal totalSpent = orders.Where(o => o.Status == CodOrderStatus.Delivered).Sum(o => o.TotalAmount);

        decimal successRate = 100m;
        if (deliveredOrders + returnedOrders > 0)
        {
            successRate = Math.Round((decimal)deliveredOrders / (deliveredOrders + returnedOrders) * 100m, 1);
        }

        // 2. Score calculation (0 to 100)
        int score = 70; // baseline for new customer
        if (totalOrders > 0)
        {
            decimal rawScore = (successRate * 0.6m) + Math.Min(totalSpent / 20.0m, 20.0m) - (returnedOrders * 20.0m);
            score = Math.Clamp((int)Math.Round(rawScore), 5, 100);
        }

        // 3. Determine tier
        ReputationTier tier;
        bool requiresDeposit = false;
        string recommendedAction;

        if (score >= 80)
        {
            tier = ReputationTier.Trusted;
            recommendedAction = "Trusted Customer: Auto-approve order directly for packing.";
        }
        else if (score >= 50)
        {
            tier = ReputationTier.Standard;
            recommendedAction = "Standard Customer: Verify via Autonomous Voice AI or WhatsApp.";
        }
        else if (score >= 20)
        {
            tier = ReputationTier.HighRisk;
            requiresDeposit = true;
            recommendedAction = "High Return Risk: Require advance micro-deposit (10 TND) before dispatch.";
        }
        else
        {
            tier = ReputationTier.Blacklisted;
            requiresDeposit = true;
            recommendedAction = "Blacklisted: Serial delivery rejector. Reject COD or demand 100% advance payment.";
        }

        // 4. Update or create scorecard
        var scorecard = await _db.CustomerReputationScorecards
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.CustomerPhone == cleanPhone);

        if (scorecard == null)
        {
            scorecard = CustomerReputationScorecard.Create(tenantId, cleanPhone, email);
            _db.CustomerReputationScorecards.Add(scorecard);
        }

        scorecard.TotalOrdersCount = totalOrders;
        scorecard.DeliveredOrdersCount = deliveredOrders;
        scorecard.RefusedReturnsCount = returnedOrders;
        scorecard.CancelledOrdersCount = cancelledOrders;
        scorecard.TotalSpentAmount = totalSpent;
        scorecard.DeliverySuccessRatePercentage = successRate;
        scorecard.ReputationScore = score;
        scorecard.Tier = tier;
        scorecard.RequiresMicroDeposit = requiresDeposit;
        scorecard.LastEvaluatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new ReputationEvaluationResult(
            CustomerPhone: cleanPhone,
            ReputationScore: score,
            Tier: tier,
            DeliverySuccessRate: successRate,
            RefusedReturnsCount: returnedOrders,
            RequiresMicroDeposit: requiresDeposit,
            RecommendedAction: recommendedAction);
    }

    public async Task<MicroDepositPaymentLink> GenerateMicroDepositLinkAsync(
        Guid tenantId,
        Guid orderId,
        string customerPhone,
        decimal depositAmount = 10.0m,
        string provider = "Flouci")
    {
        var order = await _db.CodOrders.FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == orderId);
        if (order == null)
            throw new InvalidOperationException($"Order {orderId} not found.");

        var link = MicroDepositPaymentLink.Create(tenantId, orderId, customerPhone, depositAmount, order.CodCurrency, provider);
        _db.MicroDepositPaymentLinks.Add(link);

        order.Notes = (order.Notes ?? "") + $" [Micro-Deposit required: {depositAmount:F2} {order.CodCurrency} via {provider}. Link: {link.PaymentUrl}]";
        await _db.SaveChangesAsync();

        return link;
    }

    public async Task<CodOrder> RecordMicroDepositSettlementAsync(Guid tenantId, Guid linkId)
    {
        var link = await _db.MicroDepositPaymentLinks
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == linkId);

        if (link == null)
            throw new InvalidOperationException($"Deposit link {linkId} not found.");

        var order = await _db.CodOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == link.OrderId);

        if (order == null)
            throw new InvalidOperationException($"Order {link.OrderId} not found.");

        link.Status = MicroDepositStatus.Paid;
        link.PaidAtUtc = DateTime.UtcNow;

        // Auto-confirm order & deduct advance deposit from remaining COD
        order.Status = CodOrderStatus.Confirmed;
        order.ConfirmedAtUtc = DateTime.UtcNow;
        order.ConfirmationChannel = ConfirmationChannel.AutoVerified;
        order.TotalAmount = Math.Max(0, order.TotalAmount - link.DepositAmount);
        order.Notes = (order.Notes ?? "") + $" [Deposit of {link.DepositAmount:F2} {link.Currency} settled via {link.PaymentProvider}. Remaining COD: {order.TotalAmount:F2} {order.CodCurrency}]";

        // Promote Defensive WMS stock from Reserved to Committed
        var inventories = await _db.WarehouseInventories.Where(w => w.TenantId == tenantId).ToListAsync();
        foreach (var inv in inventories)
        {
            if (inv.ReservedStock > 0)
            {
                inv.ReservedStock = Math.Max(0, inv.ReservedStock - 1);
                inv.CommittedStock += 1;
                inv.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return order;
    }
}
