using System.Text.RegularExpressions;
using SGrowing.Api.Modules.Marketing;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class InAppTourEngineTests
{
    private readonly IInAppTourService _service;
    private readonly Guid _tenantId;

    public InAppTourEngineTests()
    {
        _service = new InAppTourService();
        _tenantId = Guid.NewGuid();
    }

    [Fact]
    public async Task TourCreation_And_StepOrdering_MaintainsCorrectSequence()
    {
        // Arrange
        var request = new CreateProductTourRequest(
            Title: "Multi-Step Warehouse Scan",
            Description: "Barcode intake workflow for warehouse managers",
            TargetRoute: "/commerce/warehouses",
            TargetRole: "WarehouseManager",
            IsActive: true,
            Steps: new List<CreateTourStepRequest>
            {
                new(3, "[data-tour='confirm-print']", "Print Manifest", "Print zebra barcode tag", TourPlacement.Left, true, true),
                new(1, "[data-tour='scan-input']", "Laser Scanner", "Aim at pallet barcode", TourPlacement.Right, true, true),
                new(2, "[data-tour='bin-location']", "Assign Bin", "Select aisle location A3", TourPlacement.Bottom, false, false)
            }
        );

        // Act
        var tour = await _service.CreateTourAsync(_tenantId, request);

        // Assert
        Assert.NotNull(tour);
        Assert.Equal(_tenantId, tour.TenantId);
        Assert.Equal("Multi-Step Warehouse Scan", tour.Title);
        Assert.Equal(3, tour.Steps.Count);

        // Verify correct ordering 1 -> 2 -> 3
        Assert.Equal(1, tour.Steps[0].StepOrder);
        Assert.Equal("[data-tour='scan-input']", tour.Steps[0].TargetSelector);
        Assert.Equal(TourPlacement.Right, tour.Steps[0].Placement);
        Assert.True(tour.Steps[0].BackdropHighlight);
        Assert.True(tour.Steps[0].RequireInteraction);

        Assert.Equal(2, tour.Steps[1].StepOrder);
        Assert.Equal("[data-tour='bin-location']", tour.Steps[1].TargetSelector);
        Assert.Equal(TourPlacement.Bottom, tour.Steps[1].Placement);

        Assert.Equal(3, tour.Steps[2].StepOrder);
        Assert.Equal("[data-tour='confirm-print']", tour.Steps[2].TargetSelector);
        Assert.Equal(TourPlacement.Left, tour.Steps[2].Placement);
    }

    [Fact]
    public async Task EligibleTourFiltering_ByRouteAndRole_ReturnsOnlyMatchingTours()
    {
        // Arrange: create two distinct tours
        var whatsappTour = await _service.CreateTourAsync(_tenantId, new CreateProductTourRequest(
            Title: "WhatsApp Pairing Flow",
            Description: "Admin setup for WhatsApp",
            TargetRoute: "/channels/whatsapp-pairing",
            TargetRole: "Admin",
            IsActive: true,
            Steps: new List<CreateTourStepRequest>
            {
                new(1, "#qr-box", "Scan QR", "Scan with WhatsApp app", TourPlacement.Right)
            }
        ));

        var dispatchTour = await _service.CreateTourAsync(_tenantId, new CreateProductTourRequest(
            Title: "Dispatcher Run Sheet",
            Description: "Courier dispatch manifest execution",
            TargetRoute: "/courier",
            TargetRole: "CourierDispatcher",
            IsActive: true,
            Steps: new List<CreateTourStepRequest>
            {
                new(1, "#manifest-table", "Manifest Table", "Review assigned drivers", TourPlacement.Top)
            }
        ));

        var userId = "user_admin_test_1";

        // Act 1: Admin at /channels/whatsapp-pairing
        var eligibleAdmin = await _service.GetEligibleToursAsync(_tenantId, userId, "/channels/whatsapp-pairing", "Admin");
        Assert.Contains(eligibleAdmin, t => t.Tour.Id == whatsappTour.Id);
        Assert.DoesNotContain(eligibleAdmin, t => t.Tour.Id == dispatchTour.Id);

        // Act 2: CourierDispatcher at /channels/whatsapp-pairing (wrong role)
        var wrongRole = await _service.GetEligibleToursAsync(_tenantId, userId, "/channels/whatsapp-pairing", "CourierDispatcher");
        Assert.DoesNotContain(wrongRole, t => t.Tour.Id == whatsappTour.Id);

        // Act 3: Admin at /courier (wrong route)
        var wrongRoute = await _service.GetEligibleToursAsync(_tenantId, userId, "/courier", "Admin");
        Assert.DoesNotContain(wrongRoute, t => t.Tour.Id == whatsappTour.Id);

        // Act 4: CourierDispatcher at /courier
        var eligibleDispatcher = await _service.GetEligibleToursAsync(_tenantId, "dispatcher_user", "/courier", "CourierDispatcher");
        Assert.Contains(eligibleDispatcher, t => t.Tour.Id == dispatchTour.Id);
    }

    [Fact]
    public async Task TourProgressTracking_And_CompletionStates()
    {
        // Arrange
        var tour = await _service.CreateTourAsync(_tenantId, new CreateProductTourRequest(
            Title: "Funnel Canvas Builder",
            Description: "Guided canvas navigation",
            TargetRoute: "/funnels/canvas",
            TargetRole: "Merchant",
            IsActive: true,
            Steps: new List<CreateTourStepRequest>
            {
                new(1, "#node-palette", "Palette", "Pick nodes", TourPlacement.Right),
                new(2, "#canvas-grid", "Grid", "Drop nodes", TourPlacement.Bottom),
                new(3, "#publish-button", "Publish", "Ship funnel", TourPlacement.Left)
            }
        ));

        var userId = "merchant_user_42";

        // Act 1: Record start
        var startProgress = await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, userId, "start", 1);
        Assert.Equal(1, startProgress.CurrentStepOrder);
        Assert.Equal(1, startProgress.HighestStepReached);
        Assert.False(startProgress.IsCompleted);
        Assert.False(startProgress.IsSkipped);

        // Act 2: Advance to step 2
        var step2Progress = await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, userId, "step_viewed", 2);
        Assert.Equal(2, step2Progress.CurrentStepOrder);
        Assert.Equal(2, step2Progress.HighestStepReached);

        // Act 3: Complete step 3 and tour
        var completedProgress = await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, userId, "completed", 3);
        Assert.Equal(3, completedProgress.CurrentStepOrder);
        Assert.True(completedProgress.IsCompleted);
        Assert.NotNull(completedProgress.CompletedAtUtc);

        // Act 4: Once completed, GetEligibleToursAsync should no longer return it to the user
        var eligibleAfterCompletion = await _service.GetEligibleToursAsync(_tenantId, userId, "/funnels/canvas", "Merchant");
        Assert.DoesNotContain(eligibleAfterCompletion, t => t.Tour.Id == tour.Id);
    }

    [Fact]
    public async Task TourSkipped_PreventsTourFromReappearing()
    {
        // Arrange
        var tour = await _service.CreateTourAsync(_tenantId, new CreateProductTourRequest(
            Title: "Optional Telemetry Walkthrough",
            Description: "Tour that can be dismissed",
            TargetRoute: "/telemetry",
            TargetRole: "All",
            IsActive: true,
            Steps: new List<CreateTourStepRequest>
            {
                new(1, "#cpu-chart", "CPU Chart", "Inspect CPU", TourPlacement.Top)
            }
        ));

        var userId = "busy_dev_1";

        // Verify initially eligible
        var initial = await _service.GetEligibleToursAsync(_tenantId, userId, "/telemetry", "Admin");
        Assert.Contains(initial, t => t.Tour.Id == tour.Id);

        // Act: User skips tour
        var progress = await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, userId, "skipped", 1);
        Assert.True(progress.IsSkipped);
        Assert.NotNull(progress.SkippedAtUtc);

        // Assert: Tour is no longer eligible
        var afterSkip = await _service.GetEligibleToursAsync(_tenantId, userId, "/telemetry", "Admin");
        Assert.DoesNotContain(afterSkip, t => t.Tour.Id == tour.Id);
    }

    [Fact]
    public async Task AnnouncementTargeting_Role_RouteRegex_And_MinSessionCount()
    {
        // Arrange
        var announcement = await _service.CreateAnnouncementAsync(_tenantId, new CreateAnnouncementRequest(
            Title: "Black Friday Flash Sale Engine Activated!",
            BodyText: "Set up high-velocity flash sale triggers now.",
            CtaText: "Configure Sale",
            CtaUrl: "/funnels/canvas",
            ModalType: AnnouncementModalType.CenteredModal,
            TargetRole: "Merchant",
            TargetRouteRegex: "^/funnels/.*",
            MinSessionCount: 3,
            Priority: 90
        ));

        var userId = "merchant_target_user";

        // Test 1: Session count below minimum (sessionCount = 2, min = 3)
        var lowSession = await _service.GetEligibleAnnouncementsAsync(_tenantId, userId, "/funnels/canvas", "Merchant", sessionCount: 2);
        Assert.DoesNotContain(lowSession, a => a.Id == announcement.Id);

        // Test 2: Role mismatch (sessionCount = 3, role = Admin)
        var wrongRole = await _service.GetEligibleAnnouncementsAsync(_tenantId, userId, "/funnels/canvas", "Admin", sessionCount: 3);
        Assert.DoesNotContain(wrongRole, a => a.Id == announcement.Id);

        // Test 3: Route regex mismatch (sessionCount = 3, route = /channels/whatsapp-pairing)
        var wrongRoute = await _service.GetEligibleAnnouncementsAsync(_tenantId, userId, "/channels/whatsapp-pairing", "Merchant", sessionCount: 3);
        Assert.DoesNotContain(wrongRoute, a => a.Id == announcement.Id);

        // Test 4: All criteria satisfied
        var matched = await _service.GetEligibleAnnouncementsAsync(_tenantId, userId, "/funnels/canvas", "Merchant", sessionCount: 3);
        Assert.Contains(matched, a => a.Id == announcement.Id);
    }

    [Fact]
    public async Task AnnouncementDismissal_IsIdempotent_And_ExcludesFromEligible()
    {
        // Arrange
        var announcement = await _service.CreateAnnouncementAsync(_tenantId, new CreateAnnouncementRequest(
            Title: "System Maintenance Tonight",
            BodyText: "Scheduled maintenance window between 02:00 and 02:30 UTC.",
            CtaText: "Acknowledge",
            CtaUrl: "",
            ModalType: AnnouncementModalType.SlideInToast,
            TargetRole: "All",
            TargetRouteRegex: ".*",
            MinSessionCount: 1,
            Priority: 75
        ));

        var userId = "user_dismissal_test";

        // Eligible before dismissal
        var eligibleBefore = await _service.GetEligibleAnnouncementsAsync(_tenantId, userId, "/dashboard", "Merchant", 1);
        Assert.Contains(eligibleBefore, a => a.Id == announcement.Id);

        // Act 1: Dismiss without CTA click
        var dismissal1 = await _service.DismissAnnouncementAsync(_tenantId, announcement.Id, userId, ctaClicked: false);
        Assert.NotNull(dismissal1);
        Assert.False(dismissal1.CtaClicked);

        // Check it is no longer returned
        var eligibleAfter = await _service.GetEligibleAnnouncementsAsync(_tenantId, userId, "/dashboard", "Merchant", 1);
        Assert.DoesNotContain(eligibleAfter, a => a.Id == announcement.Id);

        // Act 2: Idempotent second dismissal with CTA click true
        var dismissal2 = await _service.DismissAnnouncementAsync(_tenantId, announcement.Id, userId, ctaClicked: true);
        Assert.NotNull(dismissal2);
        Assert.True(dismissal2.CtaClicked);
        Assert.Equal(dismissal1.Id, dismissal2.Id);
    }

    [Fact]
    public async Task CompletionFunnelMetrics_CalculatesAccurateDropoffs_And_Ctr()
    {
        // Arrange: Create a fresh tour with 3 steps
        var tour = await _service.CreateTourAsync(_tenantId, new CreateProductTourRequest(
            Title: "Metrics Analytics Tour",
            Description: "Testing funnel drop-off math",
            TargetRoute: "/analytics/test",
            TargetRole: "All",
            IsActive: true,
            Steps: new List<CreateTourStepRequest>
            {
                new(1, "#step1", "Step One", "Content 1", TourPlacement.Bottom),
                new(2, "#step2", "Step Two", "Content 2", TourPlacement.Bottom),
                new(3, "#step3", "Step Three", "Content 3", TourPlacement.Bottom)
            }
        ));

        // Simulate 4 users:
        // User 1: starts, views 1, 2, 3, completes
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_1", "start", 1);
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_1", "step_viewed", 2);
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_1", "step_viewed", 3);
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_1", "completed", 3);

        // User 2: starts, views 1, 2, 3, completes
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_2", "start", 1);
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_2", "step_viewed", 2);
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_2", "step_viewed", 3);
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_2", "completed", 3);

        // User 3: starts, views 1, views 2, drops off
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_3", "start", 1);
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_3", "step_viewed", 2);

        // User 4: starts, views 1, drops off immediately
        await _service.RecordTourTelemetryAsync(_tenantId, tour.Id, "usr_4", "start", 1);

        // Act: Get metrics
        var metrics = await _service.GetMetricsAsync(_tenantId);

        // Assert
        Assert.NotNull(metrics);
        var tourFunnel = metrics.TourFunnels.FirstOrDefault(f => f.TourId == tour.Id);
        Assert.NotNull(tourFunnel);

        Assert.Equal(4, tourFunnel.TotalStarted);
        Assert.Equal(2, tourFunnel.TotalCompleted);
        Assert.Equal(50.0, tourFunnel.CompletionRatePercent);

        Assert.Equal(3, tourFunnel.StepDropoffs.Count);

        // Step 1: all 4 reached
        Assert.Equal(1, tourFunnel.StepDropoffs[0].StepOrder);
        Assert.Equal(4, tourFunnel.StepDropoffs[0].UsersReached);
        Assert.Equal(100.0, tourFunnel.StepDropoffs[0].RetentionRatePercent);
        Assert.Equal(0.0, tourFunnel.StepDropoffs[0].DropoffRatePercent);

        // Step 2: 3 reached (usr 1, 2, 3)
        Assert.Equal(2, tourFunnel.StepDropoffs[1].StepOrder);
        Assert.Equal(3, tourFunnel.StepDropoffs[1].UsersReached);
        Assert.Equal(75.0, tourFunnel.StepDropoffs[1].RetentionRatePercent);
        Assert.Equal(25.0, tourFunnel.StepDropoffs[1].DropoffRatePercent);

        // Step 3: 2 reached (usr 1, 2)
        Assert.Equal(3, tourFunnel.StepDropoffs[2].StepOrder);
        Assert.Equal(2, tourFunnel.StepDropoffs[2].UsersReached);
        Assert.Equal(50.0, tourFunnel.StepDropoffs[2].RetentionRatePercent);
        Assert.Equal(33.3, tourFunnel.StepDropoffs[2].DropoffRatePercent);
    }

    [Fact]
    public async Task SetTourSteps_UpdatesExistingTour_OrThrowsKeyNotFound()
    {
        // Arrange
        var tour = await _service.CreateTourAsync(_tenantId, new CreateProductTourRequest(
            Title: "Initial Empty Tour",
            Description: "Tour without steps initially",
            TargetRoute: "/dashboard",
            TargetRole: "All"
        ));

        Assert.Empty(tour.Steps);

        // Act: Update steps
        var updated = await _service.SetTourStepsAsync(_tenantId, tour.Id, new List<CreateTourStepRequest>
        {
            new(2, "#b", "Step B", "Content B", TourPlacement.Right),
            new(1, "#a", "Step A", "Content A", TourPlacement.Left)
        });

        // Assert: Sorted by StepOrder
        Assert.Equal(2, updated.Count);
        Assert.Equal(1, updated[0].StepOrder);
        Assert.Equal("#a", updated[0].TargetSelector);
        Assert.Equal(2, updated[1].StepOrder);
        Assert.Equal("#b", updated[1].TargetSelector);

        // Non-existent tour throws
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.SetTourStepsAsync(_tenantId, Guid.NewGuid(), new List<CreateTourStepRequest>()));
    }
}
