using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Sustainability.CarbonExchange;

/// <summary>
/// High-integrity order book exchange matching buyers and sellers of verified
/// Renewable Energy Credits (RECs) and carbon offsets, with tamper-proof certificate retirement.
/// </summary>
public sealed class RecTokenizationAndMatchingService
{
    private readonly ConcurrentDictionary<string, RecCertificate> _certificates = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RecTradeOrder> _buyOrders = [];
    private readonly List<RecTradeOrder> _sellOrders = [];
    private readonly List<MatchedTrade> _executedTrades = [];
    private readonly List<RetirementCertificateReceipt> _retirements = [];
    private readonly System.Threading.Lock _bookLock = new();

    public RecCertificate MintCertificate(
        CreditCertificateStandard standard,
        string originFacility,
        string generationType,
        double megawattHours,
        double co2eOffsetTonnes,
        string issuerCountry,
        DateTime vintageYear)
    {
        var certId = $"REC-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var rawProof = $"{certId}|{standard}|{originFacility}|{generationType}|{megawattHours}|{co2eOffsetTonnes}|{vintageYear:yyyy}";
        var proofHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawProof))).ToLowerInvariant();

        var cert = new RecCertificate(
            CertificateId: certId,
            Standard: standard,
            OriginFacility: originFacility,
            GenerationType: generationType,
            MegawattHours: megawattHours,
            Co2eOffsetTonnes: co2eOffsetTonnes,
            IssuerCountry: issuerCountry,
            VintageYear: vintageYear,
            ProofHashHex: proofHash,
            IsRetired: false
        );

        _certificates[certId] = cert;
        return cert;
    }

    public (RecTradeOrder Order, IReadOnlyList<MatchedTrade> Matches) PlaceOrder(
        string tenantId,
        OrderBookSide side,
        double quantityTonnes,
        decimal limitPriceEur,
        CreditCertificateStandard standard)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (quantityTonnes <= 0) throw new ArgumentOutOfRangeException(nameof(quantityTonnes), "Quantity must be > 0.");
        if (limitPriceEur <= 0) throw new ArgumentOutOfRangeException(nameof(limitPriceEur), "Price must be > 0.");

        var order = new RecTradeOrder(
            OrderId: $"ORD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            TenantId: tenantId,
            Side: side,
            QuantityTonnes: quantityTonnes,
            LimitPriceEurPerTonne: limitPriceEur,
            Standard: standard,
            CreatedAtUtc: DateTime.UtcNow
        );

        var matches = new List<MatchedTrade>();

        lock (_bookLock)
        {
            if (side == OrderBookSide.Buy)
            {
                // Match against best sell orders (lowest price <= buy limit)
                var availableSells = _sellOrders
                    .Where(s => s.Standard == standard && s.Status != TradeStatus.Filled && s.LimitPriceEurPerTonne <= limitPriceEur && !string.Equals(s.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.LimitPriceEurPerTonne)
                    .ThenBy(s => s.CreatedAtUtc)
                    .ToList();

                var remaining = quantityTonnes;
                foreach (var sell in availableSells)
                {
                    if (remaining <= 0) break;

                    var sellAvailable = sell.QuantityTonnes - sell.FilledQuantityTonnes;
                    var matchQty = Math.Min(remaining, sellAvailable);
                    var execPrice = sell.LimitPriceEurPerTonne; // Price-time priority: maker price

                    var trade = new MatchedTrade(
                        TradeId: $"TRD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                        BuyOrderId: order.OrderId,
                        SellOrderId: sell.OrderId,
                        BuyerTenantId: tenantId,
                        SellerTenantId: sell.TenantId,
                        MatchedTonnes: matchQty,
                        ExecutionPriceEurPerTonne: execPrice,
                        Standard: standard,
                        ExecutedAtUtc: DateTime.UtcNow
                    );

                    matches.Add(trade);
                    _executedTrades.Add(trade);

                    remaining -= matchQty;
                    var updatedSellFilled = sell.FilledQuantityTonnes + matchQty;
                    var sellStatus = updatedSellFilled >= sell.QuantityTonnes ? TradeStatus.Filled : TradeStatus.PartiallyFilled;

                    _sellOrders[_sellOrders.IndexOf(sell)] = sell with
                    {
                        FilledQuantityTonnes = updatedSellFilled,
                        Status = sellStatus
                    };
                }

                var filledQty = quantityTonnes - remaining;
                var finalStatus = remaining <= 0 ? TradeStatus.Filled : (filledQty > 0 ? TradeStatus.PartiallyFilled : TradeStatus.Open);
                order = order with { FilledQuantityTonnes = filledQty, Status = finalStatus };

                if (finalStatus != TradeStatus.Filled)
                {
                    _buyOrders.Add(order);
                }
            }
            else // Sell order
            {
                // Match against best buy orders (highest price >= sell limit)
                var availableBuys = _buyOrders
                    .Where(b => b.Standard == standard && b.Status != TradeStatus.Filled && b.LimitPriceEurPerTonne >= limitPriceEur && !string.Equals(b.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(b => b.LimitPriceEurPerTonne)
                    .ThenBy(b => b.CreatedAtUtc)
                    .ToList();

                var remaining = quantityTonnes;
                foreach (var buy in availableBuys)
                {
                    if (remaining <= 0) break;

                    var buyAvailable = buy.QuantityTonnes - buy.FilledQuantityTonnes;
                    var matchQty = Math.Min(remaining, buyAvailable);
                    var execPrice = buy.LimitPriceEurPerTonne;

                    var trade = new MatchedTrade(
                        TradeId: $"TRD-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                        BuyOrderId: buy.OrderId,
                        SellOrderId: order.OrderId,
                        BuyerTenantId: buy.TenantId,
                        SellerTenantId: tenantId,
                        MatchedTonnes: matchQty,
                        ExecutionPriceEurPerTonne: execPrice,
                        Standard: standard,
                        ExecutedAtUtc: DateTime.UtcNow
                    );

                    matches.Add(trade);
                    _executedTrades.Add(trade);

                    remaining -= matchQty;
                    var updatedBuyFilled = buy.FilledQuantityTonnes + matchQty;
                    var buyStatus = updatedBuyFilled >= buy.QuantityTonnes ? TradeStatus.Filled : TradeStatus.PartiallyFilled;

                    _buyOrders[_buyOrders.IndexOf(buy)] = buy with
                    {
                        FilledQuantityTonnes = updatedBuyFilled,
                        Status = buyStatus
                    };
                }

                var filledQty = quantityTonnes - remaining;
                var finalStatus = remaining <= 0 ? TradeStatus.Filled : (filledQty > 0 ? TradeStatus.PartiallyFilled : TradeStatus.Open);
                order = order with { FilledQuantityTonnes = filledQty, Status = finalStatus };

                if (finalStatus != TradeStatus.Filled)
                {
                    _sellOrders.Add(order);
                }
            }
        }

        return (order, matches);
    }

    public RetirementCertificateReceipt RetireCertificate(string certificateId, string tenantId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (!_certificates.TryGetValue(certificateId, out var cert))
        {
            throw new KeyNotFoundException($"Certificate '{certificateId}' does not exist.");
        }

        if (cert.IsRetired)
        {
            throw new InvalidOperationException($"Certificate '{certificateId}' has already been retired on {cert.RetiredAtUtc:u} by tenant '{cert.RetiredByTenantId}'.");
        }

        var retiredCert = cert with
        {
            IsRetired = true,
            RetiredByTenantId = tenantId,
            RetiredAtUtc = DateTime.UtcNow
        };
        _certificates[certificateId] = retiredCert;

        var receiptId = $"RET-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var rawProof = $"{receiptId}|{certificateId}|{tenantId}|{cert.Co2eOffsetTonnes}|{retiredCert.RetiredAtUtc:O}|RETIRED_PERMANENTLY";
        var auditProof = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawProof))).ToLowerInvariant();

        var receipt = new RetirementCertificateReceipt(
            ReceiptId: receiptId,
            CertificateId: certificateId,
            TenantId: tenantId,
            RetiredTonnes: cert.Co2eOffsetTonnes,
            Reason: reason,
            ImmutableAuditProofHex: auditProof,
            RetiredAtUtc: retiredCert.RetiredAtUtc.Value
        );

        _retirements.Add(receipt);
        return receipt;
    }

    public IReadOnlyList<RecCertificate> GetActiveCertificates() =>
        _certificates.Values.Where(c => !c.IsRetired).ToList();

    public IReadOnlyList<MatchedTrade> GetExecutedTrades()
    {
        lock (_bookLock)
        {
            return [.. _executedTrades];
        }
    }
}
