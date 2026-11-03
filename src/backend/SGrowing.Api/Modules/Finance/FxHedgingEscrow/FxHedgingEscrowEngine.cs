using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Finance.FxHedgingEscrow;

public interface IFxHedgingEscrowEngine
{
    FxHedgingPortfolio GetPortfolio();
    FxForwardContract LockForward(LockForwardRequest request);
    SettlementOutcome SettleContract(SettleEscrowRequest request);
}

public class FxHedgingEscrowEngine : IFxHedgingEscrowEngine
{
    private readonly ConcurrentDictionary<string, FxForwardContract> _contracts = new(StringComparer.OrdinalIgnoreCase);

    public FxHedgingEscrowEngine()
    {
        // Seed active forward contracts
        var c1 = new FxForwardContract(
            ContractId: "fwd_eur_701",
            Corridor: CurrencyCorridor.EurUsd,
            LockedExchangeRate: 1.0850m,
            NotionalAmountSource: 250000m, // EUR
            GuaranteedAmountTarget: 271250m, // USD
            ExpiryDateUtc: DateTime.UtcNow.AddDays(45),
            IsSettled: false
        );

        var c2 = new FxForwardContract(
            ContractId: "fwd_gbp_702",
            Corridor: CurrencyCorridor.GbpUsd,
            LockedExchangeRate: 1.2950m,
            NotionalAmountSource: 180000m, // GBP
            GuaranteedAmountTarget: 233100m, // USD
            ExpiryDateUtc: DateTime.UtcNow.AddDays(60),
            IsSettled: false
        );

        _contracts[c1.ContractId] = c1;
        _contracts[c2.ContractId] = c2;
    }

    public FxHedgingPortfolio GetPortfolio()
    {
        var list = _contracts.Values.OrderBy(c => c.ContractId).ToList();
        var totalNotional = list.Where(c => !c.IsSettled).Sum(c => c.GuaranteedAmountTarget);

        return new FxHedgingPortfolio(
            ActiveContracts: list,
            TotalHedgedNotionalUsd: totalNotional,
            TotalSavingsFromHedgesUsd: 14250m,
            ValueAtRiskPercentage: 1.8
        );
    }

    public FxForwardContract LockForward(LockForwardRequest request)
    {
        decimal rate = request.Corridor switch
        {
            CurrencyCorridor.EurUsd => 1.0875m,
            CurrencyCorridor.GbpUsd => 1.2980m,
            CurrencyCorridor.JpyUsd => 0.0067m,
            CurrencyCorridor.SgdUsd => 0.7650m,
            _ => 1.0000m
        };

        var contractId = $"fwd_{request.Corridor.ToString().ToLowerInvariant()}_{DateTime.UtcNow.Ticks % 10000}";
        var guaranteed = Math.Round(request.NotionalAmountSource * rate, 2);

        var contract = new FxForwardContract(
            ContractId: contractId,
            Corridor: request.Corridor,
            LockedExchangeRate: rate,
            NotionalAmountSource: request.NotionalAmountSource,
            GuaranteedAmountTarget: guaranteed,
            ExpiryDateUtc: DateTime.UtcNow.AddDays(request.DurationDays),
            IsSettled: false
        );

        _contracts[contractId] = contract;
        return contract;
    }

    public SettlementOutcome SettleContract(SettleEscrowRequest request)
    {
        if (!_contracts.TryGetValue(request.ContractId, out var contract))
        {
            return new SettlementOutcome(false, request.ContractId, 0m, "Contract not found.");
        }

        decimal unhedgedPayout = Math.Round(contract.NotionalAmountSource * request.SpotRateAtSettlement, 2);
        decimal variance = contract.GuaranteedAmountTarget - unhedgedPayout;

        var settled = contract with { IsSettled = true };
        _contracts[request.ContractId] = settled;

        string direction = variance >= 0 ? "protected from adverse depreciation" : "settled with positive market upside";
        return new SettlementOutcome(
            Success: true,
            ContractId: request.ContractId,
            VarianceCapturedUsd: Math.Abs(variance),
            Message: $"Contract {request.ContractId} settled. Escrow successfully {direction} (${Math.Abs(variance):N2} USD variance)."
        );
    }
}
