using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Sales.CollaborativeDealRoom;

public sealed class CollaborativeDealRoomEngine
{
    private readonly ConcurrentDictionary<string, CollaborativeDealRoomRecord> _rooms = new();
    private readonly ConcurrentDictionary<string, DealRoomMilestoneRecord> _milestones = new();
    private readonly ConcurrentBag<DealRoomMilestoneReceipt> _receiptsHistory = new();

    public CollaborativeDealRoomEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var r1 = new CollaborativeDealRoomRecord(
            "ROOM-CORP-01",
            "Acme Worldwide Financial Technologies",
            "OPP-FIN-9988",
            450000.00m,
            "David Chen (Head of Infrastructure)",
            "Samantha Hayes (Enterprise AE)",
            DateTime.UtcNow.AddDays(45),
            5,
            3,
            "ON_TRACK_TO_CLOSE"
        );

        var r2 = new CollaborativeDealRoomRecord(
            "ROOM-CORP-02",
            "OmniHealth Life Sciences Group",
            "OPP-HLT-4411",
            280000.00m,
            "Dr. Aris Thorne (Chief Medical Officer)",
            "Marcus Green (Strategic AE)",
            DateTime.UtcNow.AddDays(30),
            4,
            2,
            "ON_TRACK_TO_CLOSE"
        );

        _rooms[r1.RoomId] = r1;
        _rooms[r2.RoomId] = r2;

        var m1 = new DealRoomMilestoneRecord(
            "MLS-01",
            r1.RoomId,
            "Dual Architecture Security & SOC2 Type II Review",
            "MUTUAL",
            "David Chen & Security SecOps Lead",
            DateTime.UtcNow.AddDays(-10),
            true,
            "DIGEST-0x8899AABBCC"
        );

        var m2 = new DealRoomMilestoneRecord(
            "MLS-02",
            r1.RoomId,
            "Enterprise Procurement Master Services Agreement (MSA) Redlines",
            "BUYER",
            "Legal Counsel (Acme Worldwide)",
            DateTime.UtcNow.AddDays(14),
            false,
            null
        );

        _milestones[m1.MilestoneId] = m1;
        _milestones[m2.MilestoneId] = m2;
    }

    public DealRoomOverview GetOverview()
    {
        var roomList = _rooms.Values.ToList();
        var msList = _milestones.Values.ToList();
        var totalValue = roomList.Sum(r => r.DealValueUsd);

        return new DealRoomOverview(
            roomList.Count,
            totalValue,
            75.0,
            92.4,
            roomList,
            msList
        );
    }

    public CollaborativeDealRoomRecord CreateDealRoom(CreateDealRoomRequest request)
    {
        var roomId = $"ROOM-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var record = new CollaborativeDealRoomRecord(
            roomId,
            request.AccountName,
            request.OpportunityId,
            request.DealValueUsd,
            request.ExecutiveBuyerChampion,
            request.AccountExecutive,
            request.TargetGoLiveDate,
            4,
            0,
            "ACTIVE_ROADMAP"
        );

        _rooms[roomId] = record;
        return record;
    }

    public DealRoomMilestoneReceipt CompleteMilestone(CompleteDealRoomMilestoneRequest request)
    {
        var digest = $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}";
        var receiptId = $"RCP-MLS-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        if (_milestones.TryGetValue(request.MilestoneId, out var ms))
        {
            var updated = ms with { IsCompleted = true, CompletionDigest = digest };
            _milestones[request.MilestoneId] = updated;
        }

        var receipt = new DealRoomMilestoneReceipt(
            receiptId,
            request.MilestoneId,
            request.RoomId,
            digest,
            1,
            DateTime.UtcNow
        );

        _receiptsHistory.Add(receipt);
        return receipt;
    }
}
