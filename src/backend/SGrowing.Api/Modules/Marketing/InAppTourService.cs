using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.Marketing;

public enum TourPlacement
{
    Top = 1,
    Bottom = 2,
    Left = 3,
    Right = 4
}

public enum AnnouncementModalType
{
    CenteredModal = 1,
    FloatingBanner = 2,
    SlideInToast = 3
}

public class ProductTour
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetRoute { get; set; } = string.Empty;
    public string TargetRole { get; set; } = "All";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<TourStep> Steps { get; set; } = new();
}

public class TourStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TourId { get; set; }
    public int StepOrder { get; set; }
    public string TargetSelector { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public TourPlacement Placement { get; set; } = TourPlacement.Bottom;
    public bool BackdropHighlight { get; set; } = true;
    public bool RequireInteraction { get; set; } = false;
}

public class AnnouncementModal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string CtaText { get; set; } = string.Empty;
    public string CtaUrl { get; set; } = string.Empty;
    public AnnouncementModalType ModalType { get; set; } = AnnouncementModalType.CenteredModal;
    public string TargetRole { get; set; } = "All";
    public string TargetRouteRegex { get; set; } = ".*";
    public int MinSessionCount { get; set; } = 1;
    public int Priority { get; set; } = 100;
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class UserTourProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid TourId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public int CurrentStepOrder { get; set; } = 1;
    public int HighestStepReached { get; set; } = 1;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAtUtc { get; set; }
    public bool IsSkipped { get; set; } = false;
    public DateTime? SkippedAtUtc { get; set; }
    public List<TourStepTelemetryEvent> Events { get; set; } = new();
}

public class TourStepTelemetryEvent
{
    public int StepOrder { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class UserAnnouncementDismissal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid AnnouncementId { get; set; }
    public DateTime DismissedAtUtc { get; set; } = DateTime.UtcNow;
    public bool CtaClicked { get; set; } = false;
}

// Request and Response DTOs
public record CreateProductTourRequest(
    string Title,
    string Description,
    string TargetRoute,
    string TargetRole,
    bool IsActive = true,
    List<CreateTourStepRequest>? Steps = null
);

public record CreateTourStepRequest(
    int StepOrder,
    string TargetSelector,
    string Title,
    string Content,
    TourPlacement Placement,
    bool BackdropHighlight = true,
    bool RequireInteraction = false
);

public record CreateAnnouncementRequest(
    string Title,
    string BodyText,
    string CtaText,
    string CtaUrl,
    AnnouncementModalType ModalType,
    string TargetRole,
    string TargetRouteRegex = ".*",
    int MinSessionCount = 1,
    int Priority = 100,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    bool IsActive = true
);

public record RecordTourTelemetryRequest(
    string UserId,
    string Action,
    int StepOrder
);

public record DismissAnnouncementRequest(
    string UserId,
    bool CtaClicked = false
);

public record EligibleTourDto(
    ProductTour Tour,
    UserTourProgress? UserProgress
);

public record StepDropoffMetricDto(
    int StepOrder,
    string StepTitle,
    int UsersReached,
    double RetentionRatePercent,
    double DropoffRatePercent
);

public record TourFunnelMetricDto(
    Guid TourId,
    string TourTitle,
    int TotalStarted,
    int TotalCompleted,
    int TotalSkipped,
    double CompletionRatePercent,
    List<StepDropoffMetricDto> StepDropoffs
);

public record AnnouncementMetricDto(
    Guid AnnouncementId,
    string Title,
    AnnouncementModalType ModalType,
    int Impressions,
    int Dismissals,
    int CtaClicks,
    double CtrPercent
);

public record OnboardingMetricsSummaryDto(
    int ActiveToursCount,
    int ActiveAnnouncementsCount,
    int TotalTourStarts,
    int TotalTourCompletions,
    double OverallTourCompletionRate,
    List<TourFunnelMetricDto> TourFunnels,
    List<AnnouncementMetricDto> AnnouncementMetrics
);

public interface IInAppTourService
{
    Task<ProductTour> CreateTourAsync(Guid tenantId, CreateProductTourRequest request);
    Task<ProductTour?> GetTourByIdAsync(Guid tenantId, Guid tourId);
    Task<List<ProductTour>> GetAllToursAsync(Guid tenantId);
    Task<List<TourStep>> SetTourStepsAsync(Guid tenantId, Guid tourId, List<CreateTourStepRequest> steps);
    Task<List<EligibleTourDto>> GetEligibleToursAsync(Guid tenantId, string userId, string currentRoute, string userRole);
    Task<UserTourProgress> RecordTourTelemetryAsync(Guid tenantId, Guid tourId, string userId, string action, int stepOrder);
    Task<AnnouncementModal> CreateAnnouncementAsync(Guid tenantId, CreateAnnouncementRequest request);
    Task<List<AnnouncementModal>> GetAllAnnouncementsAsync(Guid tenantId);
    Task<List<AnnouncementModal>> GetEligibleAnnouncementsAsync(Guid tenantId, string userId, string currentRoute, string userRole, int sessionCount);
    Task<UserAnnouncementDismissal> DismissAnnouncementAsync(Guid tenantId, Guid announcementId, string userId, bool ctaClicked);
    Task<OnboardingMetricsSummaryDto> GetMetricsAsync(Guid tenantId);
}

public class InAppTourService : IInAppTourService
{
    private static readonly ConcurrentDictionary<Guid, ProductTour> _tours = new();
    private static readonly ConcurrentDictionary<Guid, AnnouncementModal> _announcements = new();
    private static readonly ConcurrentDictionary<string, UserTourProgress> _tourProgress = new();
    private static readonly ConcurrentDictionary<string, UserAnnouncementDismissal> _dismissals = new();
    private static readonly ConcurrentDictionary<Guid, int> _announcementImpressions = new();

    public Task<ProductTour> CreateTourAsync(Guid tenantId, CreateProductTourRequest request)
    {
        var tour = new ProductTour
        {
            TenantId = tenantId,
            Title = request.Title,
            Description = request.Description,
            TargetRoute = request.TargetRoute,
            TargetRole = string.IsNullOrWhiteSpace(request.TargetRole) ? "All" : request.TargetRole,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (request.Steps != null && request.Steps.Count > 0)
        {
            tour.Steps = request.Steps
                .OrderBy(s => s.StepOrder)
                .Select(s => new TourStep
                {
                    TourId = tour.Id,
                    StepOrder = s.StepOrder,
                    TargetSelector = s.TargetSelector,
                    Title = s.Title,
                    Content = s.Content,
                    Placement = s.Placement,
                    BackdropHighlight = s.BackdropHighlight,
                    RequireInteraction = s.RequireInteraction
                })
                .ToList();
        }

        _tours[tour.Id] = tour;
        return Task.FromResult(tour);
    }

    public Task<ProductTour?> GetTourByIdAsync(Guid tenantId, Guid tourId)
    {
        EnsureDefaultSeedData(tenantId);
        if (_tours.TryGetValue(tourId, out var tour) && tour.TenantId == tenantId)
        {
            return Task.FromResult<ProductTour?>(tour);
        }
        return Task.FromResult<ProductTour?>(null);
    }

    public Task<List<ProductTour>> GetAllToursAsync(Guid tenantId)
    {
        EnsureDefaultSeedData(tenantId);
        var list = _tours.Values
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<List<TourStep>> SetTourStepsAsync(Guid tenantId, Guid tourId, List<CreateTourStepRequest> steps)
    {
        EnsureDefaultSeedData(tenantId);
        if (!_tours.TryGetValue(tourId, out var tour) || tour.TenantId != tenantId)
        {
            throw new KeyNotFoundException($"Product tour {tourId} not found for tenant {tenantId}");
        }

        tour.Steps = steps
            .OrderBy(s => s.StepOrder)
            .Select(s => new TourStep
            {
                TourId = tourId,
                StepOrder = s.StepOrder,
                TargetSelector = s.TargetSelector,
                Title = s.Title,
                Content = s.Content,
                Placement = s.Placement,
                BackdropHighlight = s.BackdropHighlight,
                RequireInteraction = s.RequireInteraction
            })
            .ToList();

        return Task.FromResult(tour.Steps);
    }

    public Task<List<EligibleTourDto>> GetEligibleToursAsync(Guid tenantId, string userId, string currentRoute, string userRole)
    {
        EnsureDefaultSeedData(tenantId);
        var normalizedRoute = NormalizeRoute(currentRoute);

        var eligible = new List<EligibleTourDto>();

        foreach (var tour in _tours.Values.Where(t => t.TenantId == tenantId && t.IsActive))
        {
            // Route check
            if (!RouteMatches(tour.TargetRoute, normalizedRoute))
            {
                continue;
            }

            // Role check
            if (!RoleMatches(tour.TargetRole, userRole))
            {
                continue;
            }

            // Prior progress check
            var progressKey = $"{tenantId}:{tour.Id}:{userId}";
            _tourProgress.TryGetValue(progressKey, out var progress);

            // If user has already completed or skipped this tour, do not auto-trigger
            if (progress != null && (progress.IsCompleted || progress.IsSkipped))
            {
                continue;
            }

            eligible.Add(new EligibleTourDto(tour, progress));
        }

        return Task.FromResult(eligible);
    }

    public Task<UserTourProgress> RecordTourTelemetryAsync(Guid tenantId, Guid tourId, string userId, string action, int stepOrder)
    {
        EnsureDefaultSeedData(tenantId);
        var progressKey = $"{tenantId}:{tourId}:{userId}";
        var progress = _tourProgress.GetOrAdd(progressKey, _ => new UserTourProgress
        {
            TenantId = tenantId,
            TourId = tourId,
            UserId = userId,
            StartedAtUtc = DateTime.UtcNow,
            CurrentStepOrder = Math.Max(1, stepOrder),
            HighestStepReached = Math.Max(1, stepOrder)
        });

        lock (progress)
        {
            var normalizedAction = action.ToLowerInvariant();
            progress.Events.Add(new TourStepTelemetryEvent
            {
                StepOrder = stepOrder,
                Action = normalizedAction,
                TimestampUtc = DateTime.UtcNow
            });

            if (stepOrder > progress.HighestStepReached)
            {
                progress.HighestStepReached = stepOrder;
            }

            progress.CurrentStepOrder = stepOrder;

            switch (normalizedAction)
            {
                case "start":
                    progress.StartedAtUtc = DateTime.UtcNow;
                    break;
                case "stepviewed":
                case "step_viewed":
                    break;
                case "stepcompleted":
                case "step_completed":
                    break;
                case "completed":
                case "complete":
                    progress.IsCompleted = true;
                    progress.CompletedAtUtc = DateTime.UtcNow;
                    break;
                case "skipped":
                case "dismissed":
                case "skip":
                case "dismiss":
                    progress.IsSkipped = true;
                    progress.SkippedAtUtc = DateTime.UtcNow;
                    break;
            }
        }

        return Task.FromResult(progress);
    }

    public Task<AnnouncementModal> CreateAnnouncementAsync(Guid tenantId, CreateAnnouncementRequest request)
    {
        var announcement = new AnnouncementModal
        {
            TenantId = tenantId,
            Title = request.Title,
            BodyText = request.BodyText,
            CtaText = request.CtaText,
            CtaUrl = request.CtaUrl,
            ModalType = request.ModalType,
            TargetRole = string.IsNullOrWhiteSpace(request.TargetRole) ? "All" : request.TargetRole,
            TargetRouteRegex = string.IsNullOrWhiteSpace(request.TargetRouteRegex) ? ".*" : request.TargetRouteRegex,
            MinSessionCount = request.MinSessionCount,
            Priority = request.Priority,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _announcements[announcement.Id] = announcement;
        return Task.FromResult(announcement);
    }

    public Task<List<AnnouncementModal>> GetAllAnnouncementsAsync(Guid tenantId)
    {
        EnsureDefaultSeedData(tenantId);
        var list = _announcements.Values
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Priority)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<List<AnnouncementModal>> GetEligibleAnnouncementsAsync(
        Guid tenantId,
        string userId,
        string currentRoute,
        string userRole,
        int sessionCount)
    {
        EnsureDefaultSeedData(tenantId);
        var now = DateTime.UtcNow;
        var normalizedRoute = NormalizeRoute(currentRoute);

        var eligible = new List<AnnouncementModal>();

        foreach (var ann in _announcements.Values.Where(a => a.TenantId == tenantId && a.IsActive))
        {
            // Date range check
            if (ann.StartDateUtc.HasValue && now < ann.StartDateUtc.Value)
            {
                continue;
            }
            if (ann.EndDateUtc.HasValue && now > ann.EndDateUtc.Value)
            {
                continue;
            }

            // Session count threshold
            if (sessionCount < ann.MinSessionCount)
            {
                continue;
            }

            // Role match
            if (!RoleMatches(ann.TargetRole, userRole))
            {
                continue;
            }

            // Route regex match
            if (!RouteRegexMatches(ann.TargetRouteRegex, normalizedRoute))
            {
                continue;
            }

            // Dismissal check (prevent repeated impressions if already dismissed)
            var dismissalKey = $"{tenantId}:{ann.Id}:{userId}";
            if (_dismissals.ContainsKey(dismissalKey))
            {
                continue;
            }

            // Record impression
            _announcementImpressions.AddOrUpdate(ann.Id, 1, (_, count) => count + 1);
            eligible.Add(ann);
        }

        return Task.FromResult(eligible.OrderByDescending(a => a.Priority).ToList());
    }

    public Task<UserAnnouncementDismissal> DismissAnnouncementAsync(Guid tenantId, Guid announcementId, string userId, bool ctaClicked)
    {
        EnsureDefaultSeedData(tenantId);
        var dismissalKey = $"{tenantId}:{announcementId}:{userId}";
        var dismissal = _dismissals.AddOrUpdate(
            dismissalKey,
            _ => new UserAnnouncementDismissal
            {
                TenantId = tenantId,
                AnnouncementId = announcementId,
                UserId = userId,
                DismissedAtUtc = DateTime.UtcNow,
                CtaClicked = ctaClicked
            },
            (_, existing) =>
            {
                if (ctaClicked)
                {
                    existing.CtaClicked = true;
                }
                return existing;
            });

        return Task.FromResult(dismissal);
    }

    public Task<OnboardingMetricsSummaryDto> GetMetricsAsync(Guid tenantId)
    {
        EnsureDefaultSeedData(tenantId);
        var tours = _tours.Values.Where(t => t.TenantId == tenantId).ToList();
        var announcements = _announcements.Values.Where(a => a.TenantId == tenantId).ToList();

        var tourFunnels = new List<TourFunnelMetricDto>();
        int totalStartsAcrossAll = 0;
        int totalCompletionsAcrossAll = 0;

        foreach (var tour in tours)
        {
            var tourProgresses = _tourProgress.Values
                .Where(p => p.TenantId == tenantId && p.TourId == tour.Id)
                .ToList();

            var totalStarted = tourProgresses.Count;
            var totalCompleted = tourProgresses.Count(p => p.IsCompleted);
            var totalSkipped = tourProgresses.Count(p => p.IsSkipped);
            var completionPct = totalStarted > 0 ? Math.Round((double)totalCompleted / totalStarted * 100.0, 1) : 0.0;

            totalStartsAcrossAll += totalStarted;
            totalCompletionsAcrossAll += totalCompleted;

            var stepDropoffs = new List<StepDropoffMetricDto>();
            var orderedSteps = tour.Steps.OrderBy(s => s.StepOrder).ToList();

            for (int i = 0; i < orderedSteps.Count; i++)
            {
                var step = orderedSteps[i];
                var usersReached = tourProgresses.Count(p => p.HighestStepReached >= step.StepOrder);
                var retentionPct = totalStarted > 0 ? Math.Round((double)usersReached / totalStarted * 100.0, 1) : 0.0;

                double dropoffPct = 0.0;
                if (i == 0)
                {
                    dropoffPct = totalStarted > 0 ? Math.Round((1.0 - (double)usersReached / totalStarted) * 100.0, 1) : 0.0;
                }
                else
                {
                    var prevReached = tourProgresses.Count(p => p.HighestStepReached >= orderedSteps[i - 1].StepOrder);
                    dropoffPct = prevReached > 0 ? Math.Round((1.0 - (double)usersReached / prevReached) * 100.0, 1) : 0.0;
                }

                stepDropoffs.Add(new StepDropoffMetricDto(
                    step.StepOrder,
                    step.Title,
                    usersReached,
                    retentionPct,
                    Math.Max(0.0, dropoffPct)
                ));
            }

            tourFunnels.Add(new TourFunnelMetricDto(
                tour.Id,
                tour.Title,
                totalStarted,
                totalCompleted,
                totalSkipped,
                completionPct,
                stepDropoffs
            ));
        }

        var announcementMetrics = new List<AnnouncementMetricDto>();
        foreach (var ann in announcements)
        {
            _announcementImpressions.TryGetValue(ann.Id, out var impressions);
            var annDismissals = _dismissals.Values.Where(d => d.TenantId == tenantId && d.AnnouncementId == ann.Id).ToList();
            var dismissCount = annDismissals.Count;
            var ctaClicks = annDismissals.Count(d => d.CtaClicked);
            var totalInteractions = impressions > 0 ? impressions : Math.Max(impressCount(dismissCount, ctaClicks), 1);
            var ctrPct = totalInteractions > 0 ? Math.Round((double)ctaClicks / totalInteractions * 100.0, 1) : 0.0;

            announcementMetrics.Add(new AnnouncementMetricDto(
                ann.Id,
                ann.Title,
                ann.ModalType,
                Math.Max(impressions, dismissCount),
                dismissCount,
                ctaClicks,
                ctrPct
            ));
        }

        var overallCompletionRate = totalStartsAcrossAll > 0
            ? Math.Round((double)totalCompletionsAcrossAll / totalStartsAcrossAll * 100.0, 1)
            : 0.0;

        return Task.FromResult(new OnboardingMetricsSummaryDto(
            ActiveToursCount: tours.Count(t => t.IsActive),
            ActiveAnnouncementsCount: announcements.Count(a => a.IsActive),
            TotalTourStarts: totalStartsAcrossAll,
            TotalTourCompletions: totalCompletionsAcrossAll,
            OverallTourCompletionRate: overallCompletionRate,
            TourFunnels: tourFunnels,
            AnnouncementMetrics: announcementMetrics
        ));
    }

    private static int impressCount(int dismissals, int clicks) => Math.Max(dismissals + clicks, 1);

    private static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return "/";
        var trimmed = route.Trim();
        if (!trimmed.StartsWith("/")) trimmed = "/" + trimmed;
        if (trimmed.Length > 1 && trimmed.EndsWith("/")) trimmed = trimmed.TrimEnd('/');
        return trimmed;
    }

    private static bool RouteMatches(string targetRoute, string currentRoute)
    {
        if (string.IsNullOrWhiteSpace(targetRoute) || targetRoute == "*" || targetRoute == ".*")
        {
            return true;
        }

        var normTarget = NormalizeRoute(targetRoute);
        var normCurrent = NormalizeRoute(currentRoute);

        if (normTarget.Equals(normCurrent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Support prefix matching e.g. /channels/whatsapp-pairing matches /channels/whatsapp-pairing?instance=demo
        if (normCurrent.StartsWith(normTarget + "/", StringComparison.OrdinalIgnoreCase) ||
            normCurrent.StartsWith(normTarget + "?", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool RouteRegexMatches(string regexPattern, string currentRoute)
    {
        if (string.IsNullOrWhiteSpace(regexPattern) || regexPattern == "*" || regexPattern == ".*")
        {
            return true;
        }

        try
        {
            return Regex.IsMatch(currentRoute, regexPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        }
        catch
        {
            return currentRoute.Contains(regexPattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool RoleMatches(string targetRole, string userRole)
    {
        if (string.IsNullOrWhiteSpace(targetRole) ||
            targetRole.Equals("All", StringComparison.OrdinalIgnoreCase) ||
            targetRole.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(userRole))
        {
            return false;
        }

        return targetRole.Equals(userRole, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureDefaultSeedData(Guid tenantId)
    {
        if (_tours.Values.Any(t => t.TenantId == tenantId))
        {
            return;
        }

        // 1. WhatsApp Pairing Guided Tour
        var whatsappTour = new ProductTour
        {
            TenantId = tenantId,
            Title = "WhatsApp Gateway Pairing Walkthrough",
            Description = "Connect your smartphone to the real-time WhatsApp Baileys engine in under 60 seconds.",
            TargetRoute = "/channels/whatsapp-pairing",
            TargetRole = "Admin",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            Steps = new List<TourStep>
            {
                new()
                {
                    StepOrder = 1,
                    TargetSelector = "[data-tour-step='qr-code']",
                    Title = "Scan WhatsApp QR Code",
                    Content = "Open WhatsApp on your mobile device > Settings > Linked Devices > Link a Device, then point your camera here.",
                    Placement = TourPlacement.Right,
                    BackdropHighlight = true,
                    RequireInteraction = true
                },
                new()
                {
                    StepOrder = 2,
                    TargetSelector = "[data-tour-step='webhook-status']",
                    Title = "Verify Inbound Webhook",
                    Content = "Observe the connection heartbeat pulse green when socket handshake completes.",
                    Placement = TourPlacement.Bottom,
                    BackdropHighlight = true,
                    RequireInteraction = false
                },
                new()
                {
                    StepOrder = 3,
                    TargetSelector = "[data-tour-step='test-dispatch']",
                    Title = "Send Live Test Ping",
                    Content = "Send a test welcome template to confirm bidirectional message delivery.",
                    Placement = TourPlacement.Top,
                    BackdropHighlight = true,
                    RequireInteraction = false
                }
            }
        };
        _tours[whatsappTour.Id] = whatsappTour;

        // 2. Visual Funnel Canvas Guided Tour
        var funnelTour = new ProductTour
        {
            TenantId = tenantId,
            Title = "Visual Funnel Studio Tour",
            Description = "Build and optimize frictionless COD checkouts and 1-click upsells.",
            TargetRoute = "/funnels/canvas",
            TargetRole = "Merchant",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            Steps = new List<TourStep>
            {
                new()
                {
                    StepOrder = 1,
                    TargetSelector = "[data-tour-step='node-palette']",
                    Title = "Drag-and-Drop Nodes",
                    Content = "Select from Order Forms, Upsell Triggers, and WhatsApp Confirmation nodes.",
                    Placement = TourPlacement.Right,
                    BackdropHighlight = true,
                    RequireInteraction = false
                },
                new()
                {
                    StepOrder = 2,
                    TargetSelector = "[data-tour-step='canvas-viewport']",
                    Title = "Connect Routing Cables",
                    Content = "Draw cables between nodes to define conditional branching based on cart value.",
                    Placement = TourPlacement.Bottom,
                    BackdropHighlight = true,
                    RequireInteraction = false
                },
                new()
                {
                    StepOrder = 3,
                    TargetSelector = "[data-tour-step='publish-btn']",
                    Title = "Deploy to Production",
                    Content = "One-click deployment with built-in CDN edge distribution and conversion caching.",
                    Placement = TourPlacement.Left,
                    BackdropHighlight = true,
                    RequireInteraction = true
                }
            }
        };
        _tours[funnelTour.Id] = funnelTour;

        // Seed some sample progress to provide meaningful initial metrics
        var sampleUserA = "usr_seed_merchant_1";
        var progressKeyA = $"{tenantId}:{whatsappTour.Id}:{sampleUserA}";
        _tourProgress[progressKeyA] = new UserTourProgress
        {
            TenantId = tenantId,
            TourId = whatsappTour.Id,
            UserId = sampleUserA,
            StartedAtUtc = DateTime.UtcNow.AddHours(-12),
            CurrentStepOrder = 3,
            HighestStepReached = 3,
            IsCompleted = true,
            CompletedAtUtc = DateTime.UtcNow.AddHours(-11)
        };

        var sampleUserB = "usr_seed_merchant_2";
        var progressKeyB = $"{tenantId}:{whatsappTour.Id}:{sampleUserB}";
        _tourProgress[progressKeyB] = new UserTourProgress
        {
            TenantId = tenantId,
            TourId = whatsappTour.Id,
            UserId = sampleUserB,
            StartedAtUtc = DateTime.UtcNow.AddHours(-6),
            CurrentStepOrder = 2,
            HighestStepReached = 2,
            IsCompleted = false
        };

        // Seed Announcements
        var announcement1 = new AnnouncementModal
        {
            TenantId = tenantId,
            Title = "🚀 Welcome to SGrowing v2026!",
            BodyText = "Power your e-commerce growth with native WhatsApp pairing, autonomous SDR fleet, and COD logistics protection.",
            CtaText = "Pair WhatsApp Now",
            CtaUrl = "/channels/whatsapp-pairing",
            ModalType = AnnouncementModalType.CenteredModal,
            TargetRole = "All",
            TargetRouteRegex = ".*",
            MinSessionCount = 1,
            Priority = 100,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };
        _announcements[announcement1.Id] = announcement1;
        _announcementImpressions[announcement1.Id] = 42;

        var announcement2 = new AnnouncementModal
        {
            TenantId = tenantId,
            Title = "⚡ Automated Cash-on-Delivery Reconciliation",
            BodyText = "Eliminate courier discrepancies with automated cash handover manifests and remittance audits.",
            CtaText = "View Reconciliation Cockpit",
            CtaUrl = "/commerce/reconciliation",
            ModalType = AnnouncementModalType.SlideInToast,
            TargetRole = "CourierDispatcher",
            TargetRouteRegex = "^/commerce/.*",
            MinSessionCount = 2,
            Priority = 80,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        _announcements[announcement2.Id] = announcement2;
        _announcementImpressions[announcement2.Id] = 18;

        var dismissalKey = $"{tenantId}:{announcement1.Id}:{sampleUserA}";
        _dismissals[dismissalKey] = new UserAnnouncementDismissal
        {
            TenantId = tenantId,
            AnnouncementId = announcement1.Id,
            UserId = sampleUserA,
            DismissedAtUtc = DateTime.UtcNow.AddHours(-10),
            CtaClicked = true
        };
    }
}
