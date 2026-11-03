using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Sales.CollaborativeDealRoom;

public sealed record DealRoomOverview(
    int ActiveRoomsCount,
    decimal TotalInfluencedDealValueUsd,
    double MilestoneCompletionRatePct,
    double OnTimeClosingRatePct,
    IReadOnlyList<CollaborativeDealRoomRecord> ActiveRooms,
    IReadOnlyList<DealRoomMilestoneRecord> RecentMilestones
);

public sealed record CollaborativeDealRoomRecord(
    string RoomId,
    string AccountName,
    string OpportunityId,
    decimal DealValueUsd,
    string ExecutiveBuyerChampion,
    string AccountExecutive,
    DateTime TargetGoLiveDate,
    int TotalMilestonesCount,
    int CompletedMilestonesCount,
    string Status
);

public sealed record DealRoomMilestoneRecord(
    string MilestoneId,
    string RoomId,
    string Title,
    string OwnerType, // BUYER, SELLER, MUTUAL
    string OwnerName,
    DateTime DueDate,
    bool IsCompleted,
    string? CompletionDigest
);

public sealed record CreateDealRoomRequest(
    string AccountName,
    string OpportunityId,
    decimal DealValueUsd,
    string ExecutiveBuyerChampion,
    string AccountExecutive,
    DateTime TargetGoLiveDate
);

public sealed record CompleteDealRoomMilestoneRequest(
    string MilestoneId,
    string RoomId,
    string SignerEmail,
    string DualSignoffRole
);

public sealed record DealRoomMilestoneReceipt(
    string ReceiptId,
    string MilestoneId,
    string RoomId,
    string CryptographicSignoffDigest,
    int RemainingMilestones,
    DateTime Timestamp
);
