using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Finance.SecondaryLiquidity;

public interface ISecondaryLiquidityEngine
{
    SecondaryLiquidityOverview GetOverview();
    TenderOfferWindow CreateTenderOffer(string programName, double maxVolumeUsd, double sharePriceUsd, int durationDays);
    SecondaryShareLot SubmitShareLot(string offerId, string shareholderId, string shareholderName, int sharesCount);
    RofrClearanceToken ProcessRofrClearance(string lotId, bool companyExercises);
    DilutionImpactSimulation SimulateDilution(double newSharesIssued, double totalPreShares);
}

public sealed class SecondaryLiquidityEngine : ISecondaryLiquidityEngine
{
    private readonly ConcurrentDictionary<string, TenderOfferWindow> _offers = new();
    private readonly ConcurrentDictionary<string, SecondaryShareLot> _lots = new();
    private readonly ConcurrentDictionary<string, RofrClearanceToken> _tokens = new();

    public SecondaryLiquidityEngine()
    {
        SeedOffers();
        SeedInitialLots();
    }

    private void SeedOffers()
    {
        var offer1 = new TenderOfferWindow(
            "TENDER-2026-Q4",
            "Series D Employee Liquidity Program",
            25000000,
            42.50,
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow.AddDays(14),
            TenderOfferStatus.ActiveOpenWindow,
            CurrentCommittedVolumeUsd: 18250000
        );
        _offers.TryAdd(offer1.OfferId, offer1);
    }

    private void SeedInitialLots()
    {
        var lot1 = new SecondaryShareLot(
            "LOT-SEC-001",
            "TENDER-2026-Q4",
            "SH-EXEC-104",
            "Julian Vance (VP Engineering)",
            20000,
            850000,
            RofrClearedByCompany: true,
            EstimatedTaxWithholdingUsd: 314500,
            Status: "ROFR_CLEARED"
        );
        _lots.TryAdd(lot1.LotId, lot1);
    }

    public SecondaryLiquidityOverview GetOverview()
    {
        var offers = _offers.Values.ToList();
        var lots = _lots.Values.ToList();

        return new SecondaryLiquidityOverview(
            ActiveTenderOffersCount: offers.Count,
            TotalSecondaryVolumeSettledUsd: 48200000,
            TotalParticipatingHoldersCount: 146,
            AverageSharePriceUsd: 42.50,
            ActiveOffers: offers,
            RecentLots: lots
        );
    }

    public TenderOfferWindow CreateTenderOffer(string programName, double maxVolumeUsd, double sharePriceUsd, int durationDays)
    {
        var offer = new TenderOfferWindow(
            $"TENDER-{DateTime.UtcNow.Year}-Q{((DateTime.UtcNow.Month - 1) / 3) + 1}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            programName,
            maxVolumeUsd,
            sharePriceUsd,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(durationDays),
            TenderOfferStatus.ActiveOpenWindow,
            CurrentCommittedVolumeUsd: 0.0
        );
        _offers.TryAdd(offer.OfferId, offer);
        return offer;
    }

    public SecondaryShareLot SubmitShareLot(string offerId, string shareholderId, string shareholderName, int sharesCount)
    {
        double price = 42.50;
        if (_offers.TryGetValue(offerId, out var offer))
        {
            price = offer.SharePriceUsd;
        }

        double aggregate = sharesCount * price;
        double tax = aggregate * 0.37; // 37% estimated top marginal ordinary / AMT withholding

        var lot = new SecondaryShareLot(
            $"LOT-SEC-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            offerId,
            shareholderId,
            shareholderName,
            sharesCount,
            aggregate,
            RofrClearedByCompany: false,
            EstimatedTaxWithholdingUsd: tax,
            Status: "PENDING_ROFR"
        );
        _lots.TryAdd(lot.LotId, lot);

        if (offer != null)
        {
            _offers[offerId] = offer with
            {
                CurrentCommittedVolumeUsd = offer.CurrentCommittedVolumeUsd + aggregate
            };
        }

        return lot;
    }

    public RofrClearanceToken ProcessRofrClearance(string lotId, bool companyExercises)
    {
        string purchaser = companyExercises ? "Company Treasury Escrow (Share Buyback)" : "Sequoia Capital Global Growth Fund VII";
        var rawSig = $"{lotId}:{companyExercises}:{purchaser}:{DateTime.UtcNow.Ticks}";
        var sig = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSig))).ToLowerInvariant();

        string shareholderId = "UNKNOWN";
        if (_lots.TryGetValue(lotId, out var existing))
        {
            shareholderId = existing.ShareholderId;
            _lots[lotId] = existing with
            {
                RofrClearedByCompany = true,
                Status = companyExercises ? "COMPANY_REPURCHASED" : "CLEARED_TO_SECONDARY_INVESTOR"
            };
        }

        var token = new RofrClearanceToken(
            $"ROFR-TOK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            lotId,
            shareholderId,
            companyExercises,
            purchaser,
            DateTime.UtcNow,
            sig
        );
        _tokens.TryAdd(token.TokenId, token);
        return token;
    }

    public DilutionImpactSimulation SimulateDilution(double newSharesIssued, double totalPreShares)
    {
        double postShares = totalPreShares + newSharesIssued;
        double founderPre = 34.0;
        double founderPost = Math.Round((founderPre * totalPreShares) / postShares, 2);
        double dilutionPct = Math.Round(((postShares - totalPreShares) / postShares) * 100.0, 2);

        return new DilutionImpactSimulation(
            $"DIL-SIM-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            PreTransactionFullyDilutedShares: totalPreShares,
            PostTransactionFullyDilutedShares: postShares,
            FounderOwnershipPercentageBefore: founderPre,
            FounderOwnershipPercentageAfter: founderPost,
            EffectiveDilutionPercentage: dilutionPct,
            OptionPoolUnallocatedPercentage: 11.5
        );
    }
}
