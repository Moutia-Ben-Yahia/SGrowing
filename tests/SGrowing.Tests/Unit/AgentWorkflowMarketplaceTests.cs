using FluentAssertions;
using SGrowing.Api.Modules.AI.Marketplace;
using Xunit;

namespace SGrowing.Tests.Unit;

public class AgentWorkflowMarketplaceTests
{
    private readonly AgentWorkflowMarketplaceService _service;

    public AgentWorkflowMarketplaceTests()
    {
        _service = new AgentWorkflowMarketplaceService();
    }

    [Fact]
    public void Catalog_ContainsAllSixCuratedListings()
    {
        // Act
        var listings = _service.SearchListings();

        // Assert
        listings.Should().HaveCount(6);

        // 1. KSA Ramadan COD Campaign Swarm
        var ramadanSwarm = listings.FirstOrDefault(l => l.Slug == "ksa-ramadan-cod-campaign-swarm");
        ramadanSwarm.Should().NotBeNull();
        ramadanSwarm!.Name.Should().Be("KSA Ramadan COD Campaign Swarm");
        ramadanSwarm.ItemType.Should().Be(MarketplaceItemType.AgentSwarm);
        ramadanSwarm.Tier.Should().Be(MarketplaceTier.Official);
        ramadanSwarm.Rating.Should().Be(5.0);
        ramadanSwarm.InstallCount.Should().Be(1250);
        ramadanSwarm.Tags.Should().Contain(new[] { "Ramadan", "COD", "Saudi Arabia" });

        // 2. Salla WhatsApp Cart Recovery Engine
        var sallaRecovery = listings.FirstOrDefault(l => l.Slug == "salla-whatsapp-cart-recovery-engine");
        sallaRecovery.Should().NotBeNull();
        sallaRecovery!.Name.Should().Be("Salla WhatsApp Cart Recovery Engine");
        sallaRecovery.ItemType.Should().Be(MarketplaceItemType.WorkflowTemplate);
        sallaRecovery.Tier.Should().Be(MarketplaceTier.VerifiedPartner);
        sallaRecovery.Rating.Should().Be(4.9);
        sallaRecovery.InstallCount.Should().Be(890);
        sallaRecovery.PriceSar.Should().Be(149m);

        // 3. B2B Outbound Cold ABM Hunter
        var abmHunter = listings.FirstOrDefault(l => l.Slug == "b2b-outbound-cold-abm-hunter");
        abmHunter.Should().NotBeNull();
        abmHunter!.Name.Should().Be("B2B Outbound Cold ABM Hunter");
        abmHunter.ItemType.Should().Be(MarketplaceItemType.AgentSwarm);
        abmHunter.Tier.Should().Be(MarketplaceTier.Official);
        abmHunter.Rating.Should().Be(4.8);
        abmHunter.InstallCount.Should().Be(640);

        // 4. Omnichannel Spintax Copy Generator
        var spintax = listings.FirstOrDefault(l => l.Slug == "omnichannel-spintax-copy-generator");
        spintax.Should().NotBeNull();
        spintax!.Name.Should().Be("Omnichannel Spintax Copy Generator");
        spintax.ItemType.Should().Be(MarketplaceItemType.PromptPack);
        spintax.Tier.Should().Be(MarketplaceTier.Community);
        spintax.Rating.Should().Be(4.7);
        spintax.InstallCount.Should().Be(420);
        spintax.PriceSar.Should().Be(0m);

        // 5. SEO GEO/AEO Citability Booster
        var seoBooster = listings.FirstOrDefault(l => l.Slug == "seo-geo-aeo-citability-booster");
        seoBooster.Should().NotBeNull();
        seoBooster!.Name.Should().Be("SEO GEO/AEO Citability Booster");
        seoBooster.ItemType.Should().Be(MarketplaceItemType.AgentSwarm);
        seoBooster.Tier.Should().Be(MarketplaceTier.Official);
        seoBooster.Rating.Should().Be(5.0);
        seoBooster.InstallCount.Should().Be(1500);

        // 6. Shopify Flash Sale Surge Orchestrator
        var shopifyFlash = listings.FirstOrDefault(l => l.Slug == "shopify-flash-sale-surge-orchestrator");
        shopifyFlash.Should().NotBeNull();
        shopifyFlash!.Name.Should().Be("Shopify Flash Sale Surge Orchestrator");
        shopifyFlash.ItemType.Should().Be(MarketplaceItemType.WorkflowTemplate);
        shopifyFlash.Tier.Should().Be(MarketplaceTier.VerifiedPartner);
        shopifyFlash.Rating.Should().Be(4.9);
        shopifyFlash.InstallCount.Should().Be(780);
    }

    [Fact]
    public void SearchListings_FilterByType_ReturnsMatchingItemsOnly()
    {
        // Act
        var swarms = _service.SearchListings(type: MarketplaceItemType.AgentSwarm);
        var workflows = _service.SearchListings(type: MarketplaceItemType.WorkflowTemplate);
        var promptPacks = _service.SearchListings(type: MarketplaceItemType.PromptPack);
        var connectors = _service.SearchListings(type: MarketplaceItemType.ConnectorExtension);

        // Assert
        swarms.Should().HaveCount(3);
        swarms.Should().OnlyContain(s => s.ItemType == MarketplaceItemType.AgentSwarm);

        workflows.Should().HaveCount(2);
        workflows.Should().OnlyContain(w => w.ItemType == MarketplaceItemType.WorkflowTemplate);

        promptPacks.Should().HaveCount(1);
        promptPacks.First().Name.Should().Be("Omnichannel Spintax Copy Generator");

        connectors.Should().BeEmpty();
    }

    [Fact]
    public void SearchListings_FilterByTier_ReturnsMatchingTierOnly()
    {
        // Act
        var official = _service.SearchListings(tier: MarketplaceTier.Official);
        var partners = _service.SearchListings(tier: MarketplaceTier.VerifiedPartner);
        var community = _service.SearchListings(tier: MarketplaceTier.Community);

        // Assert
        official.Should().HaveCount(3);
        official.Should().OnlyContain(o => o.Tier == MarketplaceTier.Official);

        partners.Should().HaveCount(2);
        partners.Should().OnlyContain(p => p.Tier == MarketplaceTier.VerifiedPartner);

        community.Should().HaveCount(1);
        community.First().Tier.Should().Be(MarketplaceTier.Community);
    }

    [Fact]
    public void SearchListings_FilterByQuery_MatchesKeywordsInNameDescriptionTagsOrAuthor()
    {
        // Act
        var ramadanResults = _service.SearchListings(query: "Ramadan");
        var sallaResults = _service.SearchListings(query: "Salla");
        var perpleixtyResults = _service.SearchListings(query: "Perplexity");

        // Assert
        ramadanResults.Should().ContainSingle();
        ramadanResults.First().Name.Should().Be("KSA Ramadan COD Campaign Swarm");

        sallaResults.Should().ContainSingle();
        sallaResults.First().Name.Should().Be("Salla WhatsApp Cart Recovery Engine");

        perpleixtyResults.Should().ContainSingle();
        perpleixtyResults.First().Name.Should().Be("SEO GEO/AEO Citability Booster");
    }

    [Fact]
    public void GetListingById_FindsListingByIdOrSlug_OrReturnsNull()
    {
        // Act & Assert
        var byId = _service.GetListingById("item-ramadan-cod-swarm");
        byId.Should().NotBeNull();
        byId!.Name.Should().Be("KSA Ramadan COD Campaign Swarm");

        var bySlug = _service.GetListingById("b2b-outbound-cold-abm-hunter");
        bySlug.Should().NotBeNull();
        bySlug!.Id.Should().Be("item-b2b-abm-hunter");

        var notFound = _service.GetListingById("non-existent-item");
        notFound.Should().BeNull();
    }

    [Fact]
    public void InstallItem_SuccessfulInstallation_IncrementsInstallCountAndPersistsSettings()
    {
        // Arrange
        var tenantId = "tenant-enterprise-001";
        var listing = _service.GetListingById("item-salla-cart-recovery")!;
        var initialInstallCount = listing.InstallCount;

        var customSettings = new Dictionary<string, string>
        {
            ["discountPercentage"] = "15",
            ["delayMinutes"] = "10"
        };

        // Act
        var installed = _service.InstallItem(tenantId, listing.Id, customSettings);

        // Assert
        installed.Should().NotBeNull();
        installed.TenantId.Should().Be(tenantId);
        installed.ListingId.Should().Be(listing.Id);
        installed.InstalledVersion.Should().Be(listing.Version);
        installed.IsActive.Should().BeTrue();
        installed.CustomSettings.Should().ContainKey("discountPercentage").WhoseValue.Should().Be("15");

        // Verify install count incremented
        var updatedListing = _service.GetListingById(listing.Id);
        updatedListing!.InstallCount.Should().Be(initialInstallCount + 1);

        // Verify retrieval via GetInstalledItems
        var installedList = _service.GetInstalledItems(tenantId);
        installedList.Should().ContainSingle(i => i.InstallationId == installed.InstallationId);
    }

    [Fact]
    public void InstallItem_DuplicateActiveInstallation_ThrowsInvalidOperationException()
    {
        // Arrange
        var tenantId = "tenant-duplicate-test";
        var listingId = "item-b2b-abm-hunter";

        _service.InstallItem(tenantId, listingId);

        // Act
        var act = () => _service.InstallItem(tenantId, listingId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already installed*");
    }

    [Fact]
    public void InstallItem_UnknownListing_ThrowsKeyNotFoundException()
    {
        // Act
        var act = () => _service.InstallItem("tenant-test", "unknown-listing-xyz");

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void UninstallItem_SuccessfulUninstallation_DeactivatesItemAndAllowsReinstallation()
    {
        // Arrange
        var tenantId = "tenant-uninstall-test";
        var listingId = "item-seo-geo-citability";
        var installed = _service.InstallItem(tenantId, listingId);

        // Act: Uninstall using InstallationId
        var success = _service.UninstallItem(tenantId, installed.InstallationId.ToString());

        // Assert
        success.Should().BeTrue();
        var installedAfter = _service.GetInstalledItems(tenantId);
        installedAfter.Should().NotContain(i => i.InstallationId == installed.InstallationId);

        // Reinstalling should now succeed
        var reinstall = () => _service.InstallItem(tenantId, listingId);
        reinstall.Should().NotThrow();
    }

    [Fact]
    public void UninstallItem_NotFoundOrWrongTenant_ReturnsFalse()
    {
        // Arrange
        var tenantA = "tenant-a";
        var tenantB = "tenant-b";
        var installedA = _service.InstallItem(tenantA, "item-shopify-flash-sale");

        // Act: Tenant B tries to uninstall Tenant A's item
        var resultWrongTenant = _service.UninstallItem(tenantB, installedA.InstallationId.ToString());
        var resultNonExistent = _service.UninstallItem(tenantA, Guid.NewGuid().ToString());

        // Assert
        resultWrongTenant.Should().BeFalse();
        resultNonExistent.Should().BeFalse();

        // Tenant A's item should remain active
        _service.GetInstalledItems(tenantA).Should().ContainSingle(i => i.InstallationId == installedA.InstallationId);
    }

    [Fact]
    public void MultiTenantIsolation_TenantsHaveIndependentInstallations()
    {
        // Arrange
        var tenant1 = "tenant-alpha";
        var tenant2 = "tenant-beta";
        var listingId = "item-spintax-generator";

        // Act: Both tenants install the same item
        var install1 = _service.InstallItem(tenant1, listingId, new Dictionary<string, string> { ["lang"] = "Arabic" });
        var install2 = _service.InstallItem(tenant2, listingId, new Dictionary<string, string> { ["lang"] = "English" });

        // Assert
        install1.InstallationId.Should().NotBe(install2.InstallationId);
        install1.CustomSettings["lang"].Should().Be("Arabic");
        install2.CustomSettings["lang"].Should().Be("English");

        var itemsTenant1 = _service.GetInstalledItems(tenant1);
        var itemsTenant2 = _service.GetInstalledItems(tenant2);

        itemsTenant1.Should().ContainSingle(i => i.InstallationId == install1.InstallationId);
        itemsTenant1.Should().NotContain(i => i.InstallationId == install2.InstallationId);

        itemsTenant2.Should().ContainSingle(i => i.InstallationId == install2.InstallationId);
        itemsTenant2.Should().NotContain(i => i.InstallationId == install1.InstallationId);
    }
}
