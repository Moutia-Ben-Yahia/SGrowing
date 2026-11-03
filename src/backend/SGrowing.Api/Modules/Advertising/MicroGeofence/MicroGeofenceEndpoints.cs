namespace SGrowing.Api.Modules.Advertising.MicroGeofence;

public static class MicroGeofenceEndpoints
{
    public static IServiceCollection AddMicroGeofenceServices(this IServiceCollection services)
    {
        services.AddSingleton<MicroGeofenceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMicroGeofenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/micro-geofence")
            .WithTags("Advertising - Micro-Geofencing & Footfall DSP Bidding Mesh");

        group.MapGet("/overview", (MicroGeofenceEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/define-polygon", (MicroGeofenceEngine engine, DefinePolygonRequest request) =>
        {
            var poly = engine.DefinePolygon(request);
            return Results.Created($"/api/v1/advertising/micro-geofence/polygons/{poly.PolygonId}", poly);
        });

        group.MapPost("/record-footfall", (MicroGeofenceEngine engine, RecordFootfallRequest request) =>
        {
            var evt = engine.RecordFootfall(request);
            return Results.Created($"/api/v1/advertising/micro-geofence/events/{evt.EventId}", evt);
        });

        return app;
    }
}
