using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.AI;
using SGrowing.Api.Modules.Channels.WhatsApp;
using SGrowing.Api.Modules.Commerce.CartReservations;
using SGrowing.Api.Modules.Messaging;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests;

public class EnterpriseRemainingFeaturesTests
{
    private static SGrowingDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CartReservation_Acquire_DecrementsAvailableAndIncrementsReserved()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        db.WarehouseStockRecords.Add(new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = whId,
            Sku = "SKU-CART-TEST",
            QuantityAvailable = 100,
            QuantityReserved = 0
        });
        await db.SaveChangesAsync();

        var service = new CartReservationService(db);
        var cartId = Guid.NewGuid();

        var result = await service.AcquireReservationAsync(tenantId, new AcquireCartReservationRequest(
            WarehouseId: whId,
            Sku: "SKU-CART-TEST",
            Quantity: 15,
            CartId: cartId,
            TtlMinutes: 20
        ));

        Assert.True(result.Success);
        Assert.Equal("Active", result.Status);
        Assert.Equal(15, result.Quantity);

        var updatedStock = await db.WarehouseStockRecords.FirstAsync();
        Assert.Equal(85, updatedStock.QuantityAvailable);
        Assert.Equal(15, updatedStock.QuantityReserved);
    }

    [Fact]
    public async Task CartReservation_Release_RestoresAvailableStock()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        db.WarehouseStockRecords.Add(new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = whId,
            Sku = "SKU-RELEASE-TEST",
            QuantityAvailable = 50,
            QuantityReserved = 0
        });
        await db.SaveChangesAsync();

        var service = new CartReservationService(db);

        // 1. Acquire
        await service.AcquireReservationAsync(tenantId, new AcquireCartReservationRequest(
            WarehouseId: whId,
            Sku: "SKU-RELEASE-TEST",
            Quantity: 10,
            CartId: cartId,
            TtlMinutes: 15
        ));

        // 2. Release
        var released = await service.ReleaseReservationAsync(tenantId, new ReleaseCartReservationRequest(cartId));
        Assert.True(released);

        var stock = await db.WarehouseStockRecords.FirstAsync();
        Assert.Equal(50, stock.QuantityAvailable);
        Assert.Equal(0, stock.QuantityReserved);

        var lockRecord = await db.CartReservationLocks.FirstAsync();
        Assert.Equal("Released", lockRecord.Status);
    }

    [Fact]
    public async Task CartReservation_SweepExpired_RestoresStockAndMarksExpired()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        db.WarehouseStockRecords.Add(new WarehouseStockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = whId,
            Sku = "SKU-SWEEP-TEST",
            QuantityAvailable = 40,
            QuantityReserved = 20
        });

        // Add already-expired lock
        db.CartReservationLocks.Add(new CartReservationLockRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = whId,
            Sku = "SKU-SWEEP-TEST",
            Quantity = 20,
            CartId = cartId,
            Status = "Active",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-25)
        });
        await db.SaveChangesAsync();

        var service = new CartReservationService(db);
        var sweepResult = await service.SweepExpiredReservationsAsync(tenantId);

        Assert.Equal(1, sweepResult.TotalExpiredFound);
        Assert.Equal(20, sweepResult.StockReturnedCount);

        var stock = await db.WarehouseStockRecords.FirstAsync();
        Assert.Equal(60, stock.QuantityAvailable);
        Assert.Equal(0, stock.QuantityReserved);

        var lockRecord = await db.CartReservationLocks.FirstAsync();
        Assert.Equal("Expired", lockRecord.Status);
    }

    [Fact]
    public async Task EventFanout_DispatchesParallelAcrossBrokersWithFaultIsolation()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();

        db.WebhookSubscriptions.Add(new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TargetUrl = "https://client-api.example.com/webhooks/orders",
            SubscribedEvents = "order.created,order.delivered"
        });
        await db.SaveChangesAsync();

        var dedupService = new EventDeduplicationService();
        var logger = NullLogger<MultiBrokerEventFanoutService>.Instance;
        var fanoutService = new MultiBrokerEventFanoutService(db, dedupService, logger, null);

        var payload = "{\"orderId\":\"ORD-101\",\"amount\":129.50}";
        var result = await fanoutService.FanoutEventAsync(
            tenantId,
            "order.delivered",
            payload,
            new EventFanoutOptions(true, true, true, true)
        );

        Assert.True(result.OverallSuccess);
        Assert.Equal(4, result.BrokerDeliveries.Count);
        Assert.Contains(result.BrokerDeliveries, d => d.BrokerName == "HttpWebhooks" && d.Success);
        Assert.Contains(result.BrokerDeliveries, d => d.BrokerName == "SignalRWebSockets" && d.Success);
        Assert.Contains(result.BrokerDeliveries, d => d.BrokerName == "RedisStreams" && d.Success);
        Assert.Contains(result.BrokerDeliveries, d => d.BrokerName == "RabbitMqQueue" && d.Success);
    }

    [Fact]
    public async Task WhatsAppNewsletter_CreatesChannelAndBroadcastsPostWithReactions()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();

        var service = new WhatsAppNewsletterService(db);

        // 1. Create channel
        var channel = await service.CreateChannelAsync(tenantId, new CreateNewsletterChannelRequest(
            Name: "Tech Flash Deals",
            Description: "Daily exclusive hardware drops."
        ));

        Assert.NotNull(channel);
        Assert.EndsWith("@newsletter", channel.ChannelJid);
        Assert.StartsWith("0029Va", channel.InviteCode);

        // 2. Broadcast post
        var post = await service.BroadcastPostAsync(tenantId, channel.Id, new BroadcastNewsletterPostRequest(
            Content: "🚀 Flash Sale 50% Off RTX 5090 Today Only!",
            MediaUrl: "https://media.sgrowing.io/deals/gpu-promo.jpg"
        ));

        Assert.NotNull(post);
        Assert.StartsWith("WMEX-", post.ServerId);

        // 3. React with emoji
        var updatedPost = await service.RecordReactionAsync(tenantId, post.Id, new ReactNewsletterPostRequest(
            Emoji: "🔥",
            SenderJid: "21699123456@s.whatsapp.net"
        ));

        Assert.NotNull(updatedPost);
        var reactions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(updatedPost.ReactionCountsJson);
        Assert.NotNull(reactions);
        Assert.True(reactions.ContainsKey("🔥"));
        Assert.True(reactions["🔥"] >= 1);
    }

    [Fact]
    public async Task AiModelRouter_FallbackChainExecutesWhenPrimaryFails()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var logger = NullLogger<AiModelRouterService>.Instance;

        var router = new AiModelRouterService(db, logger);

        int attempts = 0;
        var execution = await router.ExecuteWithFallbackChainAsync(
            tenantId,
            AiTaskComplexity.FastAndCheap,
            (model, provider) =>
            {
                attempts++;
                if (attempts == 1)
                {
                    // Primary model simulated rate limit 429
                    throw new HttpRequestException("HTTP 429 Rate Limit Exceeded on primary model");
                }
                return Task.FromResult($"Output from fallback model '{model}' ({provider})");
            }
        );

        Assert.True(execution.FallbackOccurred);
        Assert.NotNull(execution.FallbackReason);
        Assert.Equal("gemini-2.5-flash", execution.PrimaryModel);
        Assert.Equal("gpt-4o-mini", execution.ActualModelUsed);
        Assert.Equal("OpenAI", execution.Provider);
        Assert.Contains("Output from fallback model 'gpt-4o-mini'", execution.Result);
    }

    [Fact]
    public async Task AiModelRouter_ReturnsCatalogAndTenantQuotas()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var tenantId = Guid.NewGuid();
        var logger = NullLogger<AiModelRouterService>.Instance;

        var router = new AiModelRouterService(db, logger);

        var catalog = (await router.GetModelCatalogAsync()).ToList();
        Assert.True(catalog.Count >= 10);
        Assert.Contains(catalog, m => m.Provider == "Google" && m.ModelId == "gemini-2.5-flash");
        Assert.Contains(catalog, m => m.Provider == "Anthropic" && m.ModelId.Contains("claude-3-5-sonnet"));
        Assert.Contains(catalog, m => m.Provider == "Ollama");

        var quotas = await router.GetTenantTokenQuotaAsync(tenantId);
        Assert.Equal(tenantId, quotas.TenantId);
        Assert.Equal(5000000, quotas.MonthlyBudgetTokens);
        Assert.False(quotas.QuotaExceeded);
    }

    [Fact]
    public async Task WarehouseStock_HighConcurrency_FiftySimultaneousOrdersCompete_ZeroOversell()
    {
        string dbName = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        string sku = "SKU-FLASH-STRESS";

        // Seed 25 units available
        using (var seedDb = CreateInMemoryDb(dbName))
        {
            seedDb.WarehouseStockRecords.Add(new WarehouseStockRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WarehouseId = whId,
                Sku = sku,
                QuantityAvailable = 25,
                QuantityReserved = 0
            });
            await seedDb.SaveChangesAsync();
        }

        // 50 parallel shoppers simultaneously trying to acquire 1 unit each
        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            using var db = CreateInMemoryDb(dbName);
            var service = new CartReservationService(db);
            return await service.AcquireReservationAsync(tenantId, new AcquireCartReservationRequest(
                WarehouseId: whId,
                Sku: sku,
                Quantity: 1,
                CartId: Guid.NewGuid(),
                TtlMinutes: 15
            ));
        }).ToList();

        var results = await Task.WhenAll(tasks);

        int succeeded = results.Count(r => r.Success);
        int failed = results.Count(r => !r.Success);

        // Exactly 25 succeed, 25 fail - 0 overselling!
        Assert.Equal(25, succeeded);
        Assert.Equal(25, failed);

        // Final stock check
        using (var verifyDb = CreateInMemoryDb(dbName))
        {
            var finalStock = await verifyDb.WarehouseStockRecords.FirstAsync(s => s.Sku == sku);
            Assert.Equal(0, finalStock.QuantityAvailable);
            Assert.Equal(25, finalStock.QuantityReserved);
        }
    }
}
