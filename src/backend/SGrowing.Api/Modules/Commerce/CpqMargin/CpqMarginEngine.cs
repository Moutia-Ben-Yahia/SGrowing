using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Commerce.CpqMargin;

public interface ICpqMarginEngine
{
    QuotePricingResult CalculateQuotePricing(QuotePricingRequest request);
    QuotePricingResult? GetQuoteById(string quoteId);
}

public class CpqMarginEngine : ICpqMarginEngine
{
    private const double MinimumGrossMarginHurdle = 75.0; // 75% gross margin floor
    private readonly ConcurrentDictionary<string, QuotePricingResult> _quotes = new(StringComparer.OrdinalIgnoreCase);

    public CpqMarginEngine()
    {
        // Seed default corporate quote
        CalculateQuotePricing(new QuotePricingRequest(
            "quote_ent_901",
            "AeroDynamics Global",
            3,
            120000m,
            12.5m,
            18000m,
            5000m
        ));
    }

    public QuotePricingResult CalculateQuotePricing(QuotePricingRequest request)
    {
        var ramps = new List<ContractYearRamp>();
        decimal tcv = 0m;
        decimal totalProfit = 0m;

        for (int year = 1; year <= Math.Max(1, request.ContractTermYears); year++)
        {
            // Year 1 receives requested discount, years 2+ ramp up with +3% inflation indexing
            decimal yearDiscount = year == 1 ? request.RequestedDiscountPercentage : Math.Max(0m, request.RequestedDiscountPercentage - ((year - 1) * 3.0m));
            decimal basePriceIndexed = request.AnnualListPrice * (decimal)Math.Pow(1.03, year - 1);
            decimal netAcv = basePriceIndexed * (1m - (yearDiscount / 100m));
            decimal cogs = request.InfrastructureCogsYearly + request.SupportTierFeeYearly;
            decimal profit = netAcv - cogs;
            double margin = netAcv > 0 ? (double)Math.Round((profit / netAcv) * 100m, 2) : 0.0;

            ramps.Add(new ContractYearRamp(
                year,
                Math.Round(basePriceIndexed, 2),
                Math.Round(yearDiscount, 2),
                Math.Round(netAcv, 2),
                Math.Round(cogs, 2),
                Math.Round(profit, 2),
                margin
            ));

            tcv += netAcv;
            totalProfit += profit;
        }

        double blendedMargin = tcv > 0 ? (double)Math.Round((totalProfit / tcv) * 100m, 2) : 0.0;

        DiscountApprovalTier tier;
        string recommendation;

        if (blendedMargin < MinimumGrossMarginHurdle)
        {
            tier = DiscountApprovalTier.RejectedMarginBreach;
            recommendation = $"Rejected: Blended margin ({blendedMargin}%) is below the non-negotiable {MinimumGrossMarginHurdle}% hurdle rate.";
        }
        else if (request.RequestedDiscountPercentage > 25.0m)
        {
            tier = DiscountApprovalTier.CfoBoardApproved;
            recommendation = "Approved with CFO/Board Authorization: Heavy initial concession justified by multi-year commitment.";
        }
        else if (request.RequestedDiscountPercentage > 15.0m)
        {
            tier = DiscountApprovalTier.VpSalesApproved;
            recommendation = "Approved with VP Sales Sign-off: Within regional sales escalation delegation.";
        }
        else
        {
            tier = DiscountApprovalTier.StandardRepApproved;
            recommendation = "Auto-Approved: Within standard representative discretionary discounting limit.";
        }

        // Cryptographic Audit Hash for verifiable contract compliance
        var rawString = $"{request.QuoteId}:{request.CustomerName}:{tcv}:{blendedMargin}:{tier}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawString));
        var auditHash = Convert.ToHexString(hashBytes)[..16];

        var result = new QuotePricingResult(
            request.QuoteId,
            request.CustomerName,
            request.ContractTermYears,
            Math.Round(tcv, 2),
            Math.Round(totalProfit, 2),
            blendedMargin,
            tier,
            auditHash,
            ramps,
            recommendation
        );

        _quotes[request.QuoteId] = result;
        return result;
    }

    public QuotePricingResult? GetQuoteById(string quoteId)
    {
        _quotes.TryGetValue(quoteId, out var result);
        return result;
    }
}
