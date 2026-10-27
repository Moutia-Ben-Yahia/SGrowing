using SGrowing.Api.Data;

namespace SGrowing.Api.Modules.Marketing;

public enum TooltipPlacement
{
    Top = 1,
    Bottom = 2,
    Left = 3,
    Right = 4
}

public record TourStepDto(
    int StepOrder,
    string TargetCssSelector,
    string Title,
    string ContentMarkdown,
    TooltipPlacement Placement
);

public record InAppTourDto(
    Guid TourId,
    string Name,
    string TargetUrlPathPattern,
    List<TourStepDto> Steps,
    bool IsActive
);

public record InAppBannerDto(
    Guid BannerId,
    string Title,
    string BodyText,
    string BannerType, // "Modal", "TopBar", "Toast"
    string CtaButtonText,
    string CtaUrl,
    string AudienceSegment,
    bool IsDismissible
);

public interface IInAppEngagementService
{
    Task<InAppTourDto> CreateTourAsync(Guid tenantId, string name, string targetUrlPathPattern, List<TourStepDto> steps);
    Task<List<InAppTourDto>> GetActiveToursAsync(Guid tenantId, string currentPath);
    Task<bool> RecordTourCompletionAsync(Guid tenantId, Guid tourId, string userIdentifier);
    Task<InAppBannerDto> CreateBannerAsync(Guid tenantId, string title, string bodyText, string bannerType, string ctaButtonText, string ctaUrl, string audienceSegment);
    Task<List<InAppBannerDto>> EvaluateActiveBannersAsync(Guid tenantId, string currentPath, string userSegment);
}

public class InAppEngagementService : IInAppEngagementService
{
    private static readonly Dictionary<Guid, InAppTourDto> _tours = new();
    private static readonly Dictionary<Guid, InAppBannerDto> _banners = new();
    private static readonly HashSet<string> _completedTours = new();

    public Task<InAppTourDto> CreateTourAsync(Guid tenantId, string name, string targetUrlPathPattern, List<TourStepDto> steps)
    {
        var tourId = Guid.NewGuid();
        var tour = new InAppTourDto(tourId, name, targetUrlPathPattern, steps, true);
        _tours[tourId] = tour;
        return Task.FromResult(tour);
    }

    public Task<List<InAppTourDto>> GetActiveToursAsync(Guid tenantId, string currentPath)
    {
        var matched = _tours.Values
            .Where(t => t.IsActive && (string.IsNullOrEmpty(t.TargetUrlPathPattern) || currentPath.Contains(t.TargetUrlPathPattern, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matched.Count == 0)
        {
            // Seed sample tour
            var sampleId = Guid.NewGuid();
            var sample = new InAppTourDto(
                sampleId,
                "Welcome Onboarding Tour",
                "/dashboard",
                new List<TourStepDto>
                {
                    new(1, "#sidebar-conversations", "Omnichannel Inbox", "Access unified WhatsApp, Instagram, and Email conversations right here.", TooltipPlacement.Right),
                    new(2, "#header-quick-dialer", "WebRTC Calling", "Click here to launch the built-in browser softphone and dial leads.", TooltipPlacement.Bottom),
                    new(3, "#kanban-pipeline-view", "Deal Pipeline", "Drag and drop deals across stages. Rotting leads are highlighted automatically.", TooltipPlacement.Top)
                },
                true
            );
            _tours[sampleId] = sample;
            matched.Add(sample);
        }

        return Task.FromResult(matched);
    }

    public Task<bool> RecordTourCompletionAsync(Guid tenantId, Guid tourId, string userIdentifier)
    {
        _completedTours.Add($"{tenantId}:{tourId}:{userIdentifier}");
        return Task.FromResult(true);
    }

    public Task<InAppBannerDto> CreateBannerAsync(Guid tenantId, string title, string bodyText, string bannerType, string ctaButtonText, string ctaUrl, string audienceSegment)
    {
        var bannerId = Guid.NewGuid();
        var banner = new InAppBannerDto(bannerId, title, bodyText, bannerType, ctaButtonText, ctaUrl, audienceSegment, true);
        _banners[bannerId] = banner;
        return Task.FromResult(banner);
    }

    public Task<List<InAppBannerDto>> EvaluateActiveBannersAsync(Guid tenantId, string currentPath, string userSegment)
    {
        var active = _banners.Values
            .Where(b => string.IsNullOrEmpty(b.AudienceSegment) || b.AudienceSegment.Equals(userSegment, StringComparison.OrdinalIgnoreCase) || b.AudienceSegment == "ALL")
            .ToList();

        if (active.Count == 0)
        {
            var defaultBanner = new InAppBannerDto(
                Guid.NewGuid(),
                "New Feature: P2P Mailbox Warmup",
                "Protect your cold email domains with automated spam rescue and warmup networks.",
                "TopBar",
                "Learn More",
                "/deliverability/warmup",
                "ALL",
                true
            );
            _banners[defaultBanner.BannerId] = defaultBanner;
            active.Add(defaultBanner);
        }

        return Task.FromResult(active);
    }
}
