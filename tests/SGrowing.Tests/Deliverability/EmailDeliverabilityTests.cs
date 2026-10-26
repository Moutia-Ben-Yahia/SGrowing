using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Deliverability;
using Xunit;

namespace SGrowing.Tests.Deliverability;

public class EmailDeliverabilityTests
{
    private readonly SGrowingDbContext _db;
    private readonly EmailDeliverabilityService _service;

    public EmailDeliverabilityTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new EmailDeliverabilityService(_db);
    }

    [Fact]
    public async Task CheckDomainDnsHealth_ValidDomain_ReturnsHighScoreAndValidDnsRecords()
    {
        var tenantId = Guid.NewGuid();
        var domain = "acme-logistics.com";

        var result = await _service.CheckDomainDnsHealthAsync(tenantId, domain);

        Assert.NotNull(result);
        Assert.Equal(domain, result.DomainName);
        Assert.True(result.OverallHealthScore >= 80);
        Assert.True(result.HasSpf);
        Assert.True(result.HasDkim);
        Assert.True(result.HasDmarc);
        Assert.True(result.HasMx);
        Assert.Contains("v=spf1", result.SpfRecord);
        Assert.False(string.IsNullOrEmpty(result.DmarcPolicy));

        var latest = await _service.GetLatestDomainDnsAsync(tenantId, domain);
        Assert.NotNull(latest);
        Assert.Equal(result.Id, latest.Id);
    }

    [Fact]
    public async Task CheckDomainDnsHealth_SuspiciousDomain_FlagsIssuesAndLowScore()
    {
        var tenantId = Guid.NewGuid();
        var suspiciousDomain = "free-money-now-spam.xyz";

        var result = await _service.CheckDomainDnsHealthAsync(tenantId, suspiciousDomain);

        Assert.NotNull(result);
        Assert.False(result.HasDkim);
        Assert.False(result.HasDmarc);
        Assert.True(result.OverallHealthScore < 80);
        Assert.Contains("MissingDkimRecord", result.IssuesJson);
    }

    [Fact]
    public async Task AnalyzeEmailSpamRisk_CleanEmailWithUnsubscribe_ReturnsLowRisk()
    {
        var tenantId = Guid.NewGuid();
        var subject = "Your Order Confirmation #49281";
        var bodyHtml = "<p>Hi Sarah, your package is currently out for delivery with our courier.</p>" +
                       "<p><a href='https://example.com/unsubscribe'>Unsubscribe from order updates</a></p>";

        var report = await _service.AnalyzeEmailSpamRiskAsync(tenantId, subject, bodyHtml);

        Assert.NotNull(report);
        Assert.True(report.SpamScore <= 30);
        Assert.True(report.HasUnsubscribeHeader);
        Assert.Equal("Safe", report.Verdict);
        Assert.Equal("[]", report.TriggerWordsDetectedJson);
    }

    [Fact]
    public async Task AnalyzeEmailSpamRisk_SpammyContentWithExcessiveCaps_FlagsTriggersAndHighRisk()
    {
        var tenantId = Guid.NewGuid();
        var subject = "ACT NOW 100% FREE MONEY GUARANTEED CASH PRIZE";
        var bodyHtml = "<p>URGENT REPLY WIRE TRANSFER NO COST WINNER CLICK HERE</p>";

        var report = await _service.AnalyzeEmailSpamRiskAsync(tenantId, subject, bodyHtml);

        Assert.NotNull(report);
        Assert.True(report.SpamScore >= 60);
        Assert.False(report.HasUnsubscribeHeader);
        Assert.Equal("HighRisk", report.Verdict);
        Assert.Contains("100% free", report.TriggerWordsDetectedJson);
    }

    [Fact]
    public async Task MailboxWarmup_StartAndSimulateStep_ProgressesWarmupAndLogsExchanges()
    {
        var tenantId = Guid.NewGuid();
        var sender = "sales@enterprise-deliverability.com";

        var cadence = await _service.StartWarmupCadenceAsync(tenantId, sender, dailyTarget: 50, rampCurve: "Linear");

        Assert.NotNull(cadence);
        Assert.Equal(sender, cadence.SenderEmail);
        Assert.Equal(50, cadence.DailyTargetLimit);
        Assert.Equal(0, cadence.TotalSimulatedSent);

        // Simulate warmup step (peer network exchange)
        var updated = await _service.SimulateWarmupStepAsync(tenantId, cadence.Id);

        Assert.NotNull(updated);
        Assert.True(updated.TotalSimulatedSent > 0);

        var logs = await _service.GetWarmupLogsAsync(tenantId, cadence.Id);
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Direction == "Sent" || l.Direction == "Received");
    }
}
