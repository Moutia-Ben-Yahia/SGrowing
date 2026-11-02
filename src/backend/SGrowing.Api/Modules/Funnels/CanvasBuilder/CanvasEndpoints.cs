namespace SGrowing.Api.Modules.Funnels.CanvasBuilder;

public static class CanvasEndpoints
{
    public static IServiceCollection AddCanvasBuilderServices(this IServiceCollection services)
    {
        services.AddSingleton<ICanvasEngine, CanvasEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCanvasBuilderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/funnels/canvas")
            .WithTags("Drag-and-Drop Landing Page & Funnel Canvas Builder");

        group.MapGet("/", (ICanvasEngine engine) =>
        {
            var canvases = engine.ListCanvases();
            return Results.Ok(canvases);
        })
        .WithName("ListFunnelCanvases")
        .WithSummary("List all saved landing page and funnel visual canvases");

        group.MapGet("/{id}", (ICanvasEngine engine, string id) =>
        {
            var canvas = engine.GetCanvas(id);
            return canvas is not null ? Results.Ok(canvas) : Results.NotFound();
        })
        .WithName("GetFunnelCanvasById")
        .WithSummary("Get a specific funnel canvas element tree and theme tokens");

        group.MapPost("/", (ICanvasEngine engine, SaveCanvasRequest request) =>
        {
            var canvas = engine.SaveCanvas(request);
            return Results.Created($"/api/v1/funnels/canvas/{canvas.CanvasId}", canvas);
        })
        .WithName("SaveFunnelCanvas")
        .WithSummary("Save or publish a visual funnel canvas with theme tokens and block hierarchy");

        group.MapPost("/compile", (ICanvasEngine engine, CompileHtmlRequest request) =>
        {
            try
            {
                var result = engine.CompileHtml(request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("CompileCanvasHtml")
        .WithSummary("Compile visual canvas nodes into clean, responsive HTML for desktop, tablet, or mobile viewports");

        return app;
    }
}
