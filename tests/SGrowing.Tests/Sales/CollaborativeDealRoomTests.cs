using System;
using System.Linq;
using SGrowing.Api.Modules.Sales.CollaborativeDealRoom;
using Xunit;

namespace SGrowing.Tests.Sales;

public sealed class CollaborativeDealRoomTests
{
    [Fact]
    public void GetOverview_ReturnsInitialDealRoomsAndMilestones()
    {
        var engine = new CollaborativeDealRoomEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveRoomsCount >= 2);
        Assert.True(overview.TotalInfluencedDealValueUsd > 0);
        Assert.NotEmpty(overview.ActiveRooms);
        Assert.NotEmpty(overview.RecentMilestones);
    }

    [Fact]
    public void CreateDealRoom_AppendsNewRecordSuccessfully()
    {
        var engine = new CollaborativeDealRoomEngine();
        var request = new CreateDealRoomRequest(
            "Apex Logistics Corp",
            "OPP-LOG-7722",
            520000.00m,
            "Elena Rostova (VP Supply Chain)",
            "Samantha Hayes (Enterprise AE)",
            DateTime.UtcNow.AddDays(60)
        );

        var room = engine.CreateDealRoom(request);

        Assert.NotNull(room);
        Assert.StartsWith("ROOM-", room.RoomId);
        Assert.Equal("Apex Logistics Corp", room.AccountName);
        Assert.Equal(520000.00m, room.DealValueUsd);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveRooms, r => r.RoomId == room.RoomId);
    }

    [Fact]
    public void CompleteMilestone_AttestsAndGeneratesDigestReceipt()
    {
        var engine = new CollaborativeDealRoomEngine();
        var request = new CompleteDealRoomMilestoneRequest(
            "MLS-02",
            "ROOM-CORP-01",
            "david.chen@acmeww.com",
            "Buyer Executive Champion"
        );

        var receipt = engine.CompleteMilestone(request);

        Assert.NotNull(receipt);
        Assert.StartsWith("RCP-MLS-", receipt.ReceiptId);
        Assert.StartsWith("0x", receipt.CryptographicSignoffDigest);
        Assert.Equal("MLS-02", receipt.MilestoneId);

        var overview = engine.GetOverview();
        var completedMs = overview.RecentMilestones.FirstOrDefault(m => m.MilestoneId == "MLS-02");
        Assert.NotNull(completedMs);
        Assert.True(completedMs.IsCompleted);
        Assert.Equal(receipt.CryptographicSignoffDigest, completedMs.CompletionDigest);
    }

    [Fact]
    public void CreateDealRoom_MultipleRooms_TracksTotalInfluencedDealValue()
    {
        var engine = new CollaborativeDealRoomEngine();
        var initialOverview = engine.GetOverview();
        var initialValue = initialOverview.TotalInfluencedDealValueUsd;

        var req1 = new CreateDealRoomRequest(
            "Fintech Dynamics Global",
            "OPP-FIN-100",
            300_000.00m,
            "Dr. Marcus Brody",
            "Samantha Hayes",
            DateTime.UtcNow.AddDays(45)
        );
        var req2 = new CreateDealRoomRequest(
            "CloudScale BioTech",
            "OPP-BIO-200",
            450_000.00m,
            "Sarah Jenkins",
            "Samantha Hayes",
            DateTime.UtcNow.AddDays(90)
        );

        engine.CreateDealRoom(req1);
        engine.CreateDealRoom(req2);

        var updatedOverview = engine.GetOverview();
        Assert.Equal(initialOverview.ActiveRoomsCount + 2, updatedOverview.ActiveRoomsCount);
        Assert.Equal(initialValue + 750_000.00m, updatedOverview.TotalInfluencedDealValueUsd);
    }

    [Fact]
    public void CompleteMilestone_NonExistentMilestone_GeneratesReceiptWithoutThrowing()
    {
        var engine = new CollaborativeDealRoomEngine();
        var request = new CompleteDealRoomMilestoneRequest(
            "MLS-NONEXISTENT-999",
            "ROOM-CORP-01",
            "legal.approver@acmeww.com",
            "External Buyer Counsel"
        );

        var receipt = engine.CompleteMilestone(request);

        Assert.NotNull(receipt);
        Assert.Equal("MLS-NONEXISTENT-999", receipt.MilestoneId);
        Assert.Equal("ROOM-CORP-01", receipt.RoomId);
        Assert.StartsWith("0x", receipt.CryptographicSignoffDigest);
        Assert.Equal(1, receipt.RemainingMilestones);
    }
}
