using SGrowing.Api.Modules.Channels.WhatsApp;
using Xunit;

namespace SGrowing.Tests.Channels;

public class WhatsAppBroadcastPacingServiceTests
{
    private readonly WhatsAppBroadcastPacingService _service;

    public WhatsAppBroadcastPacingServiceTests()
    {
        _service = new WhatsAppBroadcastPacingService();
        _service.Reset();
    }

    #region 1. Leaky-Bucket Timing & Jitter Randomization Bounds

    [Fact]
    public void CalculatePacingInterval_Calculates_Exact_Base_Interval_When_Jitter_Zero()
    {
        // 60 msgs/min -> 1.0 second interval
        var res60 = _service.CalculatePacingInterval(ratePerMinute: 60, jitterFactor: 0.0);
        Assert.Equal(1.0, res60.BaseIntervalSeconds);
        Assert.Equal(1.0, res60.EffectiveIntervalSeconds);
        Assert.Equal(0.0, res60.JitterFactor);
        Assert.Equal(1.0, res60.CooldownMultiplier);

        // 30 msgs/min -> 2.0 second interval
        var res30 = _service.CalculatePacingInterval(ratePerMinute: 30, jitterFactor: 0.0);
        Assert.Equal(2.0, res30.BaseIntervalSeconds);
        Assert.Equal(2.0, res30.EffectiveIntervalSeconds);

        // 120 msgs/min -> 0.5 second interval
        var res120 = _service.CalculatePacingInterval(ratePerMinute: 120, jitterFactor: 0.0);
        Assert.Equal(0.5, res120.BaseIntervalSeconds);
        Assert.Equal(0.5, res120.EffectiveIntervalSeconds);
    }

    [Fact]
    public void CalculatePacingInterval_Applies_Jitter_Within_Expected_Boundaries()
    {
        double rate = 40.0;
        double baseInterval = 60.0 / rate; // 1.5s
        double jitterRatio = 0.25; // ±25%

        double minExpected = baseInterval * (1.0 - jitterRatio); // 1.125s
        double maxExpected = baseInterval * (1.0 + jitterRatio); // 1.875s

        for (int i = 0; i < 50; i++)
        {
            var res = _service.CalculatePacingInterval(rate, jitterFactor: jitterRatio);
            Assert.True(res.EffectiveIntervalSeconds >= minExpected - 0.001,
                $"Interval {res.EffectiveIntervalSeconds} should be >= min bound {minExpected}");
            Assert.True(res.EffectiveIntervalSeconds <= maxExpected + 0.001,
                $"Interval {res.EffectiveIntervalSeconds} should be <= max bound {maxExpected}");
        }
    }

    [Fact]
    public void CalculatePacingInterval_Throws_ArgumentOutOfRangeException_On_Zero_Or_Negative_Rate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.CalculatePacingInterval(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.CalculatePacingInterval(-15));
    }

    [Fact]
    public void CalculatePacingInterval_Applies_Cooldown_Multiplier_Properly()
    {
        double rate = 60.0; // base 1.0s
        double cooldownMultiplier = 2.5;

        var res = _service.CalculatePacingInterval(rate, jitterFactor: 0.0, cooldownMultiplier: cooldownMultiplier);
        Assert.Equal(1.0, res.BaseIntervalSeconds);
        Assert.Equal(2.5, res.EffectiveIntervalSeconds);
        Assert.Equal(2.5, res.CooldownMultiplier);
        Assert.Equal(TimeSpan.FromSeconds(2.5), res.IntervalDelay);
    }

    [Fact]
    public void CalculateNextDelay_Returns_Positive_TimeSpan_Within_Bounds()
    {
        var delay = _service.CalculateNextDelay(ratePerMinute: 50, jitterFactor: 0.20, cooldownMultiplier: 1.0);
        Assert.True(delay.TotalSeconds > 0.05);
        Assert.True(delay.TotalSeconds < 3.0);
    }

    [Fact]
    public void GeneratePacingSchedule_Returns_Correct_Count_And_Cumulative_Progression()
    {
        int count = 25;
        var schedule = _service.GeneratePacingSchedule(count, ratePerMinute: 30, jitterFactor: 0.15);

        Assert.Equal(count, schedule.Count);
        double lastCumulative = 0.0;
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(i + 1, schedule[i].MessageIndex);
            Assert.True(schedule[i].CumulativeDelaySeconds > lastCumulative);
            Assert.True(schedule[i].IntervalSeconds > 0.5);
            lastCumulative = schedule[i].CumulativeDelaySeconds;
        }
    }

    #endregion

    #region 2. 14-Day Phone Number Warmup Ramp Curve

    [Fact]
    public void WarmupRamp_Day1_Returns_Exact_InitialCap()
    {
        int limitTier1 = _service.CalculateWarmupDailyLimit(day: 1, tier: WhatsAppAccountTier.Tier1_1K, initialCap: 50);
        int limitTier2 = _service.CalculateWarmupDailyLimit(day: 1, tier: WhatsAppAccountTier.Tier2_10K, initialCap: 100);

        Assert.Equal(50, limitTier1);
        Assert.Equal(100, limitTier2);
    }

    [Fact]
    public void WarmupRamp_Day2_To_Day5_Follows_Exponential_Ramp_Curve()
    {
        // Formula: 50 * (1.4 ^ (day - 1))
        // Day 1: 50 * 1.4^0 = 50
        // Day 2: 50 * 1.4^1 = 70
        // Day 3: 50 * 1.4^2 = 98
        // Day 4: 50 * 1.4^3 = 137.2 -> 137
        // Day 5: 50 * 1.4^4 = 192.08 -> 192

        Assert.Equal(70, _service.CalculateWarmupDailyLimit(2, WhatsAppAccountTier.Tier2_10K, 50));
        Assert.Equal(98, _service.CalculateWarmupDailyLimit(3, WhatsAppAccountTier.Tier2_10K, 50));
        Assert.Equal(137, _service.CalculateWarmupDailyLimit(4, WhatsAppAccountTier.Tier2_10K, 50));
        Assert.Equal(192, _service.CalculateWarmupDailyLimit(5, WhatsAppAccountTier.Tier2_10K, 50));
    }

    [Fact]
    public void WarmupRamp_Day14_Limit_Calculates_Accurately_For_Tier2()
    {
        // Day 14: 50 * 1.4^13 = 3968.57 -> 3969. Tier 2 cap is 10,000, so result is 3969.
        int day14Limit = _service.CalculateWarmupDailyLimit(14, WhatsAppAccountTier.Tier2_10K, 50);
        Assert.Equal(3969, day14Limit);
    }

    [Fact]
    public void WarmupRamp_Enforces_Tier1_1K_Cap()
    {
        // On Day 10: 50 * 1.4^9 = 1033.05. Tier 1 has 1,000 max. Should cap at 1,000.
        int day10Limit = _service.CalculateWarmupDailyLimit(10, WhatsAppAccountTier.Tier1_1K, 50);
        Assert.Equal(1000, day10Limit);

        // Day 14 also caps at 1000
        int day14Limit = _service.CalculateWarmupDailyLimit(14, WhatsAppAccountTier.Tier1_1K, 50);
        Assert.Equal(1000, day14Limit);
    }

    [Fact]
    public void WarmupRamp_Enforces_Tier2_10K_Cap()
    {
        // With higher initialCap = 200, Day 14: 200 * 1.4^13 = 15899 -> capped at 10,000 for Tier 2
        int capped = _service.CalculateWarmupDailyLimit(14, WhatsAppAccountTier.Tier2_10K, initialCap: 200);
        Assert.Equal(10000, capped);
    }

    [Fact]
    public void WarmupRamp_Day_Zero_Or_Negative_Normalizes_To_Day_1()
    {
        int day0 = _service.CalculateWarmupDailyLimit(0, WhatsAppAccountTier.Tier1_1K, 50);
        int dayNeg = _service.CalculateWarmupDailyLimit(-3, WhatsAppAccountTier.Tier1_1K, 50);

        Assert.Equal(50, day0);
        Assert.Equal(50, dayNeg);
    }

    [Fact]
    public void WarmupRamp_Tier_Unlimited_Grows_Without_Low_Tier_Capping()
    {
        int unlimited14 = _service.CalculateWarmupDailyLimit(14, WhatsAppAccountTier.Unlimited, initialCap: 200);
        Assert.Equal(15874, unlimited14); // Not capped at 1K or 10K
    }

    [Fact]
    public void WarmupTrajectory_Returns_Complete_14_Day_Trajectory_With_Safe_Rates()
    {
        var trajectory = _service.GetWarmupTrajectory("test-phone", WhatsAppAccountTier.Tier1_1K, 50);

        Assert.Equal(14, trajectory.Count);
        for (int i = 0; i < 14; i++)
        {
            var day = trajectory[i];
            Assert.Equal(i + 1, day.Day);
            Assert.True(day.DailyLimit >= 50);
            Assert.True(day.RecommendedBatchSize >= 25);
            Assert.True(day.SafeRatePerMinute >= 10.0);
            Assert.False(string.IsNullOrWhiteSpace(day.PhaseDescription));
        }
    }

    [Fact]
    public void WarmupStatus_AutoRegisters_Or_Returns_State()
    {
        var state = _service.GetWarmupStatus("new-device-id");
        Assert.NotNull(state);
        Assert.Equal("new-device-id", state.PhoneNumberId);
        Assert.Equal(1, state.CurrentDay);
        Assert.Equal(0, state.TodaySentCount);
        Assert.Equal(state.DailyLimit, state.RemainingToday);
    }

    [Fact]
    public void WarmupStatus_RecordSentMessage_Updates_Sent_And_Remaining_Counts()
    {
        var phoneId = "counter-phone-1";
        _service.RegisterOrUpdatePhoneNumber(phoneId, "+21699000111", WhatsAppAccountTier.Tier1_1K, overrideDay: 1);

        var ok1 = _service.RecordSentMessage(phoneId, 15);
        Assert.True(ok1);

        var state = _service.GetWarmupStatus(phoneId);
        Assert.Equal(15, state.TodaySentCount);
        Assert.Equal(35, state.RemainingToday); // 50 - 15 = 35

        _service.RecordSentMessage(phoneId, 20);
        var state2 = _service.GetWarmupStatus(phoneId);
        Assert.Equal(35, state2.TodaySentCount);
        Assert.Equal(15, state2.RemainingToday);
    }

    [Fact]
    public void WarmupStatus_RegisterOrUpdatePhoneNumber_Overrides_Day_And_Tier()
    {
        var phoneId = "override-phone";
        var state = _service.RegisterOrUpdatePhoneNumber(
            phoneId,
            "+14155552671",
            WhatsAppAccountTier.Tier2_10K,
            overrideDay: 7);

        Assert.Equal(7, state.CurrentDay);
        Assert.Equal(WhatsAppAccountTier.Tier2_10K, state.AccountTier);
        Assert.Equal("+14155552671", state.PhoneNumber);
        Assert.Equal(_service.CalculateWarmupDailyLimit(7, WhatsAppAccountTier.Tier2_10K), state.DailyLimit);
    }

    #endregion

    #region 3. Multi-Lingual Opt-Out Keyword Detection & Suppression

    [Theory]
    [InlineData("STOP", "STOP")]
    [InlineData("stop", "STOP")]
    [InlineData("Please STOP sending me messages", "STOP")]
    [InlineData("UNSUBSCRIBE", "UNSUBSCRIBE")]
    [InlineData("unsubscribe now", "UNSUBSCRIBE")]
    [InlineData("CANCEL", "CANCEL")]
    [InlineData("QUIT", "QUIT")]
    [InlineData("PAUSE", "PAUSE")]
    [InlineData("OPT OUT", "OPT OUT")]
    public void OptOutInterceptor_Detects_English_Keywords(string text, string expectedKeyword)
    {
        var phone = "+15551112233";
        var result = _service.InterceptInboundMessage(phone, text);

        Assert.True(result.IsOptOut);
        Assert.Equal(expectedKeyword, result.DetectedKeyword);
        Assert.Equal("SuppressedAndBlockedFutureBroadcasts", result.ActionTaken);
        Assert.True(_service.IsSuppressed(phone));
    }

    [Theory]
    [InlineData("ARRETER", "ARRETER")]
    [InlineData("Arrêter", "ARRÊTER")]
    [InlineData("Veuillez arreter svp", "ARRETER")]
    [InlineData("DESABONNER", "DESABONNER")]
    [InlineData("d\u00e9sabonner", "DÉSABONNER")]
    [InlineData("QUITTER", "QUITTER")]
    [InlineData("ANNULER", "ANNULER")]
    public void OptOutInterceptor_Detects_French_Keywords(string text, string expectedKeyword)
    {
        var phone = "+33699887766";
        var result = _service.InterceptInboundMessage(phone, text);

        Assert.True(result.IsOptOut);
        Assert.Equal(expectedKeyword, result.DetectedKeyword);
        Assert.True(_service.IsSuppressed(phone));
    }

    [Theory]
    [InlineData("DESINSCRIBIR", "DESINSCRIBIR")]
    [InlineData("desinscribirse por favor", "DESINSCRIBIRSE")]
    [InlineData("CANCELAR", "CANCELAR")]
    [InlineData("BAJA", "BAJA")]
    [InlineData("PARAR", "PARAR")]
    [InlineData("ALTO", "ALTO")]
    public void OptOutInterceptor_Detects_Spanish_Keywords(string text, string expectedKeyword)
    {
        var phone = "+34612345678";
        var result = _service.InterceptInboundMessage(phone, text);

        Assert.True(result.IsOptOut);
        Assert.Equal(expectedKeyword, result.DetectedKeyword);
        Assert.True(_service.IsSuppressed(phone));
    }

    [Theory]
    [InlineData("توقف", "توقف")]
    [InlineData("قف", "قف")]
    [InlineData("الغاء", "الغاء")]
    [InlineData("إلغاء", "إلغاء")]
    [InlineData("إيقاف", "إيقاف")]
    [InlineData("إيقاف الاشتراك", "إيقاف الاشتراك")]
    public void OptOutInterceptor_Detects_Arabic_Keywords(string text, string expectedKeyword)
    {
        var phone = "+21698765432";
        var result = _service.InterceptInboundMessage(phone, text);

        Assert.True(result.IsOptOut);
        Assert.Equal(expectedKeyword, result.DetectedKeyword);
        Assert.True(_service.IsSuppressed(phone));
    }

    [Fact]
    public void OptOutInterceptor_Handles_Punctuation_And_Mixed_Casing()
    {
        var phone = "+19998887777";
        var result = _service.InterceptInboundMessage(phone, "  !STOP!! ... ");

        Assert.True(result.IsOptOut);
        Assert.Equal("STOP", result.DetectedKeyword);
        Assert.True(_service.IsSuppressed(phone));
    }

    [Fact]
    public void OptOutInterceptor_Ignores_Ordinary_Inbound_Messages()
    {
        var phone = "+21622334455";
        var result = _service.InterceptInboundMessage(phone, "Hello, can you send me the tracking link?");

        Assert.False(result.IsOptOut);
        Assert.Null(result.DetectedKeyword);
        Assert.Equal("PassedThrough", result.ActionTaken);
        Assert.False(_service.IsSuppressed(phone));
    }

    [Fact]
    public void SuppressionLedger_Manual_Add_Remove_And_Search_Filtering()
    {
        var phone = "+447700900077";
        Assert.False(_service.IsSuppressed(phone));

        _service.AddToSuppression(phone, "Manual GDPR right to be forgotten", source: "SupportAgent");
        Assert.True(_service.IsSuppressed(phone));

        var list = _service.GetSuppressionList(search: "GDPR");
        Assert.Contains(list, x => x.PhoneNumber == phone);

        var removed = _service.RemoveFromSuppression(phone);
        Assert.True(removed);
        Assert.False(_service.IsSuppressed(phone));
    }

    #endregion

    #region 4. Ban Risk Score Sensitivity & Automated Cooldown Multipliers

    [Fact]
    public void BanRiskIndex_Low_When_Metrics_Healthy()
    {
        var report = _service.EvaluateBanRisk(
            phoneNumberId: "healthy-phone",
            blockRate: 0.002, // 0.2%
            complaintRate: 0.0005, // 0.05%
            templateFailureRate: 0.01, // 1%
            currentVelocityRatePerMin: 15.0,
            safeVelocityRatePerMin: 25.0);

        Assert.True(report.BanRiskIndex <= 40.0, $"Expected Low risk, got {report.BanRiskIndex}");
        Assert.Equal(BanRiskLevel.Low, report.RiskLevel);
        Assert.Equal(1.0, report.CooldownMultiplier);
        Assert.False(report.IsEmergencyPaused);
    }

    [Fact]
    public void BanRiskIndex_Elevated_Triggers_Cooldown_Multiplier_When_Over_65_Percent()
    {
        // Block rate 2.8% (high), Complaint rate 1.1% (high), Velocity 45 vs safe 25
        var report = _service.EvaluateBanRisk(
            phoneNumberId: "elevated-phone",
            blockRate: 0.028,
            complaintRate: 0.011,
            templateFailureRate: 0.04,
            currentVelocityRatePerMin: 45.0,
            safeVelocityRatePerMin: 25.0);

        Assert.True(report.BanRiskIndex > 65.0, $"Expected > 65% risk score, got {report.BanRiskIndex}");
        Assert.True(report.BanRiskIndex <= 85.0);
        Assert.Equal(BanRiskLevel.Elevated, report.RiskLevel);
        Assert.Equal(2.0, report.CooldownMultiplier); // automated cooldown multiplier active!
        Assert.False(report.IsEmergencyPaused);
    }

    [Fact]
    public void BanRiskIndex_Critical_Triggers_Emergency_Pause_When_Over_85_Percent()
    {
        // Severe conditions: Block rate 5.5% (critical), Complaint rate 2.0% (critical)
        var report = _service.EvaluateBanRisk(
            phoneNumberId: "critical-phone",
            blockRate: 0.055,
            complaintRate: 0.020,
            templateFailureRate: 0.10,
            currentVelocityRatePerMin: 60.0,
            safeVelocityRatePerMin: 25.0);

        Assert.True(report.BanRiskIndex > 85.0, $"Expected > 85% risk score, got {report.BanRiskIndex}");
        Assert.Equal(BanRiskLevel.Critical, report.RiskLevel);
        Assert.True(report.IsEmergencyPaused); // automated emergency pause active!
        Assert.Equal(3.0, report.CooldownMultiplier);
    }

    [Fact]
    public void BanRiskIndex_Sensitivity_To_Velocity_Overshoot()
    {
        // Identical quality rates, differing velocity
        var normalVel = _service.EvaluateBanRisk("phone-vel-normal", 0.01, 0.003, 0.02, currentVelocityRatePerMin: 20.0, safeVelocityRatePerMin: 20.0);
        var highVel = _service.EvaluateBanRisk("phone-vel-high", 0.01, 0.003, 0.02, currentVelocityRatePerMin: 60.0, safeVelocityRatePerMin: 20.0);

        Assert.True(highVel.BanRiskIndex > normalVel.BanRiskIndex);
    }

    [Fact]
    public void BanRisk_AdjustCooldown_Manual_Override()
    {
        var phoneId = "manual-adjust-phone";
        _service.EvaluateBanRisk(phoneId, 0.001, 0.0001, 0.01, 15.0);

        var adjusted = _service.AdjustCooldown(phoneId, cooldownMultiplier: 2.2, emergencyPause: false, reason: "Operator precautionary buffer");
        Assert.Equal(2.2, adjusted.CooldownMultiplier);
        Assert.False(adjusted.IsEmergencyPaused);
        Assert.Contains("Operator precautionary buffer", adjusted.DiagnosticSummary);

        var paused = _service.AdjustCooldown(phoneId, cooldownMultiplier: 3.0, emergencyPause: true, reason: "Manual maintenance freeze");
        Assert.True(paused.IsEmergencyPaused);
    }

    #endregion

    #region 5. Campaign Simulation, Batching & Scheduling

    [Fact]
    public void CampaignSimulation_Slices_Into_Appropriate_Batches_With_Pauses()
    {
        var req = new CampaignSimulationRequest
        {
            PhoneNumberId = "default-phone",
            TotalRecipients = 180,
            RatePerMinute = 30, // 2s per msg
            WarmupDay = 5,      // batch size 60, pause 60s
            JitterPercentage = 20
        };

        var sim = _service.SimulateCampaign(req);

        Assert.Equal(180, sim.TotalRecipients);
        Assert.Equal(180, sim.EligibleRecipients);
        Assert.Equal(60, sim.BatchSize);
        Assert.Equal(3, sim.BatchCount); // 180 / 60 = 3 batches
        Assert.Equal(3, sim.BatchPlan.Count);
        Assert.Equal(60, sim.InterBatchPauseSeconds);

        Assert.Equal(60, sim.BatchPlan[0].MessageCount);
        Assert.Equal(60, sim.BatchPlan[0].CooldownPauseAfterSeconds);
        Assert.Equal(60, sim.BatchPlan[1].MessageCount);
        Assert.Equal(60, sim.BatchPlan[1].CooldownPauseAfterSeconds);
        Assert.Equal(60, sim.BatchPlan[2].MessageCount);
        Assert.Equal(0, sim.BatchPlan[2].CooldownPauseAfterSeconds); // last batch has no pause

        Assert.True(sim.EstimatedDurationMinutes > 0);
        Assert.True(sim.EstimatedDeliveryEndUtc > sim.EstimatedDeliveryStartUtc);
    }

    [Fact]
    public void CampaignSimulation_Filters_Suppressed_Contacts_And_Gives_Safety_Recommendations()
    {
        var suppressedNum = "+21655443322";
        _service.AddToSuppression(suppressedNum, "Opt-Out keyword", "STOP");

        var recipients = new List<string>
        {
            "+21698111222",
            "+21698222333",
            suppressedNum, // suppressed!
            "+21698333444"
        };

        var req = new CampaignSimulationRequest
        {
            PhoneNumberId = "default-phone",
            RecipientPhoneNumbers = recipients,
            RatePerMinute = 15,
            WarmupDay = 1, // day 1 limit is 50
            JitterPercentage = 10
        };

        var sim = _service.SimulateCampaign(req);

        Assert.Equal(4, sim.TotalRecipients);
        Assert.Equal(1, sim.SuppressedRecipientsCount);
        Assert.Equal(3, sim.EligibleRecipients);
        Assert.Contains(sim.SafetyMarginRecommendations, r => r.Contains("suppression ledger"));
    }

    [Fact]
    public void CampaignSimulation_Detects_Volume_Exceeding_Warmup_Limit()
    {
        var req = new CampaignSimulationRequest
        {
            PhoneNumberId = "default-phone",
            TotalRecipients = 500,
            RatePerMinute = 30,
            WarmupDay = 2, // day 2 limit is 70
            AccountTier = WhatsAppAccountTier.Tier1_1K
        };

        var sim = _service.SimulateCampaign(req);

        Assert.True(sim.RequiresMultiDaySplitting);
        Assert.Contains(sim.SafetyMarginRecommendations, r => r.Contains("exceeds Day 2 daily capacity limit"));
    }

    [Fact]
    public void ScheduleBroadcast_Blocks_When_Emergency_Paused()
    {
        var phoneId = "paused-sender";
        _service.AdjustCooldown(phoneId, 3.0, emergencyPause: true, reason: "Meta template rejection surge");

        var req = new ScheduleBroadcastRequest
        {
            CampaignId = "camp-emergency-block",
            PhoneNumberId = phoneId,
            CampaignName = "Emergency Promo",
            Recipients = new List<string> { "+21698111222", "+21698222333" },
            RatePerMinute = 20
        };

        var res = _service.ScheduleBroadcast(req);

        Assert.True(res.IsEmergencyPaused);
        Assert.Equal(0, res.EligibleRecipients);
        Assert.Equal("BlockedByEmergencyPause", res.Status);
    }

    [Fact]
    public void ScheduleBroadcast_Schedules_Eligible_Recipients_And_Excludes_Suppressed()
    {
        var phoneId = "active-sender-ok";
        var suppressed = "+21699999999";
        _service.AddToSuppression(suppressed, "Opt-Out STOP");

        var req = new ScheduleBroadcastRequest
        {
            CampaignId = "camp-clean-schedule",
            PhoneNumberId = phoneId,
            CampaignName = "VIP Flash Sale",
            Recipients = new List<string> { "+21698111222", "+21698222333", suppressed },
            RatePerMinute = 20
        };

        var res = _service.ScheduleBroadcast(req);

        Assert.False(res.IsEmergencyPaused);
        Assert.Equal(3, res.TotalRecipients);
        Assert.Equal(2, res.EligibleRecipients);
        Assert.Equal(1, res.SuppressedFiltered);
        Assert.Contains("Scheduled", res.Status);
        Assert.Contains("suppressed contacts safely excluded", res.Message);
    }

    #endregion
}
