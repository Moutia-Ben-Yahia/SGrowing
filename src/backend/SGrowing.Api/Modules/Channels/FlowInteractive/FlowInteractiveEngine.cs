using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Channels.FlowInteractive;

public class FlowInteractiveEngine
{
    private readonly ConcurrentDictionary<string, WhatsAppFlowDefinition> _flows = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<FlowSessionTracking> _sessions = new();

    public FlowInteractiveEngine()
    {
        SeedFlows();
    }

    private void SeedFlows()
    {
        var screen1 = new WhatsAppFlowScreen(
            ScreenId: "SCREEN_LEAD_INFO",
            Title: "Enterprise Consultation Booking",
            LayoutType: "FormLayout",
            Components: new List<WhatsAppFlowComponent>
            {
                new("TextInput", "company_name", "Company Legal Name", true),
                new("Dropdown", "team_size", "Organization Team Size", true, new[] { "1-50", "51-200", "201-1000", "1000+" }),
                new("DatePicker", "preferred_date", "Preferred Consultation Date", true)
            }
        );

        var screen2 = new WhatsAppFlowScreen(
            ScreenId: "SCREEN_USE_CASE",
            Title: "Select Primary Growth Objective",
            LayoutType: "SingleChoiceList",
            Components: new List<WhatsAppFlowComponent>
            {
                new("RadioGroup", "primary_goal", "Primary Goal", true, new[] { "Autonomous Voice SDR", "Cross-Border Tax & Trade", "High-Throughput WhatsApp Engine" })
            },
            TerminalAction: "SUBMIT_FLOW"
        );

        var flow1 = new WhatsAppFlowDefinition(
            FlowId: "flow_vip_booking_v3",
            FlowName: "VIP Enterprise Demo & Consultation Flow",
            Category: "LEAD_GENERATION",
            InitialScreenId: screen1.ScreenId,
            Screens: new[] { screen1, screen2 },
            Status: "Published",
            Version: "3.0",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-12)
        );

        _flows[flow1.FlowId] = flow1;

        _sessions.Add(new FlowSessionTracking("sess_01", flow1.FlowId, "+14155558912", "SUCCESS", true, DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-3).AddMinutes(2)));
        _sessions.Add(new FlowSessionTracking("sess_02", flow1.FlowId, "+44791112999", "SUCCESS", true, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-2).AddMinutes(1)));
        _sessions.Add(new FlowSessionTracking("sess_03", flow1.FlowId, "+49151234567", "SCREEN_USE_CASE", false, DateTime.UtcNow.AddHours(-1)));
    }

    public Task<IReadOnlyList<WhatsAppFlowDefinition>> GetFlowsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<WhatsAppFlowDefinition>>(_flows.Values.ToList());
    }

    public Task<WhatsAppFlowDefinition> GetFlowByIdAsync(string flowId, CancellationToken ct = default)
    {
        if (!_flows.TryGetValue(flowId, out var flow))
        {
            throw new KeyNotFoundException($"WhatsApp flow '{flowId}' not found.");
        }
        return Task.FromResult(flow);
    }

    public Task<FlowDataExchangeResponse> ProcessDataExchangeAsync(FlowDataExchangeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_flows.TryGetValue(request.FlowId, out var flow))
        {
            throw new KeyNotFoundException($"WhatsApp flow '{request.FlowId}' not found.");
        }

        var currentScreen = flow.Screens.FirstOrDefault(s => s.ScreenId.Equals(request.ScreenId, StringComparison.OrdinalIgnoreCase))
            ?? flow.Screens[0];

        // Determine transition based on screen and action
        string? nextScreen = null;
        var responsePayload = new Dictionary<string, string>(request.DataPayload);

        if (currentScreen.ScreenId == "SCREEN_LEAD_INFO")
        {
            nextScreen = "SCREEN_USE_CASE";
            responsePayload["step_completed"] = "lead_info_saved";
        }
        else if (currentScreen.ScreenId == "SCREEN_USE_CASE" || currentScreen.TerminalAction == "SUBMIT_FLOW")
        {
            nextScreen = "SUCCESS_SCREEN";
            responsePayload["status"] = "CONFIRMED_BOOKED";
            responsePayload["calendar_event_id"] = $"evt_{Guid.NewGuid():N}";

            _sessions.Add(new FlowSessionTracking(
                SessionId: $"sess_{Guid.NewGuid():N}",
                FlowId: flow.FlowId,
                UserPhone: request.DataPayload.TryGetValue("phone", out var p) ? p : "+14155550000",
                CurrentScreenId: "SUCCESS_SCREEN",
                IsCompleted: true,
                StartedAtUtc: DateTime.UtcNow.AddMinutes(-2),
                CompletedAtUtc: DateTime.UtcNow
            ));
        }

        var response = new FlowDataExchangeResponse(
            ScreenId: currentScreen.ScreenId,
            DataPayload: responsePayload,
            NextScreenId: nextScreen
        );

        return Task.FromResult(response);
    }

    public Task<FlowAnalyticsSummary> GetAnalyticsAsync(CancellationToken ct = default)
    {
        var sessions = _sessions.ToList();
        var total = sessions.Count;
        var completed = sessions.Count(s => s.IsCompleted);
        var rate = total > 0 ? Math.Round(((double)completed / total) * 100, 1) : 0;

        var summary = new FlowAnalyticsSummary(
            TotalPublishedFlows: _flows.Count,
            TotalSessionsStarted: total,
            TotalCompletedSessions: completed,
            FunnelCompletionRatePercent: rate,
            HighestDropOffScreen: "SCREEN_USE_CASE"
        );

        return Task.FromResult(summary);
    }
}
