namespace SGrowing.Api.Modules.Infrastructure.DePinWorkerArbiter;

public class DePinWorkerArbiterEngine
{
    public DePinArbitrationResult ArbitrateBatch(DePinArbitrationRequest request)
    {
        var workersMap = request.RegisteredNodes.ToDictionary(w => w.WorkerId);
        var tasksMap = request.Tasks.ToDictionary(t => t.TaskId);

        var settlements = new List<WorkerSettlementRecord>();
        int verifiedProofs = 0;
        int slashedWorkers = 0;
        decimal totalRewards = 0m;
        decimal totalSlashed = 0m;

        foreach (var proof in request.SubmittedProofs)
        {
            if (!workersMap.TryGetValue(proof.WorkerId, out var worker) ||
                !tasksMap.TryGetValue(proof.TaskId, out var task))
            {
                continue;
            }

            bool hashMatch = string.Equals(proof.ProducedResultHash, task.ExpectedResultHash, StringComparison.OrdinalIgnoreCase);
            bool latencyWithinSla = proof.ExecutionDurationMs <= task.MaxAllowedLatencyMs;

            if (hashMatch && latencyWithinSla)
            {
                verifiedProofs++;
                totalRewards += task.RewardTokens;
                var updatedRep = Math.Min(1.0, Math.Round(worker.HistoricalReputationScore + 0.05, 2));

                settlements.Add(new WorkerSettlementRecord(
                    WorkerId: proof.WorkerId,
                    TaskId: proof.TaskId,
                    IsVerified: true,
                    RewardEarnedTokens: task.RewardTokens,
                    SlashedTokens: 0m,
                    UpdatedReputation: updatedRep,
                    VerificationNote: "PoUW valid. Latency within SLA."
                ));
            }
            else if (!hashMatch)
            {
                // Byzantine output mismatch -> severe slashing
                slashedWorkers++;
                var slashAmount = Math.Round(worker.StakedCollateralTokens * request.SlashingFraction, 2);
                totalSlashed += slashAmount;
                var updatedRep = Math.Max(0.0, Math.Round(worker.HistoricalReputationScore - 0.50, 2));

                settlements.Add(new WorkerSettlementRecord(
                    WorkerId: proof.WorkerId,
                    TaskId: proof.TaskId,
                    IsVerified: false,
                    RewardEarnedTokens: 0m,
                    SlashedTokens: slashAmount,
                    UpdatedReputation: updatedRep,
                    VerificationNote: "Byzantine hash mismatch detected. Collateral slashed."
                ));
            }
            else
            {
                // Hash matched but breached latency SLA -> zero reward, minor reputation penalty
                var updatedRep = Math.Max(0.0, Math.Round(worker.HistoricalReputationScore - 0.10, 2));

                settlements.Add(new WorkerSettlementRecord(
                    WorkerId: proof.WorkerId,
                    TaskId: proof.TaskId,
                    IsVerified: false,
                    RewardEarnedTokens: 0m,
                    SlashedTokens: 0m,
                    UpdatedReputation: updatedRep,
                    VerificationNote: $"Latency SLA breached ({proof.ExecutionDurationMs}ms > {task.MaxAllowedLatencyMs}ms)."
                ));
            }
        }

        double networkIntegrityScore = request.SubmittedProofs.Count > 0
            ? Math.Round(((double)verifiedProofs / request.SubmittedProofs.Count) * 100.0, 2)
            : 100.0;

        return new DePinArbitrationResult(
            TotalTasksEvaluated: request.SubmittedProofs.Count,
            VerifiedProofs: verifiedProofs,
            SlashedWorkers: slashedWorkers,
            TotalRewardsDisbursed: totalRewards,
            TotalTokensSlashed: totalSlashed,
            Settlements: settlements,
            NetworkIntegrityScore: networkIntegrityScore
        );
    }
}
