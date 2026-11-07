using System;
using System.Linq;
using System.Threading.Tasks;
using SGrowing.Api.Common;
using SGrowing.Api.Modules.Outbound.LinkedInCadence;
using Xunit;

namespace SGrowing.Tests.Unit;

public class LinkedInCadenceTests
{
    private readonly LinkedInCadenceService _service = new();

    [Fact]
    public async Task Service_InitializesDefaultTenant_WithGccEnterpriseSaasCadence()
    {
        // Act
        var cadences = await _service.GetCadencesAsync(LinkedInCadenceService.DefaultTenantId);

        // Assert
        Assert.NotEmpty(cadences);
        var cadence = cadences.First(c => c.CadenceId == LinkedInCadenceService.DefaultCadenceId);
        Assert.Equal("GCC Enterprise SaaS Decision-Makers (KSA & UAE)", cadence.Name);
        Assert.Equal("Enterprise CTOs, CISOs & Heads of Digital Transformation", cadence.TargetPersona);
        Assert.True(cadence.IsActive);
        Assert.Equal(40, cadence.DailyActionLimit);
        Assert.True(cadence.ActiveProspectCount >= 5);
        Assert.True(cadence.Steps.Count >= 10);
    }

    [Fact]
    public async Task GetCadences_StrictTenantIsolation_ReturnsEmptyForNewTenant()
    {
        // Arrange
        var isolatedTenantId = Guid.NewGuid();

        // Act
        var cadences = await _service.GetCadencesAsync(isolatedTenantId);

        // Assert
        Assert.Empty(cadences);
    }

    [Fact]
    public async Task CreateCadence_AddsNewCadence_IsolatedToTenant()
    {
        // Arrange
        var isolatedTenantId = Guid.NewGuid();
        var request = new CreateCadenceRequest(
            TenantId: isolatedTenantId,
            Name: "Dubai FinTech Founders Sequence",
            TargetPersona: "FinTech Co-Founders & Managing Directors",
            Description: "Targeted outbound cadence for emerging payments and wealth-tech startups in UAE.",
            DailyActionLimit: 25,
            InitialSteps: new[]
            {
                new CreateCadenceStepDto(1, LinkedInTouchType.ProfileView, "Warm view from DIFC hub", 60),
                new CreateCadenceStepDto(3, LinkedInTouchType.ConnectionRequest, "FinTech founder connection note", 120),
                new CreateCadenceStepDto(5, LinkedInTouchType.PersonalizedFollowUp, "Growth case study benchmark", 150)
            }
        );

        // Act
        var created = await _service.CreateCadenceAsync(request);
        var tenantCadences = await _service.GetCadencesAsync(isolatedTenantId);
        var defaultTenantCadences = await _service.GetCadencesAsync(LinkedInCadenceService.DefaultTenantId);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(isolatedTenantId, created.TenantId);
        Assert.Equal(25, created.DailyActionLimit);
        Assert.Equal(3, created.Steps.Count);

        // Verify isolation
        Assert.Single(tenantCadences);
        Assert.Equal(created.CadenceId, tenantCadences[0].CadenceId);
        Assert.DoesNotContain(defaultTenantCadences, c => c.CadenceId == created.CadenceId);
    }

    [Fact]
    public async Task GetCadenceSteps_ReturnsSeededMultiTouchSteps_OrderedCorrectly()
    {
        // Act
        var steps = await _service.GetCadenceStepsAsync(
            LinkedInCadenceService.DefaultTenantId,
            LinkedInCadenceService.DefaultCadenceId);

        // Assert
        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.TouchType == LinkedInTouchType.ProfileView);
        Assert.Contains(steps, s => s.TouchType == LinkedInTouchType.PostEngagement);
        Assert.Contains(steps, s => s.TouchType == LinkedInTouchType.ConnectionRequest);
        Assert.Contains(steps, s => s.TouchType == LinkedInTouchType.PersonalizedFollowUp);
        Assert.Contains(steps, s => s.TouchType == LinkedInTouchType.InMailMessage);

        // Verify sorted by DayOffset ascending
        var dayOffsets = steps.Select(s => s.DayOffset).ToList();
        var sortedDayOffsets = dayOffsets.OrderBy(d => d).ToList();
        Assert.Equal(sortedDayOffsets, dayOffsets);
    }

    [Fact]
    public async Task ExecuteStep_UpdatesStatusToSent_RecordsTimestampAndIncrementsDailyQuota()
    {
        // Arrange
        var targetStepId = Guid.Parse("44444444-4444-4444-4444-444444444406"); // Noura Al-Zahrani ConnectionRequest
        var initialOverview = await _service.GetOverviewReportAsync(LinkedInCadenceService.DefaultTenantId);
        var initialQuotaUsed = initialOverview.DailyQuotaUsed;

        var request = new ExecuteCadenceStepRequest(
            TenantId: LinkedInCadenceService.DefaultTenantId,
            CadenceId: LinkedInCadenceService.DefaultCadenceId,
            StepId: targetStepId,
            ApprovedContent: "Noura, your work scaling SABIC distributed event brokers is remarkable. Sharing our GCC industrial data mesh benchmark.",
            MarkAsReplied: false
        );

        // Act
        var executedStep = await _service.ExecuteStepAsync(request);
        var updatedOverview = await _service.GetOverviewReportAsync(LinkedInCadenceService.DefaultTenantId);

        // Assert
        Assert.NotNull(executedStep);
        Assert.Equal(targetStepId, executedStep.StepId);
        Assert.Equal(CadenceStepStatus.Sent, executedStep.Status);
        Assert.NotNull(executedStep.ExecutionTimestampUtc);
        Assert.Equal(request.ApprovedContent, executedStep.CustomizedContent);

        // Verify quota increment
        Assert.Equal(initialQuotaUsed + 1, updatedOverview.DailyQuotaUsed);
    }

    [Fact]
    public async Task ExecuteStep_MarkAsReplied_UpdatesStatusToRepliedAndRecordsResponse()
    {
        // Arrange
        var targetStepId = Guid.Parse("44444444-4444-4444-4444-444444444409"); // Faisal Bin Rashid ConnectionRequest

        var request = new ExecuteCadenceStepRequest(
            TenantId: LinkedInCadenceService.DefaultTenantId,
            CadenceId: LinkedInCadenceService.DefaultCadenceId,
            StepId: targetStepId,
            ApprovedContent: "Custom invitation note for DIFC FinTech CTO.",
            MarkAsReplied: true
        );

        // Act
        var updatedStep = await _service.ExecuteStepAsync(request);

        // Assert
        Assert.NotNull(updatedStep);
        Assert.Equal(CadenceStepStatus.Replied, updatedStep.Status);
        Assert.NotNull(updatedStep.ResponseReceived);
        Assert.Contains("replied", updatedStep.ResponseReceived, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteStep_WhenDailyQuotaExceeded_ThrowsInvalidOperationException()
    {
        // Arrange
        var testService = new LinkedInCadenceService();
        var tenantId = Guid.NewGuid();

        // Create a cadence with daily quota limit of 2
        var cadence = await testService.CreateCadenceAsync(new CreateCadenceRequest(
            TenantId: tenantId,
            Name: "Strict Pacing Cadence",
            TargetPersona: "CTO",
            Description: "Low limit cadence",
            DailyActionLimit: 2,
            InitialSteps: new[]
            {
                new CreateCadenceStepDto(1, LinkedInTouchType.ProfileView, "Step 1", 30),
                new CreateCadenceStepDto(2, LinkedInTouchType.PostEngagement, "Step 2", 30),
                new CreateCadenceStepDto(3, LinkedInTouchType.ConnectionRequest, "Step 3", 30)
            }
        ));

        var steps = await testService.GetCadenceStepsAsync(tenantId, cadence.CadenceId);
        Assert.True(steps.Count >= 3);

        // Execute action 1 (Quota: 0 -> 1)
        await testService.ExecuteStepAsync(new ExecuteCadenceStepRequest(tenantId, cadence.CadenceId, steps[0].StepId));
        // Execute action 2 (Quota: 1 -> 2)
        await testService.ExecuteStepAsync(new ExecuteCadenceStepRequest(tenantId, cadence.CadenceId, steps[1].StepId));

        // Act & Assert: Action 3 should exceed limit and throw
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            testService.ExecuteStepAsync(new ExecuteCadenceStepRequest(tenantId, cadence.CadenceId, steps[2].StepId)));

        Assert.Contains("Daily anti-bot pacing limit reached", ex.Message);
    }

    [Theory]
    [InlineData("en", "Saudi Telecom Cloud Services", "Tariq Al-Mansoor", ProspectSeniority.VP)]
    [InlineData("ar", "أرامكو الرقمية", "سارة العتيبي", ProspectSeniority.Director)]
    public async Task DraftAiMessage_GeneratesBilingualPersonalizedTouchWithAppropriatePacing(
        string lang,
        string company,
        string prospectName,
        ProspectSeniority seniority)
    {
        // Arrange
        var request = new DraftMessageAiRequest(
            TenantId: LinkedInCadenceService.DefaultTenantId,
            TouchType: LinkedInTouchType.ConnectionRequest,
            ProspectName: prospectName,
            CompanyName: company,
            JobTitle: seniority == ProspectSeniority.VP ? "VP of Digital Infrastructure" : "Director of Cloud Systems",
            Seniority: seniority,
            Industry: "Cloud & Telecom Infrastructure",
            KeyPainPoint: lang == "ar" ? "سيادة البيانات والامتثال لنظام حماية البيانات الشخصية" : "data sovereignty and PDPL compliance",
            Language: lang
        );

        // Act
        var draft = await _service.DraftAiMessageAsync(request);

        // Assert
        Assert.NotNull(draft);
        Assert.Equal(LinkedInTouchType.ConnectionRequest, draft.TouchType);
        Assert.True(draft.ConfidenceScore >= 0.90);
        Assert.True(draft.PacingWaitMinutes >= 15);
        Assert.NotEmpty(draft.RecommendedHashtags);

        if (lang == "ar")
        {
            Assert.Contains("السلام عليكم", draft.DraftedMessage);
            Assert.Contains(prospectName, draft.DraftedMessage);
            Assert.Contains("رؤية 2030", draft.PersonalizationHook);
        }
        else
        {
            Assert.Contains("Hi", draft.DraftedMessage);
            Assert.Contains(prospectName, draft.DraftedMessage);
            Assert.Contains("GCC", draft.DraftedMessage);
        }
    }

    [Fact]
    public async Task OverviewReport_AggregatesKpisAndPacingQueueHealthCorrectly()
    {
        // Act
        var overview = await _service.GetOverviewReportAsync(LinkedInCadenceService.DefaultTenantId);

        // Assert
        Assert.NotNull(overview);
        Assert.Equal(LinkedInCadenceService.DefaultTenantId, overview.TenantId);
        Assert.True(overview.TotalCadences >= 1);
        Assert.True(overview.ActiveProspectsCount >= 5);
        Assert.True(overview.TotalStepsExecuted >= 5);
        Assert.True(overview.PendingApprovalCount >= 2);
        Assert.True(overview.AvgConnectionRatePercent > 0);
        Assert.True(overview.AvgReplyRatePercent > 0);
        Assert.Equal(40, overview.DailyQuotaLimit);
        Assert.Contains("Optimal", overview.PacingQueueStatus);
        Assert.NotEmpty(overview.RecentSteps);
    }

    [Fact]
    public void EndpointsModule_ImplementsIEndpointModule_ParameterlessConstructor()
    {
        var moduleType = typeof(LinkedInCadenceEndpoints);
        Assert.True(typeof(IEndpointModule).IsAssignableFrom(moduleType));
        var instance = Activator.CreateInstance(moduleType) as IEndpointModule;
        Assert.NotNull(instance);
    }
}
