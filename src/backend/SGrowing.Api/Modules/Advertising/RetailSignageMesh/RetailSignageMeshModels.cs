using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.RetailSignageMesh;

public sealed record RetailSignageOverview(
    int ActiveInStoreScreensCount,
    int TotalPlanogramShelvesMonitored,
    double OutOfStockSuppressionRatePct,
    double ScreenAdFillRatePct,
    IReadOnlyList<InStoreScreenProfile> Screens,
    IReadOnlyList<ShelfInventoryRecord> Shelves
);

public sealed record InStoreScreenProfile(
    string ScreenId,
    string StoreLocation, // Flagship Manhattan Store, London Oxford St, Tokyo Shibuya
    string DisplayLocation, // Endcap Aisle 4, Checkout Register 2, Smart Cooler Door 1
    string ScreenResolution,
    int HourlyFootfallCount,
    string CurrentSponsoredBrand
);

public sealed record ShelfInventoryRecord(
    string ShelfId,
    string AssociatedScreenId,
    string UpcBarcode,
    string ProductName,
    int CurrentUnitsInStock,
    int MinimumStockThreshold,
    bool IsAdActive
);

public sealed record EvaluateShelfBidRequest(
    string ScreenId,
    string ShelfId,
    int ShopperProximityCount,
    string TimeOfDay
);

public sealed record ShelfBidEvaluationResult(
    string BidId,
    string ScreenId,
    string ShelfId,
    bool CanServeAd,
    string WinningBrandCreativeUrl,
    decimal WinningCpmUsd,
    string InventoryStatus
);

public sealed record UpdateShelfStockRequest(
    string ShelfId,
    int NewUnitsCount,
    string UpdateSource // RFID_PORTAL, WEIGHT_SENSOR, POS_SCAN
);

public sealed record ShelfStockUpdateReceipt(
    string ReceiptId,
    string ShelfId,
    int PreviousStock,
    int UpdatedStock,
    bool AdStatusToggled,
    DateTime Timestamp
);
