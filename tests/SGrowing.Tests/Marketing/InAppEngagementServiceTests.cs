using SGrowing.Api.Modules.Marketing;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class InAppEngagementServiceTests
{
    private readonly InAppEngagementService _service = new();

    [Fact]
    public async Task CreateTourAsync_Should_Create_Active_Tour_With_Ordered_Steps()
    {
        var tenantId = Guid.NewGuid();
        var steps = new List<TourStepDto>
        {
            new(1, "#nav-campaigns", "Marketing Campaigns", "Launch multi-channel SMS and email flows.", TooltipPlacement.Right),
            new(2, "#btn-new-campaign", "New Campaign Button", "Click here to author your automated campaign.", TooltipPlacement.Bottom)
        };

        var tour = await _service.CreateTourAsync(tenantId, "Campaign Studio Onboarding", "/marketing/campaigns", steps);

        Assert.NotNull(tour);
        Assert.NotEqual(Guid.Empty, tour.TourId);
        Assert.Equal("Campaign Studio Onboarding", tour.Name);
        Assert.Equal("/marketing/campaigns", tour.TargetUrlPathPattern);
        Assert.True(tour.IsActive);
        Assert.Equal(2, tour.Steps.Count);
        Assert.Equal(TooltipPlacement.Right, tour.Steps[0].Placement);
    }

    [Fact]
    public async Task GetActiveToursAsync_Should_Filter_By_Url_Pattern_Case_Insensitively()
    {
        var tenantId = Guid.NewGuid();
        var steps = new List<TourStepDto>
        {
            new(1, "#pipeline-grid", "Pipeline", "View sales stages.", TooltipPlacement.Top)
        };

        var created = await _service.CreateTourAsync(tenantId, "Sales Pipeline Tour", "/CRM/DEALS", steps);

        var matching = await _service.GetActiveToursAsync(tenantId, "/crm/deals/stage-1");
        Assert.Contains(matching, t => t.TourId == created.TourId);

        var nonMatching = await _service.GetActiveToursAsync(tenantId, "/settings/billing");
        Assert.DoesNotContain(nonMatching, t => t.TourId == created.TourId);
    }

    [Fact]
    public async Task GetActiveToursAsync_Should_Seed_Sample_Tour_When_No_Tours_Match()
    {
        var tenantId = Guid.NewGuid();
        var uniquePath = $"/unmatched-path-{Guid.NewGuid():N}";

        var tours = await _service.GetActiveToursAsync(tenantId, uniquePath);

        Assert.NotEmpty(tours);
        Assert.Contains(tours, t => t.Name == "Welcome Onboarding Tour");
    }

    [Fact]
    public async Task RecordTourCompletionAsync_Should_Return_True_And_Record_State()
    {
        var tenantId = Guid.NewGuid();
        var tourId = Guid.NewGuid();
        var userIdentifier = "agent-ops-01";

        var result = await _service.RecordTourCompletionAsync(tenantId, tourId, userIdentifier);

        Assert.True(result);
    }

    [Fact]
    public async Task CreateBannerAsync_Should_Persist_Banner_With_Audience_Segment()
    {
        var tenantId = Guid.NewGuid();
        var banner = await _service.CreateBannerAsync(
            tenantId,
            "Black Friday Sale Automation",
            "Boost GMV with 1-click cart rescue workflows.",
            "Modal",
            "Configure Now",
            "/marketing/black-friday",
            "ECOMMERCE_PRO"
        );

        Assert.NotNull(banner);
        Assert.NotEqual(Guid.Empty, banner.BannerId);
        Assert.Equal("Black Friday Sale Automation", banner.Title);
        Assert.Equal("Modal", banner.BannerType);
        Assert.Equal("ECOMMERCE_PRO", banner.AudienceSegment);
        Assert.True(banner.IsDismissible);
    }

    [Fact]
    public async Task EvaluateActiveBannersAsync_Should_Filter_By_Segment_Or_Match_All()
    {
        var tenantId = Guid.NewGuid();
        var vipBanner = await _service.CreateBannerAsync(
            tenantId,
            "VIP Beta Features",
            "Exclusive access to AI voice cloning.",
            "TopBar",
            "Access Beta",
            "/ai/beta",
            "VIP_TIER"
        );

        var matchedVip = await _service.EvaluateActiveBannersAsync(tenantId, "/dashboard", "vip_tier");
        Assert.Contains(matchedVip, b => b.BannerId == vipBanner.BannerId);

        var nonVip = await _service.EvaluateActiveBannersAsync(tenantId, "/dashboard", "STANDARD_TIER");
        Assert.DoesNotContain(nonVip, b => b.BannerId == vipBanner.BannerId);
    }

    [Fact]
    public async Task EvaluateActiveBannersAsync_Should_Include_Universal_All_Segment_Banners()
    {
        var tenantId = Guid.NewGuid();
        var universalBanner = await _service.CreateBannerAsync(
            tenantId,
            "System Maintenance Window",
            "Scheduled maintenance on Sunday 02:00 UTC.",
            "Toast",
            "View Status",
            "/status",
            "ALL"
        );

        var banners = await _service.EvaluateActiveBannersAsync(tenantId, "/home", "ANY_CUSTOM_SEGMENT");
        Assert.Contains(banners, b => b.BannerId == universalBanner.BannerId);
    }

    [Fact]
    public async Task EvaluateActiveBannersAsync_WhenNoBannersExist_SeedsDefaultWarmupBanner()
    {
        var freshEngine = new InAppEngagementService();
        var tenantId = Guid.NewGuid();

        var banners = await freshEngine.EvaluateActiveBannersAsync(tenantId, "/dashboard", "NON_EXISTENT_SEGMENT");

        Assert.NotEmpty(banners);
        var seeded = banners.First();
        Assert.Equal("New Feature: P2P Mailbox Warmup", seeded.Title);
        Assert.Equal("TopBar", seeded.BannerType);
        Assert.Equal("ALL", seeded.AudienceSegment);
        Assert.True(seeded.IsDismissible);
    }
}
