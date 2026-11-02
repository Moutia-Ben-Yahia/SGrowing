namespace SGrowing.Api.Modules.Dev.WebhookMesh;

public static class BiDirectionalWebhookMeshEndpoints
{
    public static IServiceCollection AddBiDirectionalWebhookMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<IWebhookMeshEngine, WebhookMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBiDirectionalWebhookMeshEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dev/webhooks")
            .WithTags("Bi-Directional Webhook Event Mesh & Real-Time Replay Debugger");

        group.MapGet("/subscriptions", (IWebhookMeshEngine engine) =>
        {
            var subs = engine.ListSubscriptions();
            return Results.Ok(subs);
        })
        .WithName("ListWebhookSubscriptions")
        .WithSummary("List all active external webhook subscriptions and event triggers");

        group.MapPost("/subscriptions", (IWebhookMeshEngine engine, CreateSubscriptionRequest request) =>
        {
            var sub = engine.CreateSubscription(request);
            return Results.Created($"/api/v1/dev/webhooks/subscriptions/{sub.SubscriptionId}", sub);
        })
        .WithName("CreateWebhookSubscription")
        .WithSummary("Register an external webhook endpoint with automated HMAC-SHA256 signing");

        group.MapGet("/deliveries", (IWebhookMeshEngine engine) =>
        {
            var deliveries = engine.ListDeliveryHistory();
            return Results.Ok(deliveries);
        })
        .WithName("ListWebhookDeliveries")
        .WithSummary("Retrieve live outbound delivery log with response codes, signatures, and latency");

        group.MapPost("/trigger", (IWebhookMeshEngine engine, TriggerWebhookEventRequest request) =>
        {
            var attempts = engine.TriggerEvent(request);
            return Results.Ok(attempts);
        })
        .WithName("TriggerWebhookEvent")
        .WithSummary("Dispatch an event across all subscribed external endpoints");

        group.MapPost("/replay", (IWebhookMeshEngine engine, ReplayDeliveryRequest request) =>
        {
            try
            {
                var replayed = engine.ReplayDelivery(request);
                return Results.Ok(replayed);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("ReplayWebhookDelivery")
        .WithSummary("Replay or mutate a failed webhook delivery attempt directly from the inspector");

        group.MapGet("/dlq", (IWebhookMeshEngine engine) =>
        {
            var dlq = engine.GetDlqSummary();
            return Results.Ok(dlq);
        })
        .WithName("GetWebhookDlqSummary")
        .WithSummary("Inspect exhausted dead-letter queue messages awaiting manual review or bulk redelivery");

        return app;
    }
}
