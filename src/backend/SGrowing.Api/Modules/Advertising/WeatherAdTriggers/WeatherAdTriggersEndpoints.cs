namespace SGrowing.Api.Modules.Advertising.WeatherAdTriggers;

public static class WeatherAdTriggersEndpoints
{
    public static IServiceCollection AddWeatherAdTriggersServices(this IServiceCollection services)
    {
        services.AddSingleton<WeatherAdTriggersEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWeatherAdTriggersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/weather-triggers")
            .WithTags("Advertising - Dynamic DPA Weather & Environmental Trigger Engine");

        group.MapGet("/overview", (WeatherAdTriggersEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/trigger-campaign", (WeatherAdTriggersEngine engine, TriggerCampaignRequest request) =>
        {
            var rule = engine.TriggerCampaign(request);
            return Results.Created($"/api/v1/advertising/weather-triggers/rules/{rule.RuleId}", rule);
        });

        group.MapPost("/record-observation", (WeatherAdTriggersEngine engine, RecordWeatherObservationRequest request) =>
        {
            var obs = engine.RecordWeatherObservation(request);
            return Results.Created($"/api/v1/advertising/weather-triggers/observations/{obs.ObservationId}", obs);
        });

        return app;
    }
}
