using SGrowing.Api.Modules.Channels.FlowInteractive;
using Xunit;

namespace SGrowing.Tests.Channels;

public sealed class FlowInteractiveTests
{
    private readonly FlowInteractiveEngine _engine = new();

    [Fact]
    public async Task GetFlows_ReturnsSeededInteractiveFlowsWithScreens()
    {
        var flows = await _engine.GetFlowsAsync();

        Assert.NotNull(flows);
        Assert.NotEmpty(flows);
        Assert.Contains(flows, f => f.FlowId == "flow_vip_booking_v3");

        var flow = flows.First(f => f.FlowId == "flow_vip_booking_v3");
        Assert.Equal("3.0", flow.Version);
        Assert.Equal(2, flow.Screens.Count);
        Assert.Contains(flow.Screens, s => s.ScreenId == "SCREEN_LEAD_INFO");
    }

    [Fact]
    public async Task ProcessDataExchange_NavigatesToNextScreen()
    {
        var request = new FlowDataExchangeRequest(
            FlowId: "flow_vip_booking_v3",
            ScreenId: "SCREEN_LEAD_INFO",
            Action: "data_exchange",
            DataPayload: new Dictionary<string, string>
            {
                ["company_name"] = "Stellar Enterprises",
                ["team_size"] = "201-1000",
                ["preferred_date"] = "2026-11-15"
            }
        );

        var response = await _engine.ProcessDataExchangeAsync(request);

        Assert.NotNull(response);
        Assert.Equal("SCREEN_LEAD_INFO", response.ScreenId);
        Assert.Equal("SCREEN_USE_CASE", response.NextScreenId);
        Assert.Equal("lead_info_saved", response.DataPayload["step_completed"]);
    }

    [Fact]
    public async Task ProcessDataExchange_TerminalScreenCompletesBooking()
    {
        var request = new FlowDataExchangeRequest(
            FlowId: "flow_vip_booking_v3",
            ScreenId: "SCREEN_USE_CASE",
            Action: "submit",
            DataPayload: new Dictionary<string, string>
            {
                ["primary_goal"] = "Autonomous Voice SDR",
                ["phone"] = "+14155551234"
            }
        );

        var response = await _engine.ProcessDataExchangeAsync(request);

        Assert.NotNull(response);
        Assert.Equal("SUCCESS_SCREEN", response.NextScreenId);
        Assert.Equal("CONFIRMED_BOOKED", response.DataPayload["status"]);
        Assert.True(response.DataPayload.ContainsKey("calendar_event_id"));
    }

    [Fact]
    public async Task GetFlowById_ExistingAndMissing_ReturnsFlowOrThrows()
    {
        var flow = await _engine.GetFlowByIdAsync("flow_vip_booking_v3");
        Assert.NotNull(flow);
        Assert.Equal("flow_vip_booking_v3", flow.FlowId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.GetFlowByIdAsync("non_existent_flow_id"));
    }

    [Fact]
    public async Task ProcessDataExchange_NullOrMissingFlow_ThrowsExpectedExceptions()
    {
        // 1. Null request
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _engine.ProcessDataExchangeAsync(null!));

        // 2. Non-existent FlowId
        var invalidReq = new FlowDataExchangeRequest("flow_missing", "SCREEN_1", "action", new Dictionary<string, string>());
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.ProcessDataExchangeAsync(invalidReq));
    }

    [Fact]
    public async Task GetAnalytics_CalculatesFunnelCompletionRateAndScreenDropOff()
    {
        var analytics = await _engine.GetAnalyticsAsync();

        Assert.NotNull(analytics);
        Assert.True(analytics.TotalPublishedFlows >= 1);
        Assert.True(analytics.TotalSessionsStarted >= 3);
        Assert.True(analytics.TotalCompletedSessions >= 2);
        Assert.True(analytics.FunnelCompletionRatePercent > 0);
        Assert.Equal("SCREEN_USE_CASE", analytics.HighestDropOffScreen);
    }

    [Fact]
    public async Task ProcessDataExchange_WithUnknownScreenId_FallsBackToFirstScreen()
    {
        var request = new FlowDataExchangeRequest(
            FlowId: "flow_vip_booking_v3",
            ScreenId: "NON_EXISTENT_SCREEN_ID",
            Action: "data_exchange",
            DataPayload: new Dictionary<string, string>
            {
                ["company_name"] = "Fallback Enterprises"
            }
        );

        var response = await _engine.ProcessDataExchangeAsync(request);

        Assert.NotNull(response);
        // Falls back to SCREEN_LEAD_INFO, leading to SCREEN_USE_CASE
        Assert.Equal("SCREEN_LEAD_INFO", response.ScreenId);
        Assert.Equal("SCREEN_USE_CASE", response.NextScreenId);
        Assert.Equal("lead_info_saved", response.DataPayload["step_completed"]);
    }

    [Fact]
    public async Task ProcessDataExchange_TerminalScreen_AddsSessionAndIncrementsCompletedCount()
    {
        var initialAnalytics = await _engine.GetAnalyticsAsync();
        var initialCompleted = initialAnalytics.TotalCompletedSessions;

        var request = new FlowDataExchangeRequest(
            FlowId: "flow_vip_booking_v3",
            ScreenId: "SCREEN_USE_CASE",
            Action: "submit",
            DataPayload: new Dictionary<string, string>
            {
                ["primary_goal"] = "High-Throughput WhatsApp Engine",
                ["phone"] = "+21699887766"
            }
        );

        var response = await _engine.ProcessDataExchangeAsync(request);

        Assert.NotNull(response);
        Assert.Equal("SUCCESS_SCREEN", response.NextScreenId);

        var updatedAnalytics = await _engine.GetAnalyticsAsync();
        Assert.Equal(initialCompleted + 1, updatedAnalytics.TotalCompletedSessions);
    }
}
