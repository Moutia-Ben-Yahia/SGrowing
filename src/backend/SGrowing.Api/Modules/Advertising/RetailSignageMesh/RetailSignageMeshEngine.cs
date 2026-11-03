using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.RetailSignageMesh;

public sealed class RetailSignageMeshEngine
{
    private readonly ConcurrentDictionary<string, InStoreScreenProfile> _screens = new();
    private readonly ConcurrentDictionary<string, ShelfInventoryRecord> _shelves = new();
    private readonly ConcurrentBag<ShelfStockUpdateReceipt> _updateHistory = new();

    public RetailSignageMeshEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var s1 = new InStoreScreenProfile(
            "SCR-NYC-FLAGSHIP-01",
            "Manhattan Flagship Store #104",
            "Premium Endcap Aisle 4 (Organic Beverages)",
            "3840x2160 4K HDR",
            420,
            "Electrolyte Hydration Labs"
        );

        var s2 = new InStoreScreenProfile(
            "SCR-LDN-OXFORD-02",
            "London Oxford Street #202",
            "Checkout Lane 3 Digital Merchandiser",
            "1920x1080 FHD",
            680,
            "BioActive Snack Bars"
        );

        _screens[s1.ScreenId] = s1;
        _screens[s2.ScreenId] = s2;

        var sh1 = new ShelfInventoryRecord(
            "SHLF-BEV-01",
            s1.ScreenId,
            "079452288114",
            "Sparkling Organic Electrolyte 500ml",
            24,
            5,
            true
        );

        var sh2 = new ShelfInventoryRecord(
            "SHLF-SNK-02",
            s2.ScreenId,
            "088214477123",
            "Salted Caramel Protein Crisps 60g",
            3, // Below threshold
            5,
            false // Ad suppressed due to low stock
        );

        _shelves[sh1.ShelfId] = sh1;
        _shelves[sh2.ShelfId] = sh2;
    }

    public RetailSignageOverview GetOverview()
    {
        var scrList = _screens.Values.ToList();
        var shlfList = _shelves.Values.ToList();

        return new RetailSignageOverview(
            scrList.Count,
            shlfList.Count,
            100.0,
            96.5,
            scrList,
            shlfList
        );
    }

    public ShelfBidEvaluationResult EvaluateShelfBid(EvaluateShelfBidRequest request)
    {
        var shelf = _shelves.TryGetValue(request.ShelfId, out var s) ? s : _shelves.Values.First();

        var canServe = shelf.CurrentUnitsInStock >= shelf.MinimumStockThreshold;
        var status = canServe ? "IN_STOCK_SPONSORED_SERVED" : "OUT_OF_STOCK_SUPPRESSED";
        var cpm = canServe ? 24.50m : 0m;

        return new ShelfBidEvaluationResult(
            $"BID-SHLF-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            request.ScreenId,
            shelf.ShelfId,
            canServe,
            "https://cdn.sgrowing.io/retail/creatives/electrolyte-hydration-4k.mp4",
            cpm,
            status
        );
    }

    public ShelfStockUpdateReceipt UpdateShelfStock(UpdateShelfStockRequest request)
    {
        var shelf = _shelves.TryGetValue(request.ShelfId, out var s) ? s : _shelves.Values.First();
        var prevStock = shelf.CurrentUnitsInStock;
        var adActive = request.NewUnitsCount >= shelf.MinimumStockThreshold;

        var updated = shelf with { CurrentUnitsInStock = request.NewUnitsCount, IsAdActive = adActive };
        _shelves[shelf.ShelfId] = updated;

        var receipt = new ShelfStockUpdateReceipt(
            $"RCP-STK-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            shelf.ShelfId,
            prevStock,
            request.NewUnitsCount,
            adActive != shelf.IsAdActive,
            DateTime.UtcNow
        );

        _updateHistory.Add(receipt);
        return receipt;
    }
}
