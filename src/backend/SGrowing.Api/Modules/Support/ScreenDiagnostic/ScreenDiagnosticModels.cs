using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Support.ScreenDiagnostic;

public record BoundingBox(
    int X,
    int Y,
    int Width,
    int Height
);

public record DetectedUiElement(
    string ElementId,
    string ElementType, // "Button", "TextInput", "Dropdown", "Modal", "Toast"
    BoundingBox Box,
    double ConfidenceScore,
    string LabelText
);

public record CursorTrajectoryWaypoint(
    int StepIndex,
    int TargetX,
    int TargetY,
    string InterpolationType, // "CubicBezier", "Linear"
    string Action, // "Move", "Hover", "Click", "DoubleClick", "TextInput"
    string InstructionNarrative
);

public record ScreenDiagnosticSession(
    string SessionId,
    string CustomerId,
    string ActiveUrl,
    string IssueCategory, // "OAuthConfiguration", "WebhookSecrets", "BillingCardUpdate", "PermissionDenied"
    string DetectedStickingPoint,
    double ResolutionProgressPct,
    string Status // "Diagnosing", "GuidingCursor", "Resolved"
);

public record ScreenDiagnosticOverview(
    int ActiveSessionsCount,
    int AverageResolutionTimeSeconds,
    double ElementDetectionAccuracyPct,
    double AutonomousGuidanceSuccessPct,
    IReadOnlyList<ScreenDiagnosticSession> Sessions,
    IReadOnlyList<DetectedUiElement> HighlightedElements
);

public record AnalyzeFrameCommand(
    string SessionId,
    string FrameHash,
    int ViewportWidth,
    int ViewportHeight
);

public record AnalyzeFrameResult(
    string AnalysisId,
    string SessionId,
    IReadOnlyList<DetectedUiElement> DetectedElements,
    string RecommendedAction,
    int ConfidencePct
);

public record GenerateTrajectoryCommand(
    string SessionId,
    int StartX,
    int StartY,
    string TargetElementId
);

public record GenerateTrajectoryResult(
    string TrajectoryId,
    string SessionId,
    string TargetElementId,
    int TotalDurationMs,
    IReadOnlyList<CursorTrajectoryWaypoint> Waypoints
);
