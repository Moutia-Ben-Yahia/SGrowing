using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Growth.Influencers;

public class InfluencerEscrowEngine
{
    private readonly ConcurrentDictionary<string, CreatorProfile> _creators = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, EscrowContract> _contracts = new(StringComparer.OrdinalIgnoreCase);

    public InfluencerEscrowEngine()
    {
        SeedData();
    }

    private void SeedData()
    {
        var c1 = new CreatorProfile(
            CreatorId: "creator_sara_tech",
            Handle: "@sarah.builds.saas",
            Platform: "TikTok",
            FollowersCount: 245000,
            EngagementRatePercent: 7.8,
            AuthenticityScore: 96.4,
            NicheCategory: "B2B Tech & DevTools",
            IsVerified: true
        );

        var c2 = new CreatorProfile(
            CreatorId: "creator_dave_growth",
            Handle: "@growthhacker_dave",
            Platform: "YouTube",
            FollowersCount: 180000,
            EngagementRatePercent: 6.2,
            AuthenticityScore: 94.1,
            NicheCategory: "E-Commerce & Marketing",
            IsVerified: true
        );

        _creators[c1.CreatorId] = c1;
        _creators[c2.CreatorId] = c2;

        var contract1 = new EscrowContract(
            ContractId: "escrow_contract_01",
            CreatorId: c1.CreatorId,
            CampaignName: "Growth OS Q4 Launch Blitz",
            TotalEscrowAmount: 5000m,
            ReleasedAmount: 2000m,
            RemainingAmount: 3000m,
            Milestones: new List<EscrowMilestone>
            {
                new("ms_01", "Deliverable 1: In-depth Platform Walkthrough Reel", 2000m, "Reel posted with custom bio link", 50000, 68000, true, true),
                new("ms_02", "Deliverable 2: Follow-up ROI Breakdown Story Series", 3000m, "3x Stories with swipe-up UTM", 75000, 0, false, false)
            },
            Status: "MilestoneApproved",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-10)
        );

        _contracts[contract1.ContractId] = contract1;
    }

    public Task<IReadOnlyList<CreatorProfile>> GetCreatorsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<CreatorProfile>>(_creators.Values.ToList());
    }

    public Task<IReadOnlyList<EscrowContract>> GetContractsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<EscrowContract>>(_contracts.Values.OrderByDescending(c => c.CreatedAtUtc).ToList());
    }

    public Task<EscrowContract> CreateContractAsync(CreateEscrowContractRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_creators.ContainsKey(request.CreatorId))
        {
            throw new KeyNotFoundException($"Creator '{request.CreatorId}' not found.");
        }

        var contractId = $"escrow_{Guid.NewGuid():N}";
        var contract = new EscrowContract(
            ContractId: contractId,
            CreatorId: request.CreatorId,
            CampaignName: request.CampaignName,
            TotalEscrowAmount: request.TotalEscrowAmount,
            ReleasedAmount: 0m,
            RemainingAmount: request.TotalEscrowAmount,
            Milestones: request.Milestones,
            Status: "Funded",
            CreatedAtUtc: DateTime.UtcNow
        );

        _contracts[contractId] = contract;
        return Task.FromResult(contract);
    }

    public Task<EscrowContract> VerifyAndReleaseMilestoneAsync(ReleaseMilestoneRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_contracts.TryGetValue(request.ContractId, out var contract))
        {
            throw new KeyNotFoundException($"Contract '{request.ContractId}' not found.");
        }

        var updatedMilestones = new List<EscrowMilestone>();
        decimal amountToRelease = 0m;

        foreach (var ms in contract.Milestones)
        {
            if (ms.MilestoneId.Equals(request.MilestoneId, StringComparison.OrdinalIgnoreCase))
            {
                if (ms.IsReleased)
                {
                    throw new InvalidOperationException($"Milestone '{request.MilestoneId}' has already been released.");
                }

                amountToRelease = ms.PayoutAmount;
                updatedMilestones.Add(ms with
                {
                    ActualViews = request.ActualViewsAchieved,
                    IsVerified = true,
                    IsReleased = true
                });
            }
            else
            {
                updatedMilestones.Add(ms);
            }
        }

        var newReleased = contract.ReleasedAmount + amountToRelease;
        var newRemaining = contract.TotalEscrowAmount - newReleased;
        var newStatus = newRemaining <= 0 ? "Completed" : "MilestoneApproved";

        var updated = contract with
        {
            Milestones = updatedMilestones,
            ReleasedAmount = newReleased,
            RemainingAmount = newRemaining,
            Status = newStatus,
            CompletedAtUtc = newStatus == "Completed" ? DateTime.UtcNow : null
        };

        _contracts[contract.ContractId] = updated;
        return Task.FromResult(updated);
    }

    public Task<CreatorCampaignSummary> GetCampaignSummaryAsync(CancellationToken ct = default)
    {
        var contracts = _contracts.Values.ToList();
        var totalEscrow = contracts.Sum(c => c.TotalEscrowAmount);
        var released = contracts.Sum(c => c.ReleasedAmount);
        var gmv = released * 4.8m; // 4.8x average creator ROAS
        var roas = released > 0 ? (double)(gmv / released) : 0;

        return Task.FromResult(new CreatorCampaignSummary(
            TotalActiveContracts: contracts.Count,
            TotalEscrowSecured: totalEscrow,
            TotalReleasedPayouts: released,
            AttributedGmv: gmv,
            InfluencerRoas: Math.Round(roas, 2)
        ));
    }
}
