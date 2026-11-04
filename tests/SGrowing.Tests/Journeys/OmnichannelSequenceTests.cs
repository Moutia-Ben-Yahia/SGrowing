using FluentAssertions;
using SGrowing.Api.Modules.Journeys.OmnichannelSequence;
using Xunit;

namespace SGrowing.Tests.Journeys;

public class OmnichannelSequenceTests
{
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    [Fact]
    public void ExecuteStep_WhenUnengagedAfterTimeout_EscalatesToFallbackChannel()
    {
        // 14:00 UTC is within business hours (09:00 - 18:00)
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.Zero));
        var engine = new OmnichannelSequenceEngine(timeProvider);

        const string tenantId = "tenant_growth";
        const string cadenceId = "cadence_outreach_1";
        const string recipientId = "rec_lead_001";

        var cadence = new OmnichannelCadenceDefinition(
            CadenceId: cadenceId,
            Name: "High Intent Outbound Sequence",
            Steps:
            [
                new CadenceStepConfig(
                    StepOrder: 1,
                    Channel: ChannelType.WhatsApp,
                    DelayBeforeStep: TimeSpan.Zero,
                    FallbackTimeout: TimeSpan.FromHours(4),
                    TemplateOrScriptId: "tpl_wa_intro"
                )
            ],
            MaxWeeklyTouchpoints: 5
        );
        engine.RegisterCadence(tenantId, cadence);

        var recipient = new CadenceRecipient(
            RecipientId: recipientId,
            Name: "Alice Smith",
            Email: "alice@acme.com",
            PhoneNumber: "+1-555-0101",
            TimeZoneId: "UTC",
            LeadScore: 85,
            WeeklyTouchpointsCount: 1
        );
        engine.RegisterRecipient(tenantId, recipient);

        var request = new ExecuteCadenceStepRequest(
            TenantId: tenantId,
            CadenceId: cadenceId,
            RecipientId: recipientId,
            StepOrder: 1,
            PriorStepEngaged: false,
            ElapsedHoursSincePriorStep: 5 // > 4h FallbackTimeout
        );

        var result = engine.ExecuteStep(request);

        result.Should().NotBeNull();
        result.ActionDispatched.Should().BeTrue();
        result.EscalatedToFallback.Should().BeTrue();
        result.ChannelUsed.Should().Be(ChannelType.Email); // WhatsApp unengaged falls back to Email
        result.SuppressedDueToFatigue.Should().BeFalse();
        result.RecipientId.Should().Be(recipientId);
        result.StatusMessage.Should().Contain("Email");

        var updatedRecipient = engine.GetRecipient(tenantId, recipientId);
        updatedRecipient.Should().NotBeNull();
        updatedRecipient!.WeeklyTouchpointsCount.Should().Be(2);
    }

    [Fact]
    public void ExecuteStep_WhenWeeklyTouchpointsExceedCap_SuppressesOutreachForBuyerFatigue()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.Zero));
        var engine = new OmnichannelSequenceEngine(timeProvider);

        const string tenantId = "tenant_growth";
        const string cadenceId = "cadence_outreach_2";
        const string recipientId = "rec_lead_002";

        var cadence = new OmnichannelCadenceDefinition(
            CadenceId: cadenceId,
            Name: "Strict Frequency Sequence",
            Steps:
            [
                new CadenceStepConfig(
                    StepOrder: 1,
                    Channel: ChannelType.Email,
                    DelayBeforeStep: TimeSpan.Zero,
                    FallbackTimeout: TimeSpan.FromHours(2),
                    TemplateOrScriptId: "tpl_email_followup"
                )
            ],
            MaxWeeklyTouchpoints: 3
        );
        engine.RegisterCadence(tenantId, cadence);

        var recipient = new CadenceRecipient(
            RecipientId: recipientId,
            Name: "Bob Johnson",
            Email: "bob@globex.io",
            PhoneNumber: "+1-555-0102",
            TimeZoneId: "UTC",
            LeadScore: 60,
            WeeklyTouchpointsCount: 3 // Cap already reached
        );
        engine.RegisterRecipient(tenantId, recipient);

        var request = new ExecuteCadenceStepRequest(
            TenantId: tenantId,
            CadenceId: cadenceId,
            RecipientId: recipientId,
            StepOrder: 1,
            PriorStepEngaged: false,
            ElapsedHoursSincePriorStep: 1
        );

        var result = engine.ExecuteStep(request);

        result.Should().NotBeNull();
        result.ActionDispatched.Should().BeFalse();
        result.SuppressedDueToFatigue.Should().BeTrue();
        result.EscalatedToFallback.Should().BeFalse();
        result.StatusMessage.Should().Contain("buyer fatigue protection");

        var storedRecipient = engine.GetRecipient(tenantId, recipientId);
        storedRecipient.Should().NotBeNull();
        storedRecipient!.WeeklyTouchpointsCount.Should().Be(3); // Untouched
    }

    [Fact]
    public void ExecuteStep_WhenPriorStepEngaged_ExecutesStandardNextStepWithoutEscalation()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 11, 0, 0, TimeSpan.Zero));
        var engine = new OmnichannelSequenceEngine(timeProvider);

        const string tenantId = "tenant_growth";
        const string cadenceId = "cadence_outreach_3";
        const string recipientId = "rec_lead_003";

        var cadence = new OmnichannelCadenceDefinition(
            CadenceId: cadenceId,
            Name: "VIP Pipeline Journey",
            Steps:
            [
                new CadenceStepConfig(
                    StepOrder: 1,
                    Channel: ChannelType.WhatsApp,
                    DelayBeforeStep: TimeSpan.Zero,
                    FallbackTimeout: TimeSpan.FromHours(4),
                    TemplateOrScriptId: "tpl_wa_vip"
                )
            ],
            MaxWeeklyTouchpoints: 5
        );
        engine.RegisterCadence(tenantId, cadence);

        var recipient = new CadenceRecipient(
            RecipientId: recipientId,
            Name: "Carol Danvers",
            Email: "carol@marvel.org",
            PhoneNumber: "+1-555-0103",
            TimeZoneId: "UTC",
            LeadScore: 95,
            WeeklyTouchpointsCount: 0
        );
        engine.RegisterRecipient(tenantId, recipient);

        var request = new ExecuteCadenceStepRequest(
            TenantId: tenantId,
            CadenceId: cadenceId,
            RecipientId: recipientId,
            StepOrder: 1,
            PriorStepEngaged: true, // Prior step engaged!
            ElapsedHoursSincePriorStep: 6 // Even if elapsed > timeout, engagement prevents fallback
        );

        var result = engine.ExecuteStep(request);

        result.Should().NotBeNull();
        result.ActionDispatched.Should().BeTrue();
        result.EscalatedToFallback.Should().BeFalse();
        result.ChannelUsed.Should().Be(ChannelType.WhatsApp);
        result.SuppressedDueToFatigue.Should().BeFalse();

        var updatedRecipient = engine.GetRecipient(tenantId, recipientId);
        updatedRecipient.Should().NotBeNull();
        updatedRecipient!.WeeklyTouchpointsCount.Should().Be(1);
    }

    [Fact]
    public void ExecuteStep_OutsideBusinessHours_DefersDispatch()
    {
        // 22:30 UTC is outside business hours (09:00 - 18:00)
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 22, 30, 0, TimeSpan.Zero));
        var engine = new OmnichannelSequenceEngine(timeProvider);

        const string tenantId = "tenant_growth";
        const string cadenceId = "cadence_outreach_4";
        const string recipientId = "rec_lead_004";

        var cadence = new OmnichannelCadenceDefinition(
            CadenceId: cadenceId,
            Name: "Evening Guard Cadence",
            Steps:
            [
                new CadenceStepConfig(
                    StepOrder: 1,
                    Channel: ChannelType.Sms,
                    DelayBeforeStep: TimeSpan.Zero,
                    FallbackTimeout: TimeSpan.FromHours(2),
                    TemplateOrScriptId: "tpl_sms_promo"
                )
            ],
            MaxWeeklyTouchpoints: 5
        );
        engine.RegisterCadence(tenantId, cadence);

        var recipient = new CadenceRecipient(
            RecipientId: recipientId,
            Name: "David Miller",
            Email: "david@enterprise.com",
            PhoneNumber: "+1-555-0104",
            TimeZoneId: "UTC",
            LeadScore: 70,
            WeeklyTouchpointsCount: 0
        );
        engine.RegisterRecipient(tenantId, recipient);

        var request = new ExecuteCadenceStepRequest(
            TenantId: tenantId,
            CadenceId: cadenceId,
            RecipientId: recipientId,
            StepOrder: 1,
            PriorStepEngaged: true,
            ElapsedHoursSincePriorStep: 0
        );

        var result = engine.ExecuteStep(request);

        result.Should().NotBeNull();
        result.ActionDispatched.Should().BeFalse();
        result.SuppressedDueToFatigue.Should().BeFalse();
        result.StatusMessage.Should().Contain("outside business hours");

        var storedRecipient = engine.GetRecipient(tenantId, recipientId);
        storedRecipient.Should().NotBeNull();
        storedRecipient!.WeeklyTouchpointsCount.Should().Be(0); // Not incremented when deferred
    }

    [Fact]
    public void RegisterAndGetCadence_MaintainsThreadSafeTenantConfiguration()
    {
        var engine = new OmnichannelSequenceEngine();
        const int tenantCount = 20;

        Parallel.For(0, tenantCount, i =>
        {
            var tenantId = $"tenant_{i}";
            var cadenceId = $"cadence_{i}";

            var cadence = new OmnichannelCadenceDefinition(
                CadenceId: cadenceId,
                Name: $"Autonomous Cadence #{i}",
                Steps:
                [
                    new CadenceStepConfig(1, ChannelType.WhatsApp, TimeSpan.Zero, TimeSpan.FromHours(4), $"tpl_wa_{i}"),
                    new CadenceStepConfig(2, ChannelType.Email, TimeSpan.FromHours(24), TimeSpan.FromHours(8), $"tpl_email_{i}"),
                    new CadenceStepConfig(3, ChannelType.Sms, TimeSpan.FromHours(48), TimeSpan.FromHours(12), $"tpl_sms_{i}"),
                    new CadenceStepConfig(4, ChannelType.VoiceSdr, TimeSpan.FromHours(72), TimeSpan.FromHours(24), $"script_sdr_{i}")
                ],
                MaxWeeklyTouchpoints: 4
            );

            engine.RegisterCadence(tenantId, cadence);
        });

        Parallel.For(0, tenantCount, i =>
        {
            var tenantId = $"tenant_{i}";
            var cadenceId = $"cadence_{i}";

            var retrieved = engine.GetCadence(tenantId, cadenceId);
            retrieved.Should().NotBeNull();
            retrieved!.CadenceId.Should().Be(cadenceId);
            retrieved.Steps.Should().HaveCount(4);

            // Cross-tenant isolation verification
            var foreignCadence = engine.GetCadence(tenantId, $"cadence_{(i + 1) % tenantCount}");
            foreignCadence.Should().BeNull();
        });
    }

    [Fact]
    public void ExecuteStep_WhenCadenceOrStepOrderNotFound_ReturnsActionNotDispatched()
    {
        var engine = new OmnichannelSequenceEngine();
        const string tenantId = "tenant_test";

        // 1. Cadence not found
        var req1 = new ExecuteCadenceStepRequest(tenantId, "non_existent_cadence", "rec_001", 1, false, 0);
        var res1 = engine.ExecuteStep(req1);
        res1.ActionDispatched.Should().BeFalse();
        res1.StatusMessage.Should().Contain("not found for tenant");

        // 2. Step order not found
        var cadence = new OmnichannelCadenceDefinition(
            CadenceId: "cad_1",
            Name: "Test Cadence",
            Steps: [new CadenceStepConfig(1, ChannelType.Email, TimeSpan.Zero, TimeSpan.FromHours(2), "tpl_1")],
            MaxWeeklyTouchpoints: 5
        );
        engine.RegisterCadence(tenantId, cadence);

        var req2 = new ExecuteCadenceStepRequest(tenantId, "cad_1", "rec_001", 99, false, 0);
        var res2 = engine.ExecuteStep(req2);
        res2.ActionDispatched.Should().BeFalse();
        res2.StatusMessage.Should().Contain("Step order 99 not found");
    }

    [Theory]
    [InlineData(ChannelType.WhatsApp, ChannelType.Email)]
    [InlineData(ChannelType.Email, ChannelType.Sms)]
    [InlineData(ChannelType.Sms, ChannelType.VoiceSdr)]
    [InlineData(ChannelType.VoiceSdr, ChannelType.VoiceSdr)]
    public void EscalateChannel_FullEscalationLadder_FollowsDefinedHierarchy(ChannelType source, ChannelType expected)
    {
        var result = OmnichannelSequenceEngine.EscalateChannel(source);
        result.Should().Be(expected);
    }

    [Fact]
    public void GetDispatchHistory_RecordsDispatchesAndFiltersByTenantAndRecipient()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.Zero));
        var engine = new OmnichannelSequenceEngine(timeProvider);
        const string tenantId = "tenant_history";

        var cadence = new OmnichannelCadenceDefinition(
            CadenceId: "cad_hist",
            Name: "History Test Cadence",
            Steps:
            [
                new CadenceStepConfig(1, ChannelType.WhatsApp, TimeSpan.Zero, TimeSpan.FromHours(4), "tpl_wa"),
                new CadenceStepConfig(2, ChannelType.Email, TimeSpan.Zero, TimeSpan.FromHours(4), "tpl_email")
            ],
            MaxWeeklyTouchpoints: 10
        );
        engine.RegisterCadence(tenantId, cadence);

        // Recipient A: 2 dispatches
        engine.ExecuteStep(new ExecuteCadenceStepRequest(tenantId, "cad_hist", "rec_A", 1, true, 0));
        engine.ExecuteStep(new ExecuteCadenceStepRequest(tenantId, "cad_hist", "rec_A", 2, true, 0));

        // Recipient B: 1 dispatch
        engine.ExecuteStep(new ExecuteCadenceStepRequest(tenantId, "cad_hist", "rec_B", 1, true, 0));

        var allHistory = engine.GetDispatchHistory(tenantId);
        allHistory.Should().HaveCount(3);

        var recAHistory = engine.GetDispatchHistory(tenantId, "rec_A");
        recAHistory.Should().HaveCount(2);
        recAHistory.Select(h => h.RecipientId).Should().AllBe("rec_A");

        var recBHistory = engine.GetDispatchHistory(tenantId, "rec_B");
        recBHistory.Should().HaveCount(1);
        recBHistory[0].RecipientId.Should().Be("rec_B");
    }
}
