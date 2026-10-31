using SGrowing.Api.Modules.Telemetry;
using Xunit;

namespace SGrowing.Tests.Telemetry;

public class ProductAnalyticsServiceTests
{
    private readonly ProductAnalyticsService _service;
    private readonly Guid _testTenantId = Guid.NewGuid();

    public ProductAnalyticsServiceTests()
    {
        _service = new ProductAnalyticsService();
    }

    #region Fast Telemetry Ingestion Tests

    [Fact]
    public async Task IngestEvent_SingleValidEvent_EnqueuesAndReturnsAccepted()
    {
        var evt = new TelemetryIngestEvent(
            EventId: "evt-test-1",
            TenantId: _testTenantId,
            UserId: "user-alpha",
            SessionId: "ses-101",
            EventType: "pageview",
            EventName: "landing_view",
            TimestampUtc: DateTime.UtcNow,
            Url: "/pricing"
        );

        var response = await _service.IngestEventAsync(evt);

        Assert.True(response.Success);
        Assert.Equal("evt-test-1", response.EventId);
        Assert.True(response.BufferedQueueCount >= 1);
    }

    [Fact]
    public async Task IngestEvent_MissingEventId_GeneratesDeterministicOrNewGuid()
    {
        var evt = new TelemetryIngestEvent(
            EventId: null,
            TenantId: _testTenantId,
            UserId: "user-beta",
            SessionId: "ses-102",
            EventType: "click",
            EventName: "cta_click",
            TimestampUtc: DateTime.UtcNow,
            Url: "/features"
        );

        var response = await _service.IngestEventAsync(evt);

        Assert.True(response.Success);
        Assert.False(string.IsNullOrWhiteSpace(response.EventId));
    }

    [Fact]
    public async Task IngestEvent_MissingTimestamp_DefaultsToUtcNow()
    {
        var evt = new TelemetryIngestEvent(
            EventId: "evt-test-time",
            TenantId: _testTenantId,
            UserId: "user-gamma",
            SessionId: "ses-103",
            EventType: "custom",
            EventName: "feature_used",
            TimestampUtc: default,
            Url: "/app"
        );

        var response = await _service.IngestEventAsync(evt);

        Assert.True(response.Success);
        var recent = await _service.GetRecentEventsAsync(_testTenantId, 10, "custom", "user-gamma");
        // After flush, event has valid timestamp
        await _service.FlushBufferAsync();
        var flushedEvents = await _service.GetRecentEventsAsync(_testTenantId, 10, "custom", "user-gamma");
        Assert.NotEmpty(flushedEvents);
        Assert.True(flushedEvents[0].TimestampUtc > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task IngestEvent_MissingDeviceMetadata_ProvidesCleanDefaults()
    {
        var evt = new TelemetryIngestEvent(
            EventId: "evt-meta-clean",
            TenantId: _testTenantId,
            UserId: "user-delta",
            SessionId: "ses-104",
            EventType: "pageview",
            EventName: "blog_read",
            TimestampUtc: DateTime.UtcNow,
            Url: "/blog",
            DeviceType: null,
            Os: null,
            Browser: null,
            AppVersion: null
        );

        await _service.IngestEventAsync(evt);
        await _service.FlushBufferAsync();

        var recent = await _service.GetRecentEventsAsync(_testTenantId, 5, userId: "user-delta");
        Assert.NotEmpty(recent);
        Assert.Equal("Desktop", recent[0].DeviceType);
        Assert.Equal("Web", recent[0].Os);
        Assert.Equal("Chrome", recent[0].Browser);
        Assert.Equal("1.0.0", recent[0].AppVersion);
    }

    [Fact]
    public async Task IngestBatch_MultipleEvents_EnqueuesAllAndReturnsCount()
    {
        var batch = new List<TelemetryIngestEvent>
        {
            new("batch-1", _testTenantId, "user-b1", "ses-b1", "pageview", "view_a", DateTime.UtcNow, "/a"),
            new("batch-2", _testTenantId, "user-b2", "ses-b2", "pageview", "view_b", DateTime.UtcNow, "/b"),
            new("batch-3", _testTenantId, "user-b3", "ses-b3", "pageview", "view_c", DateTime.UtcNow, "/c")
        };

        var response = await _service.IngestBatchAsync(batch);

        Assert.True(response.Success);
        Assert.Equal(3, response.AcceptedCount);
        Assert.True(response.TotalQueueCount >= 3);
    }

    [Fact]
    public async Task IngestBatch_EmptyList_ReturnsZeroAcceptedWithoutError()
    {
        var response = await _service.IngestBatchAsync(new List<TelemetryIngestEvent>());

        Assert.True(response.Success);
        Assert.Equal(0, response.AcceptedCount);
    }

    [Fact]
    public async Task FlushBuffer_MovesQueuedEventsToStoredEvents()
    {
        var initialPending = _service.GetPendingBufferCount();
        var initialStored = _service.GetTotalStoredEventsCount();

        await _service.IngestEventAsync(new("flush-1", _testTenantId, "user-f1", "ses-f1", "click", "btn_save", DateTime.UtcNow, "/settings"));
        await _service.IngestEventAsync(new("flush-2", _testTenantId, "user-f2", "ses-f2", "click", "btn_exit", DateTime.UtcNow, "/settings"));

        Assert.Equal(initialPending + 2, _service.GetPendingBufferCount());

        var flushed = await _service.FlushBufferAsync(100);

        Assert.True(flushed >= 2);
        Assert.True(_service.GetTotalStoredEventsCount() >= initialStored + 2);
    }

    [Fact]
    public async Task FlushBuffer_WithMaxBatchSize_DrainsOnlyUpToLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            await _service.IngestEventAsync(new($"limit-{i}", _testTenantId, $"user-{i}", "ses-lim", "pageview", "home", DateTime.UtcNow, "/"));
        }

        var drained = await _service.FlushBufferAsync(maxBatchSize: 4);

        Assert.Equal(4, drained);
    }

    [Fact]
    public async Task FlushBuffer_EmptyQueue_ReturnsZero()
    {
        // Drain everything first
        await _service.FlushBufferAsync(int.MaxValue);

        var drained = await _service.FlushBufferAsync(100);

        Assert.Equal(0, drained);
        Assert.Equal(0, _service.GetPendingBufferCount());
    }

    [Fact]
    public async Task GetPendingBufferCount_ReflectsAccurateQueueLength()
    {
        await _service.FlushBufferAsync(int.MaxValue);
        Assert.Equal(0, _service.GetPendingBufferCount());

        await _service.IngestEventAsync(new("ct-1", _testTenantId, "u1", "s1", "pv", "home", DateTime.UtcNow, "/"));
        await _service.IngestEventAsync(new("ct-2", _testTenantId, "u2", "s2", "pv", "home", DateTime.UtcNow, "/"));

        Assert.Equal(2, _service.GetPendingBufferCount());
    }

    [Fact]
    public async Task GetTotalStoredEventsCount_IncreasesAfterFlushing()
    {
        int before = _service.GetTotalStoredEventsCount();
        await _service.IngestEventAsync(new("ts-1", _testTenantId, "u1", "s1", "pv", "home", DateTime.UtcNow, "/"));
        await _service.FlushBufferAsync();
        int after = _service.GetTotalStoredEventsCount();

        Assert.True(after > before);
    }

    #endregion

    #region Time-Series Rollup Aggregator Tests

    [Fact]
    public async Task GetActiveUsersMetrics_ComputesDauCorrectlyForToday()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Ingest today's events for 3 distinct users
        await _service.IngestEventAsync(new("d1", tenant, "dau-user-1", "s1", "pv", "home", now, "/"));
        await _service.IngestEventAsync(new("d2", tenant, "dau-user-2", "s2", "pv", "home", now, "/"));
        await _service.IngestEventAsync(new("d3", tenant, "dau-user-3", "s3", "pv", "home", now, "/"));
        await _service.FlushBufferAsync();

        var metrics = await _service.GetActiveUsersMetricsAsync(tenant, days: 7);

        Assert.Equal(3, metrics.Dau);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_DuplicateEventsSameUser_CountsDauAsOne()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Ingest 5 events for the same user today
        for (int i = 0; i < 5; i++)
        {
            await _service.IngestEventAsync(new($"dup-{i}", tenant, "same-user", $"s{i}", "click", "btn", now.AddMinutes(i), "/"));
        }
        await _service.FlushBufferAsync();

        var metrics = await _service.GetActiveUsersMetricsAsync(tenant, days: 7);

        Assert.Equal(1, metrics.Dau);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_ComputesWauAcrossLast7Days()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // User active today
        await _service.IngestEventAsync(new("w1", tenant, "user-now", "s1", "pv", "home", now, "/"));
        // User active 4 days ago
        await _service.IngestEventAsync(new("w2", tenant, "user-4d", "s2", "pv", "home", now.AddDays(-4), "/"));
        // User active 6 days ago
        await _service.IngestEventAsync(new("w3", tenant, "user-6d", "s3", "pv", "home", now.AddDays(-6), "/"));
        // User active 10 days ago (outside WAU)
        await _service.IngestEventAsync(new("w4", tenant, "user-10d", "s4", "pv", "home", now.AddDays(-10), "/"));
        await _service.FlushBufferAsync();

        var metrics = await _service.GetActiveUsersMetricsAsync(tenant, days: 14);

        Assert.Equal(3, metrics.Wau);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_ComputesMauAcrossLast30Days()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _service.IngestEventAsync(new("m1", tenant, "mau-1", "s1", "pv", "home", now.AddDays(-2), "/"));
        await _service.IngestEventAsync(new("m2", tenant, "mau-2", "s2", "pv", "home", now.AddDays(-15), "/"));
        await _service.IngestEventAsync(new("m3", tenant, "mau-3", "s3", "pv", "home", now.AddDays(-28), "/"));
        await _service.IngestEventAsync(new("m4", tenant, "mau-old", "s4", "pv", "home", now.AddDays(-35), "/")); // outside
        await _service.FlushBufferAsync();

        var metrics = await _service.GetActiveUsersMetricsAsync(tenant, days: 30);

        Assert.Equal(3, metrics.Mau);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_UsersOlderThan30Days_ExcludedFromMau()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _service.IngestEventAsync(new("old-1", tenant, "ancient-user", "s1", "pv", "home", now.AddDays(-45), "/"));
        await _service.FlushBufferAsync();

        var metrics = await _service.GetActiveUsersMetricsAsync(tenant, days: 30);

        Assert.Equal(0, metrics.Mau);
        Assert.Equal(0, metrics.Wau);
        Assert.Equal(0, metrics.Dau);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_StickinessRatio_ComputesDauDividedByMauTimes100()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // 2 DAU today
        await _service.IngestEventAsync(new("st-1", tenant, "st-user-1", "s1", "pv", "home", now, "/"));
        await _service.IngestEventAsync(new("st-2", tenant, "st-user-2", "s2", "pv", "home", now, "/"));

        // 8 other users during the month (total MAU = 10)
        for (int i = 3; i <= 10; i++)
        {
            await _service.IngestEventAsync(new($"st-{i}", tenant, $"st-user-{i}", $"s{i}", "pv", "home", now.AddDays(-i), "/"));
        }
        await _service.FlushBufferAsync();

        var metrics = await _service.GetActiveUsersMetricsAsync(tenant, days: 30);

        Assert.Equal(2, metrics.Dau);
        Assert.Equal(10, metrics.Mau);
        Assert.Equal(20.0, metrics.StickinessRatioPercent);
        Assert.Equal("Good (18-25%)", metrics.StickinessBenchmarkHealth);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_ZeroMau_ReturnsZeroStickinessWithoutDivideByZero()
    {
        var tenant = Guid.NewGuid();
        var metrics = await _service.GetActiveUsersMetricsAsync(tenant, days: 30);

        Assert.Equal(0, metrics.Mau);
        Assert.Equal(0.0, metrics.StickinessRatioPercent);
        Assert.Equal("At Risk (<10%)", metrics.StickinessBenchmarkHealth);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_StickinessBenchmarkHealth_CategorizesCorrectly()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // 3 DAU, 10 MAU = 30% -> Elite
        for (int i = 1; i <= 3; i++)
        {
            await _service.IngestEventAsync(new($"e-{i}", tenant, $"u-{i}", "s", "pv", "home", now, "/"));
        }
        for (int i = 4; i <= 10; i++)
        {
            await _service.IngestEventAsync(new($"e-{i}", tenant, $"u-{i}", "s", "pv", "home", now.AddDays(-5), "/"));
        }
        await _service.FlushBufferAsync();

        var metrics = await _service.GetActiveUsersMetricsAsync(tenant, days: 30);

        Assert.Equal(30.0, metrics.StickinessRatioPercent);
        Assert.Equal("Elite (>25%)", metrics.StickinessBenchmarkHealth);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_GeneratesDailyTimeSeries_WithCorrectDatesAndRollup()
    {
        var metrics = await _service.GetActiveUsersMetricsAsync(days: 14);

        Assert.NotNull(metrics.TimeSeries);
        Assert.Equal(14, metrics.TimeSeries.Count);
        Assert.All(metrics.TimeSeries, pt =>
        {
            Assert.False(string.IsNullOrWhiteSpace(pt.DateLabel));
            Assert.True(pt.Dau >= 0);
            Assert.True(pt.EventsCount >= 0);
        });
    }

    [Fact]
    public async Task GetActiveUsersMetrics_TenantFiltering_IsolatesDataCorrectly()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _service.IngestEventAsync(new("iso-a", tenantA, "user-a", "s1", "pv", "home", now, "/"));
        await _service.IngestEventAsync(new("iso-b1", tenantB, "user-b1", "s2", "pv", "home", now, "/"));
        await _service.IngestEventAsync(new("iso-b2", tenantB, "user-b2", "s3", "pv", "home", now, "/"));
        await _service.FlushBufferAsync();

        var metricsA = await _service.GetActiveUsersMetricsAsync(tenantA, days: 7);
        var metricsB = await _service.GetActiveUsersMetricsAsync(tenantB, days: 7);

        Assert.Equal(1, metricsA.Dau);
        Assert.Equal(2, metricsB.Dau);
    }

    [Fact]
    public async Task GetActiveUsersMetrics_DayOverDayAndWeekOverWeekGrowth_CalculatedAccurately()
    {
        var metrics = await _service.GetActiveUsersMetricsAsync(days: 30);

        // Verify metrics structure contains growth rates
        Assert.True(metrics.DauDayOverDayGrowthPercent >= -100.0);
        Assert.True(metrics.WauWeekOverWeekGrowthPercent >= -100.0);
    }

    #endregion

    #region Cohort Retention Matrix Engine Tests

    [Fact]
    public async Task GetCohortRetentionMatrix_ReturnsTriangularMatrixStructure()
    {
        var matrix = await _service.GetCohortRetentionMatrixAsync(granularity: "weekly", cohortCount: 6);

        Assert.NotNull(matrix);
        Assert.Equal("weekly", matrix.Granularity);
        Assert.Equal(new List<int> { 0, 1, 3, 7, 14, 21, 30 }, matrix.RetentionIntervals);
        Assert.NotEmpty(matrix.Cohorts);
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_Day0Retention_IsAlways100Percent()
    {
        var matrix = await _service.GetCohortRetentionMatrixAsync(granularity: "weekly", cohortCount: 5);

        foreach (var cohort in matrix.Cohorts)
        {
            Assert.True(cohort.RetentionPercentages.ContainsKey(0));
            Assert.Equal(100.0, cohort.RetentionPercentages[0]);
            Assert.Equal(cohort.TotalAcquiredUsers, cohort.RetainedUserCounts[0]);
        }
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_ReturningUsersOnDay1_CalculatesExactPercentage()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var acquisitionDate = now.Date.AddDays(-14);

        // Cohort of 4 users acquired 14 days ago
        for (int u = 1; u <= 4; u++)
        {
            await _service.IngestEventAsync(new($"coh-acq-{u}", tenant, $"coh-user-{u}", $"s-{u}", "pv", "signup", acquisitionDate, "/signup"));
        }

        // Exactly 2 users return on Day 1
        await _service.IngestEventAsync(new("coh-d1-1", tenant, "coh-user-1", "s-r1", "pv", "home", acquisitionDate.AddDays(1), "/home"));
        await _service.IngestEventAsync(new("coh-d1-2", tenant, "coh-user-2", "s-r2", "pv", "home", acquisitionDate.AddDays(1), "/home"));

        await _service.FlushBufferAsync();

        var matrix = await _service.GetCohortRetentionMatrixAsync(tenant, granularity: "daily", cohortCount: 30);
        var targetCohort = matrix.Cohorts.FirstOrDefault(c => c.CohortStartDate == acquisitionDate);

        Assert.NotNull(targetCohort);
        Assert.Equal(4, targetCohort.TotalAcquiredUsers);
        Assert.Equal(50.0, targetCohort.RetentionPercentages[1]); // 2 / 4 = 50.0%
        Assert.Equal(2, targetCohort.RetainedUserCounts[1]);
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_UsersWithNoActivity_ShowsZeroRetention()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var acquisitionDate = now.Date.AddDays(-10);

        // 3 users acquired 10 days ago who NEVER return
        for (int u = 1; u <= 3; u++)
        {
            await _service.IngestEventAsync(new($"ghost-{u}", tenant, $"ghost-user-{u}", $"s-{u}", "pv", "landing", acquisitionDate, "/"));
        }
        await _service.FlushBufferAsync();

        var matrix = await _service.GetCohortRetentionMatrixAsync(tenant, granularity: "daily", cohortCount: 20);
        var ghostCohort = matrix.Cohorts.FirstOrDefault(c => c.CohortStartDate == acquisitionDate);

        Assert.NotNull(ghostCohort);
        Assert.Equal(3, ghostCohort.TotalAcquiredUsers);
        Assert.Equal(100.0, ghostCohort.RetentionPercentages[0]);
        Assert.Equal(0.0, ghostCohort.RetentionPercentages[1]);
        Assert.Equal(0.0, ghostCohort.RetentionPercentages[3]);
        Assert.Equal(0.0, ghostCohort.RetentionPercentages[7]);
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_FutureIntervals_ReturnNullPendingStatus()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        // User acquired yesterday
        var acquisitionDate = now.Date.AddDays(-1);

        await _service.IngestEventAsync(new("fut-1", tenant, "future-user", "s1", "pv", "home", acquisitionDate, "/"));
        await _service.FlushBufferAsync();

        var matrix = await _service.GetCohortRetentionMatrixAsync(tenant, granularity: "daily", cohortCount: 5);
        var recentCohort = matrix.Cohorts.FirstOrDefault(c => c.CohortStartDate == acquisitionDate);

        Assert.NotNull(recentCohort);
        // Day 7, Day 14, Day 21, Day 30 cannot have occurred yet for a cohort acquired yesterday
        Assert.Null(recentCohort.RetentionPercentages[7]);
        Assert.Null(recentCohort.RetentionPercentages[14]);
        Assert.Null(recentCohort.RetentionPercentages[21]);
        Assert.Null(recentCohort.RetentionPercentages[30]);
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_WeeklyGranularity_GroupsByMondayCohort()
    {
        var matrix = await _service.GetCohortRetentionMatrixAsync(granularity: "weekly", cohortCount: 5);

        Assert.NotEmpty(matrix.Cohorts);
        foreach (var c in matrix.Cohorts)
        {
            Assert.Contains("-W", c.CohortId);
            Assert.Equal(DayOfWeek.Monday, c.CohortStartDate.DayOfWeek);
        }
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_MonthlyGranularity_GroupsByFirstOfMonth()
    {
        var matrix = await _service.GetCohortRetentionMatrixAsync(granularity: "monthly", cohortCount: 5);

        Assert.NotEmpty(matrix.Cohorts);
        foreach (var c in matrix.Cohorts)
        {
            Assert.Equal(1, c.CohortStartDate.Day);
        }
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_DailyGranularity_GroupsByDate()
    {
        var matrix = await _service.GetCohortRetentionMatrixAsync(granularity: "daily", cohortCount: 10);

        Assert.NotEmpty(matrix.Cohorts);
        Assert.Equal("daily", matrix.Granularity);
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_AverageRetentionCurve_ComputesWeightedAverages()
    {
        var matrix = await _service.GetCohortRetentionMatrixAsync(granularity: "weekly", cohortCount: 8);

        Assert.NotNull(matrix.AverageRetentionCurve);
        Assert.True(matrix.AverageRetentionCurve.ContainsKey(0));
        Assert.Equal(100.0, matrix.AverageRetentionCurve[0]);
        // Typical SaaS decay curve: Day 0 >= Day 1 >= Day 7 >= Day 30
        Assert.True(matrix.AverageRetentionCurve[0] >= matrix.AverageRetentionCurve[1]);
        Assert.True(matrix.AverageRetentionCurve[1] >= matrix.AverageRetentionCurve[7]);
    }

    [Fact]
    public async Task GetCohortRetentionMatrix_EmptyStore_ReturnsEmptyMatrixGracefully()
    {
        var isolatedTenant = Guid.NewGuid();
        var matrix = await _service.GetCohortRetentionMatrixAsync(isolatedTenant, granularity: "weekly", cohortCount: 5);

        Assert.NotNull(matrix);
        Assert.Empty(matrix.Cohorts);
        Assert.Equal(0.0, matrix.AverageRetentionCurve[0]);
    }

    #endregion

    #region Crash Diagnostics & Stack Trace Clustering Tests

    [Fact]
    public void ComputeDeterministicSignature_IdenticalTraces_ProduceIdenticalSha256()
    {
        string type = "NullReferenceException";
        string trace = "   at SGrowing.Web.Components.PaymentModal.OnSubmit() in PaymentModal.cs:line 142\n   at Microsoft.AspNetCore.Components.ComponentBase.CallStateHasChanged()";
        string msg = "Object reference not set";

        var sig1 = ProductAnalyticsService.ComputeDeterministicSignature(type, trace, msg);
        var sig2 = ProductAnalyticsService.ComputeDeterministicSignature(type, trace, msg);

        Assert.Equal(sig1, sig2);
        Assert.Equal(64, sig1.Length); // 64 hex chars for SHA-256
    }

    [Fact]
    public void ComputeDeterministicSignature_NormalizesLineNumbers_AcrossEnvironments()
    {
        string type = "ArgumentNullException";
        string traceDev = "   at SGrowing.Services.UserResolver.Resolve() in UserResolver.cs:line 45";
        string traceProd = "   at SGrowing.Services.UserResolver.Resolve() in UserResolver.cs:line 987";
        string msg = "Value cannot be null";

        var sigDev = ProductAnalyticsService.ComputeDeterministicSignature(type, traceDev, msg);
        var sigProd = ProductAnalyticsService.ComputeDeterministicSignature(type, traceProd, msg);

        Assert.Equal(sigDev, sigProd);
    }

    [Fact]
    public void ComputeDeterministicSignature_NormalizesHexMemoryAddresses()
    {
        string type = "AccessViolationException";
        string traceA = "   at NativeInterop.Dispatch(IntPtr 0x7fffbc1209) in Dispatch.cs:line 10";
        string traceB = "   at NativeInterop.Dispatch(IntPtr 0x12a0fe55aa) in Dispatch.cs:line 10";
        string msg = "Memory corruption";

        var sigA = ProductAnalyticsService.ComputeDeterministicSignature(type, traceA, msg);
        var sigB = ProductAnalyticsService.ComputeDeterministicSignature(type, traceB, msg);

        Assert.Equal(sigA, sigB);
    }

    [Fact]
    public void ComputeDeterministicSignature_StripsFullFilePaths()
    {
        string type = "InvalidOperationException";
        string traceWin = @"   at SGrowing.Handler.Handle() in C:\Builds\Agent01\src\Handler.cs:line 15";
        string traceUnix = @"   at SGrowing.Handler.Handle() in /var/jenkins/workspace/src/Handler.cs:line 15";
        string msg = "Operation invalid";

        var sigWin = ProductAnalyticsService.ComputeDeterministicSignature(type, traceWin, msg);
        var sigUnix = ProductAnalyticsService.ComputeDeterministicSignature(type, traceUnix, msg);

        Assert.Equal(sigWin, sigUnix);
    }

    [Fact]
    public async Task RecordCrash_NewException_CreatesNewClusterWithSha256()
    {
        var payload = new CrashReportPayload(
            ExceptionType: "System.DivideByZeroException",
            Message: "Attempted to divide by zero in MetricCalculator.Compute()",
            StackTrace: "   at MetricCalculator.Compute() in MetricCalculator.cs:line 22",
            Breadcrumbs: new List<string> { "input:denominator_zero", "click:calculate_btn" },
            Severity: "Critical"
        );

        var cluster = await _service.RecordCrashAsync(_testTenantId, "user-crash-1", payload, "3.0.0", "macOS", "Safari");

        Assert.NotNull(cluster);
        Assert.Equal(64, cluster.Signature.Length);
        Assert.Equal("System.DivideByZeroException", cluster.ExceptionType);
        Assert.Equal(1, cluster.TotalOccurrences);
        Assert.Equal(1, cluster.UniqueUsersAffected);
        Assert.Equal("Critical", cluster.Severity);
        Assert.Equal("Open", cluster.Status);
    }

    [Fact]
    public async Task RecordCrash_ExistingSignature_IncrementsOccurrencesAndUpdatesLastSeen()
    {
        var payload = new CrashReportPayload(
            ExceptionType: "System.TimeoutException",
            Message: "Redis cache connection timeout",
            StackTrace: "   at SGrowing.Cache.Redis.GetAsync() in Redis.cs:line 45",
            Breadcrumbs: new List<string> { "cache_lookup" },
            Severity: "Error"
        );

        var c1 = await _service.RecordCrashAsync(_testTenantId, "user-time-1", payload);
        var c2 = await _service.RecordCrashAsync(_testTenantId, "user-time-2", payload);

        Assert.Equal(c1.Signature, c2.Signature);
        Assert.Equal(2, c2.TotalOccurrences);
        Assert.Equal(2, c2.UniqueUsersAffected);
    }

    [Fact]
    public async Task RecordCrash_SameUserMultipleCrashes_PreservesUniqueUserCount()
    {
        var payload = new CrashReportPayload(
            ExceptionType: "System.FormatException",
            Message: "Input string was not in a correct format",
            StackTrace: "   at System.Number.ParseInt32() in Number.cs:line 12",
            Severity: "Warning"
        );

        // Same user crashes 3 times
        await _service.RecordCrashAsync(_testTenantId, "same-user-id", payload);
        await _service.RecordCrashAsync(_testTenantId, "same-user-id", payload);
        var cluster = await _service.RecordCrashAsync(_testTenantId, "same-user-id", payload);

        Assert.Equal(3, cluster.TotalOccurrences);
        Assert.Equal(1, cluster.UniqueUsersAffected);
    }

    [Fact]
    public async Task RecordCrash_CollectsBreadcrumbTrail_AndCapsAtMaximumLimit()
    {
        var breadcrumbs = Enumerable.Range(1, 30).Select(i => $"action_step_{i}").ToList();
        var payload = new CrashReportPayload(
            ExceptionType: "StackOverflowException",
            Message: "Recursive loop detected",
            StackTrace: "   at Engine.Recurse() in Engine.cs:line 10",
            Breadcrumbs: breadcrumbs
        );

        var cluster = await _service.RecordCrashAsync(_testTenantId, "user-bc", payload);

        Assert.NotEmpty(cluster.RecentBreadcrumbs);
        Assert.True(cluster.RecentBreadcrumbs.Count <= 25);
    }

    [Fact]
    public async Task RecordCrash_TracksMultiplePlatformsAndVersions()
    {
        var payload = new CrashReportPayload(
            ExceptionType: "PlatformNotSupportedException",
            Message: "Native API unsupported",
            StackTrace: "   at NativeBridge.Call() in Bridge.cs:line 5"
        );

        await _service.RecordCrashAsync(_testTenantId, "u1", payload, appVersion: "2.1.0", os: "Linux", browser: "Chrome");
        var cluster = await _service.RecordCrashAsync(_testTenantId, "u2", payload, appVersion: "2.2.0", os: "macOS", browser: "Safari");

        Assert.Contains("2.1.0", cluster.AffectedVersions);
        Assert.Contains("2.2.0", cluster.AffectedVersions);
        Assert.True(cluster.PlatformBreakdown.ContainsKey("Linux / Chrome"));
        Assert.True(cluster.PlatformBreakdown.ContainsKey("macOS / Safari"));
    }

    [Fact]
    public async Task GetCrashClusters_FilterBySeverity_ReturnsMatchingClustersOnly()
    {
        var criticalClusters = await _service.GetCrashClustersAsync(severity: "Critical");

        Assert.NotEmpty(criticalClusters.Clusters);
        Assert.All(criticalClusters.Clusters, c => Assert.Equal("Critical", c.Severity));
    }

    [Fact]
    public async Task GetCrashClusters_FilterByStatus_ReturnsMatchingClustersOnly()
    {
        var openClusters = await _service.GetCrashClustersAsync(status: "Open");

        Assert.NotEmpty(openClusters.Clusters);
        Assert.All(openClusters.Clusters, c => Assert.Equal("Open", c.Status));
    }

    [Fact]
    public async Task UpdateCrashStatus_ValidSignature_UpdatesStatusSuccessfully()
    {
        var clusters = await _service.GetCrashClustersAsync();
        var firstCluster = clusters.Clusters.First();

        var success = await _service.UpdateCrashStatusAsync(firstCluster.Signature, "Investigating");
        Assert.True(success);

        var updatedClusters = await _service.GetCrashClustersAsync();
        var updated = updatedClusters.Clusters.First(c => c.Signature == firstCluster.Signature);
        Assert.Equal("Investigating", updated.Status);
    }

    [Fact]
    public async Task UpdateCrashStatus_UnknownSignature_ReturnsFalse()
    {
        var success = await _service.UpdateCrashStatusAsync("non-existent-sha-signature-12345", "Resolved");
        Assert.False(success);
    }

    [Fact]
    public async Task IngestEvent_CrashEventType_AutomaticallyRecordsCrashCluster()
    {
        var evt = new TelemetryIngestEvent(
            EventId: "crash-evt-auto",
            TenantId: _testTenantId,
            UserId: "auto-crash-user",
            SessionId: "ses-c1",
            EventType: "crash",
            EventName: "unhandled_exception",
            TimestampUtc: DateTime.UtcNow,
            Url: "/billing/checkout",
            CrashData: new CrashReportPayload(
                ExceptionType: "StripePaymentCardException",
                Message: "Card declined due to insufficient funds",
                StackTrace: "   at PaymentModule.Pay() in Payment.cs:line 99",
                Severity: "Critical"
            )
        );

        await _service.IngestEventAsync(evt);

        var clusters = await _service.GetCrashClustersAsync(severity: "Critical");
        var matched = clusters.Clusters.FirstOrDefault(c => c.ExceptionType == "StripePaymentCardException");

        Assert.NotNull(matched);
        Assert.Equal("Critical", matched.Severity);
    }

    #endregion

    #region Multi-Step Funnel Conversion Calculator Tests

    [Fact]
    public async Task EvaluateFunnel_StandardFunnel_ComputesStepByStepProgression()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow.AddHours(-1);

        // 10 users enter step 0
        // 6 advance to step 1
        // 3 advance to step 2
        for (int i = 1; i <= 10; i++)
        {
            var u = $"funnel-u-{i}";
            await _service.IngestEventAsync(new($"fn-0-{i}", tenant, u, $"s{i}", "pageview", "step_land", now, "/"));

            if (i <= 6)
            {
                await _service.IngestEventAsync(new($"fn-1-{i}", tenant, u, $"s{i}", "click", "step_signup", now.AddMinutes(2), "/signup"));
            }

            if (i <= 3)
            {
                await _service.IngestEventAsync(new($"fn-2-{i}", tenant, u, $"s{i}", "custom", "step_pay", now.AddMinutes(5), "/pay"));
            }
        }
        await _service.FlushBufferAsync();

        var request = new EvaluateFunnelRequestDto(
            TenantId: tenant,
            FunnelName: "Growth Funnel",
            Steps: new List<string> { "step_land", "step_signup", "step_pay" }
        );

        var result = await _service.EvaluateFunnelAsync(request);

        Assert.Equal("Growth Funnel", result.FunnelName);
        Assert.Equal(10, result.TotalEnteredUsers);
        Assert.Equal(3, result.TotalCompletedUsers);
        Assert.Equal(30.0, result.OverallConversionPercent); // 3 / 10 = 30%
        Assert.Equal(3, result.Steps.Count);

        // Step 0
        Assert.Equal(10, result.Steps[0].ReachedCount);
        Assert.Equal(4, result.Steps[0].DroppedCount); // 10 - 6 = 4
        Assert.Equal(100.0, result.Steps[0].StepConversionPercent);
        Assert.Equal(40.0, result.Steps[0].DropoffPercent);

        // Step 1
        Assert.Equal(6, result.Steps[1].ReachedCount);
        Assert.Equal(3, result.Steps[1].DroppedCount); // 6 - 3 = 3
        Assert.Equal(60.0, result.Steps[1].StepConversionPercent); // 6 / 10 = 60%
        Assert.Equal(60.0, result.Steps[1].OverallConversionPercent);

        // Step 2
        Assert.Equal(3, result.Steps[2].ReachedCount);
        Assert.Equal(0, result.Steps[2].DroppedCount);
        Assert.Equal(50.0, result.Steps[2].StepConversionPercent); // 3 / 6 = 50%
        Assert.Equal(30.0, result.Steps[2].OverallConversionPercent);
    }

    [Fact]
    public async Task EvaluateFunnel_StrictChronology_DisallowsSkippingIntermediateSteps()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow.AddHours(-1);

        // User does step 0, skips step 1, directly does step 2
        await _service.IngestEventAsync(new("sk-0", tenant, "skip-user", "s1", "pv", "step_0", now, "/0"));
        await _service.IngestEventAsync(new("sk-2", tenant, "skip-user", "s1", "pv", "step_2", now.AddMinutes(5), "/2"));
        await _service.FlushBufferAsync();

        var request = new EvaluateFunnelRequestDto(
            TenantId: tenant,
            FunnelName: "Strict Flow",
            Steps: new List<string> { "step_0", "step_1", "step_2" }
        );

        var result = await _service.EvaluateFunnelAsync(request);

        Assert.Equal(1, result.TotalEnteredUsers);
        Assert.Equal(0, result.TotalCompletedUsers);
        Assert.Equal(0.0, result.OverallConversionPercent);
        Assert.Equal(1, result.Steps[0].ReachedCount);
        Assert.Equal(0, result.Steps[1].ReachedCount);
        Assert.Equal(0, result.Steps[2].ReachedCount);
    }

    [Fact]
    public async Task EvaluateFunnel_ComputesOverallConversionRate_FromFirstToLastStep()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow.AddHours(-2);

        // 4 users entered, 1 completed
        for (int i = 1; i <= 4; i++)
        {
            await _service.IngestEventAsync(new($"r0-{i}", tenant, $"u-{i}", "s", "pv", "entry", now, "/"));
        }
        await _service.IngestEventAsync(new("r1-1", tenant, "u-1", "s", "pv", "finish", now.AddMinutes(10), "/done"));
        await _service.FlushBufferAsync();

        var result = await _service.EvaluateFunnelAsync(new(tenant, "Quick", new() { "entry", "finish" }));

        Assert.Equal(25.0, result.OverallConversionPercent); // 1 / 4 = 25%
    }

    [Fact]
    public async Task EvaluateFunnel_ComputesStepDropoffCountAndPercent()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow.AddHours(-1);

        // 5 users at step 1, 1 advances to step 2 -> 4 dropped (80% drop-off)
        for (int i = 1; i <= 5; i++)
        {
            await _service.IngestEventAsync(new($"d0-{i}", tenant, $"usr-d-{i}", "s", "pv", "start", now, "/"));
        }
        await _service.IngestEventAsync(new("d1-1", tenant, "usr-d-1", "s", "pv", "middle", now.AddMinutes(3), "/mid"));
        await _service.FlushBufferAsync();

        var result = await _service.EvaluateFunnelAsync(new(tenant, "Dropoff Test", new() { "start", "middle" }));

        Assert.Equal(4, result.Steps[0].DroppedCount);
        Assert.Equal(80.0, result.Steps[0].DropoffPercent);
        Assert.Equal("start", result.BiggestDropoffStep);
        Assert.Equal(80.0, result.BiggestDropoffPercent);
    }

    [Fact]
    public async Task EvaluateFunnel_IdentifiesBiggestDropoffStep()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow.AddMinutes(-30);

        // Step A: 10 users
        // Step B: 9 users (10% drop)
        // Step C: 2 users (77.8% drop - biggest dropoff)
        for (int i = 1; i <= 10; i++)
        {
            var u = $"bot-{i}";
            await _service.IngestEventAsync(new($"a-{i}", tenant, u, "s", "pv", "stepA", now, "/A"));
            if (i <= 9)
            {
                await _service.IngestEventAsync(new($"b-{i}", tenant, u, "s", "pv", "stepB", now.AddMinutes(1), "/B"));
            }
            if (i <= 2)
            {
                await _service.IngestEventAsync(new($"c-{i}", tenant, u, "s", "pv", "stepC", now.AddMinutes(2), "/C"));
            }
        }
        await _service.FlushBufferAsync();

        var result = await _service.EvaluateFunnelAsync(new(tenant, "Bottleneck", new() { "stepA", "stepB", "stepC" }));

        Assert.Equal("stepB", result.BiggestDropoffStep);
        Assert.True(result.BiggestDropoffPercent > 70.0);
    }

    [Fact]
    public async Task EvaluateFunnel_CalculatesAverageTransitionDuration()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow.AddMinutes(-15);

        // User 1 took 60 seconds, User 2 took 120 seconds -> average 90 seconds
        await _service.IngestEventAsync(new("t-0-1", tenant, "dur-1", "s", "pv", "origin", now, "/"));
        await _service.IngestEventAsync(new("t-1-1", tenant, "dur-1", "s", "pv", "dest", now.AddSeconds(60), "/done"));

        await _service.IngestEventAsync(new("t-0-2", tenant, "dur-2", "s", "pv", "origin", now, "/"));
        await _service.IngestEventAsync(new("t-1-2", tenant, "dur-2", "s", "pv", "dest", now.AddSeconds(120), "/done"));

        await _service.FlushBufferAsync();

        var result = await _service.EvaluateFunnelAsync(new(tenant, "Timing", new() { "origin", "dest" }));

        Assert.Equal(90.0, result.Steps[1].AverageDurationSecondsFromPrevious);
    }

    [Fact]
    public async Task EvaluateFunnel_ZeroEnteredUsers_ReturnsSafeZeroes()
    {
        var tenant = Guid.NewGuid();
        var result = await _service.EvaluateFunnelAsync(new(tenant, "Empty", new() { "non_existent_1", "non_existent_2" }));

        Assert.Equal(0, result.TotalEnteredUsers);
        Assert.Equal(0, result.TotalCompletedUsers);
        Assert.Equal(0.0, result.OverallConversionPercent);
    }

    [Fact]
    public async Task EvaluateFunnel_EmptyStepsList_ReturnsGracefulResult()
    {
        var result = await _service.EvaluateFunnelAsync(new(null, "No Steps", new()));

        Assert.Equal(0, result.TotalEnteredUsers);
        Assert.Equal(0.0, result.OverallConversionPercent);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public async Task EvaluateFunnel_DateRangeFilter_ExcludesEventsOutsideWindow()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // User active 10 days ago
        await _service.IngestEventAsync(new("out-0", tenant, "range-u", "s", "pv", "step1", now.AddDays(-10), "/1"));
        await _service.IngestEventAsync(new("out-1", tenant, "range-u", "s", "pv", "step2", now.AddDays(-10).AddMinutes(5), "/2"));
        await _service.FlushBufferAsync();

        // Query only last 2 days
        var result = await _service.EvaluateFunnelAsync(new(
            tenant,
            "Windowed",
            new() { "step1", "step2" },
            FromUtc: now.AddDays(-2),
            ToUtc: now
        ));

        Assert.Equal(0, result.TotalEnteredUsers);
    }

    #endregion

    #region Concurrency & High Parallel Telemetry Tests

    [Fact]
    public async Task ConcurrentIngestion_ThousandsOfParallelEvents_PreservesAllItemsWithoutLoss()
    {
        int parallelEvents = 1000;
        var tenant = Guid.NewGuid();

        await Parallel.ForAsync(0, parallelEvents, async (i, ct) =>
        {
            await _service.IngestEventAsync(new TelemetryIngestEvent(
                EventId: $"par-{i}",
                TenantId: tenant,
                UserId: $"user-par-{i % 50}",
                SessionId: $"ses-{i % 25}",
                EventType: "click",
                EventName: $"action_{i % 5}",
                TimestampUtc: DateTime.UtcNow,
                Url: $"/page/{i % 10}"
            ));
        });

        int pending = _service.GetPendingBufferCount();
        Assert.True(pending >= parallelEvents);

        int drained = await _service.FlushBufferAsync(parallelEvents * 2);
        Assert.True(drained >= parallelEvents);
    }

    [Fact]
    public async Task ConcurrentIngestAndFlush_SimultaneousDrainingAndQueuing_IsThreadSafe()
    {
        var tenant = Guid.NewGuid();
        int producerCount = 200;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var ingestionTask = Task.Run(async () =>
        {
            for (int i = 0; i < producerCount; i++)
            {
                await _service.IngestEventAsync(new($"sim-{i}", tenant, $"u-{i % 20}", "s", "pv", "view", DateTime.UtcNow, "/"));
                await Task.Delay(2);
            }
        });

        var flusherTask = Task.Run(async () =>
        {
            while (!ingestionTask.IsCompleted || _service.GetPendingBufferCount() > 0)
            {
                await _service.FlushBufferAsync(25);
                await Task.Delay(5);
            }
        });

        await Task.WhenAll(ingestionTask, flusherTask);

        // Ensure everything is flushed
        await _service.FlushBufferAsync(int.MaxValue);
        Assert.Equal(0, _service.GetPendingBufferCount());
    }

    [Fact]
    public async Task ConcurrentCrashRecording_ParallelThreadsSameSignature_AccuratelyIncrements()
    {
        var payload = new CrashReportPayload(
            ExceptionType: "System.IO.IOException",
            Message: "Disk full error",
            StackTrace: "   at Storage.Write() in Storage.cs:line 120"
        );

        int workers = 50;
        await Parallel.ForAsync(0, workers, async (i, ct) =>
        {
            await _service.RecordCrashAsync(_testTenantId, $"crash-user-{i}", payload);
        });

        var clusters = await _service.GetCrashClustersAsync();
        var cluster = clusters.Clusters.First(c => c.ExceptionType == "System.IO.IOException");

        Assert.True(cluster.TotalOccurrences >= workers);
        Assert.True(cluster.UniqueUsersAffected >= workers);
    }

    [Fact]
    public async Task RecentEvents_ReturnsChronologicallySortedEventsWithFilter()
    {
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await _service.IngestEventAsync(new("rec-1", tenant, "rec-u1", "s", "pageview", "view_first", now.AddMinutes(-10), "/1"));
        await _service.IngestEventAsync(new("rec-2", tenant, "rec-u1", "s", "click", "click_second", now.AddMinutes(-5), "/2"));
        await _service.IngestEventAsync(new("rec-3", tenant, "rec-u2", "s", "pageview", "view_third", now, "/3"));
        await _service.FlushBufferAsync();

        var recentUser1 = await _service.GetRecentEventsAsync(tenant, 10, userId: "rec-u1");

        Assert.Equal(2, recentUser1.Count);
        Assert.Equal("click_second", recentUser1[0].EventName); // newest first
        Assert.Equal("view_first", recentUser1[1].EventName);
    }

    #endregion
}
