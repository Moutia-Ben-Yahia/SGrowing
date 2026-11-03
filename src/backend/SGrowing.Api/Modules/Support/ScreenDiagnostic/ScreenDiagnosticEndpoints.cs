using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Support.ScreenDiagnostic;

public static class ScreenDiagnosticEndpoints
{
    public static IServiceCollection AddScreenDiagnosticServices(this IServiceCollection services)
    {
        services.AddSingleton<IScreenDiagnosticEngine, ScreenDiagnosticEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapScreenDiagnosticEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/support/screen-diagnostic")
            .WithTags("Support - Screen Diagnostic & Cursor Guidance");

        group.MapGet("/overview", (IScreenDiagnosticEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetScreenDiagnosticOverview")
        .WithSummary("Displays active video screen sessions, UI element detection accuracy, and guidance completion rate");

        group.MapPost("/analyze-frame", (AnalyzeFrameCommand command, IScreenDiagnosticEngine engine) =>
        {
            var result = engine.AnalyzeFrame(command);
            return Results.Ok(result);
        })
        .WithName("AnalyzeScreenFrame")
        .WithSummary("Analyzes a screen frame, detects UI elements, flags customer sticking points, and computes next cursor waypoint");

        group.MapPost("/generate-trajectory", (GenerateTrajectoryCommand command, IScreenDiagnosticEngine engine) =>
        {
            var result = engine.GenerateTrajectory(command);
            return Results.Ok(result);
        })
        .WithName("GenerateCursorTrajectory")
        .WithSummary("Computes natural cubic Bezier cursor trajectory points for visual co-pilot overlay");

        return app;
    }
}
