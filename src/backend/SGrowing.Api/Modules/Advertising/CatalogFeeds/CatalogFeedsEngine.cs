using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.CatalogFeeds;

public interface ICatalogFeedsEngine
{
    List<CatalogFeed> GetFeeds();
    List<CatalogProductItem> GetProducts();
    CatalogFeed GenerateFeed(GenerateFeedRequest request);
    CatalogProductItem ApplyDynamicBadge(ApplyDynamicBadgeRequest request);
}

public class CatalogFeedsEngine : ICatalogFeedsEngine
{
    private readonly ConcurrentDictionary<string, CatalogFeed> _feeds = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CatalogProductItem> _products = new(StringComparer.OrdinalIgnoreCase);

    public CatalogFeedsEngine()
    {
        // Seed catalog products
        var p1 = new CatalogProductItem("prod_saas_ent_01", "SGrowing Growth OS Enterprise Seat", "Unlimited multi-tenant orchestrator and agent swarm access", 4999m, "USD", "in_stock", "Software > Business", "SGrowing", new() { "Free Dedicated Migration", "Enterprise SLA" });
        var p2 = new CatalogProductItem("prod_voice_copilot_02", "Voice AI Autonomous SDR Seat", "Full-duplex WebRTC speech synthesis and live whisper coaching", 899m, "USD", "in_stock", "Software > Communications", "SGrowing", new() { "Instant Provisioning" });
        var p3 = new CatalogProductItem("prod_shard_gateway_03", "Global Zero-Trust Multi-Region Shard Mesh Add-on", "Active-active multi-region PostgreSQL database clustering", 1499m, "USD", "in_stock", "Software > Infrastructure", "SGrowing", new() { "SOC2 Certified" });

        _products[p1.ProductId] = p1;
        _products[p2.ProductId] = p2;
        _products[p3.ProductId] = p3;

        // Seed feeds
        _feeds["feed_google_merchant"] = new CatalogFeed(
            "feed_google_merchant",
            FeedChannel.GoogleMerchantCenter,
            FeedFormat.XmlRss2,
            3,
            DateTime.UtcNow.AddHours(-2),
            true,
            "https://cdn.sgrowing.internal/feeds/google-merchant.xml"
        );

        _feeds["feed_meta_advantage"] = new CatalogFeed(
            "feed_meta_advantage",
            FeedChannel.MetaAdvantageCatalog,
            FeedFormat.CsvTsv,
            3,
            DateTime.UtcNow.AddHours(-1),
            true,
            "https://cdn.sgrowing.internal/feeds/meta-dpa-catalog.csv"
        );

        _feeds["feed_tiktok_shop"] = new CatalogFeed(
            "feed_tiktok_shop",
            FeedChannel.TikTokShop,
            FeedFormat.JsonLd,
            3,
            DateTime.UtcNow.AddMinutes(-30),
            true,
            "https://cdn.sgrowing.internal/feeds/tiktok-shop.json"
        );
    }

    public List<CatalogFeed> GetFeeds() => _feeds.Values.OrderBy(f => f.Channel.ToString()).ToList();

    public List<CatalogProductItem> GetProducts() => _products.Values.OrderBy(p => p.ProductId).ToList();

    public CatalogFeed GenerateFeed(GenerateFeedRequest request)
    {
        var feedId = $"feed_{request.Channel.ToString().ToLowerInvariant()}";
        var extension = request.Format switch
        {
            FeedFormat.XmlRss2 => "xml",
            FeedFormat.JsonLd => "json",
            FeedFormat.CsvTsv => "csv",
            _ => "xml"
        };

        var feed = new CatalogFeed(
            FeedId: feedId,
            Channel: request.Channel,
            Format: request.Format,
            TotalItems: _products.Count,
            LastGeneratedAtUtc: DateTime.UtcNow,
            IsCompliant: true,
            FeedEndpointUrl: $"https://cdn.sgrowing.internal/feeds/{request.Channel.ToString().ToLowerInvariant()}.{extension}"
        );

        _feeds[feedId] = feed;
        return feed;
    }

    public CatalogProductItem ApplyDynamicBadge(ApplyDynamicBadgeRequest request)
    {
        if (!_products.TryGetValue(request.ProductId, out var product))
        {
            throw new KeyNotFoundException($"Product {request.ProductId} not found.");
        }

        var badges = new List<string>(product.Badges);
        if (!badges.Contains(request.BadgeName, StringComparer.OrdinalIgnoreCase))
        {
            badges.Add(request.BadgeName);
        }

        var updated = product with { Badges = badges };
        _products[request.ProductId] = updated;
        return updated;
    }
}
