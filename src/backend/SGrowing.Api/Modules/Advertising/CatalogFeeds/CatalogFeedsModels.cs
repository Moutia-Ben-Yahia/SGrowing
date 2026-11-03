using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.CatalogFeeds;

public enum FeedChannel
{
    GoogleMerchantCenter,
    MetaAdvantageCatalog,
    TikTokShop,
    PinterestCommerce
}

public enum FeedFormat
{
    XmlRss2,
    JsonLd,
    CsvTsv
}

public record CatalogProductItem(
    string ProductId,
    string Title,
    string Description,
    decimal Price,
    string Currency,
    string Availability,
    string Category,
    string Brand,
    List<string> Badges
);

public record CatalogFeed(
    string FeedId,
    FeedChannel Channel,
    FeedFormat Format,
    int TotalItems,
    DateTime LastGeneratedAtUtc,
    bool IsCompliant,
    string FeedEndpointUrl
);

public record GenerateFeedRequest(
    FeedChannel Channel,
    FeedFormat Format
);

public record ApplyDynamicBadgeRequest(
    string ProductId,
    string BadgeName
);
