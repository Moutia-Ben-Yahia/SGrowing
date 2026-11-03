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
}
