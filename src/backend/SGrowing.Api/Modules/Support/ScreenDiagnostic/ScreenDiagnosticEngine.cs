using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Support.ScreenDiagnostic;

public interface IScreenDiagnosticEngine
{
    ScreenDiagnosticOverview GetOverview();
    AnalyzeFrameResult AnalyzeFrame(AnalyzeFrameCommand command);
    GenerateTrajectoryResult GenerateTrajectory(GenerateTrajectoryCommand command);
}

public class ScreenDiagnosticEngine : IScreenDiagnosticEngine
{
    private readonly List<ScreenDiagnosticSession> _sessions =
    [
        new("SESS-SCR-401", "CUST-ACME-88", "https://app.sgrowing.io/settings/integrations", "OAuthConfiguration", "Missing Redirect URI Callback", 65.0, "GuidingCursor"),
        new("SESS-SCR-402", "CUST-STRIPE-12", "https://app.sgrowing.io/settings/billing", "BillingCardUpdate", "Zip Code Verification Mismatch", 80.0, "GuidingCursor"),
        new("SESS-SCR-403", "CUST-NEXUS-99", "https://app.sgrowing.io/security/api-keys", "WebhookSecrets", "Unchecked Webhook Signing Secret", 35.0, "Diagnosing")
    ];

    private readonly List<DetectedUiElement> _detectedElements =
    [
        new("UI-EL-01", "TextInput", new BoundingBox(320, 240, 400, 48), 0.98, "OAuth Redirect URI Field"),
        new("UI-EL-02", "Button", new BoundingBox(740, 240, 120, 48), 0.96, "Save Callback URL"),
        new("UI-EL-03", "Dropdown", new BoundingBox(320, 310, 260, 44), 0.94, "Grant Type Scopes"),
        new("UI-EL-04", "Toast", new BoundingBox(960, 40, 300, 60), 0.99, "Invalid Client Secret Warning")
    ];

    public ScreenDiagnosticOverview GetOverview()
    {
        var activeCount = _sessions.Count;
        var avgResolutionTime = 95; // seconds
        var detectionAccuracy = 97.4;
        var guidanceSuccess = 92.8;

        return new ScreenDiagnosticOverview(
            activeCount,
            avgResolutionTime,
            detectionAccuracy,
            guidanceSuccess,
            _sessions,
            _detectedElements
        );
    }

    public AnalyzeFrameResult AnalyzeFrame(AnalyzeFrameCommand command)
    {
        var analysisId = $"ANLZ-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var recommended = "Direct synthetic cursor to 'Save Callback URL' button to resolve authorization hang";

        return new AnalyzeFrameResult(
            analysisId,
            command.SessionId,
            _detectedElements,
            recommended,
            96
        );
    }

    public GenerateTrajectoryResult GenerateTrajectory(GenerateTrajectoryCommand command)
    {
        var trajectoryId = $"TRAJ-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var waypoints = new List<CursorTrajectoryWaypoint>
        {
            new(1, command.StartX, command.StartY, "Linear", "Move", "Acquire initial cursor position"),
            new(2, command.StartX + (800 - command.StartX) / 2, command.StartY + 40, "CubicBezier", "Move", "Smooth arc toward target button"),
            new(3, 800, 264, "CubicBezier", "Hover", "Highlight 'Save Callback URL' with halo pulse"),
            new(4, 800, 264, "Linear", "Click", "Trigger synthetic click confirmation")
        };

        return new GenerateTrajectoryResult(
            trajectoryId,
            command.SessionId,
            command.TargetElementId,
            850, // ms duration
            waypoints
        );
    }
}
