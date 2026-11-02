namespace SGrowing.Api.Modules.Growth.Affiliates;

public static class AffiliateLoyaltyEndpoints
{
    public static IServiceCollection AddAffiliateLoyaltyServices(this IServiceCollection services)
    {
        services.AddSingleton<IAffiliateLoyaltyEngine, AffiliateLoyaltyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAffiliateLoyaltyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/growth/affiliates")
            .WithTags("Viral Referral, Tiered Customer Loyalty & Multi-Tier Affiliate Network");

        group.MapGet("/", (IAffiliateLoyaltyEngine engine) =>
        {
            var partners = engine.ListAffiliates();
            return Results.Ok(partners);
        })
        .WithName("ListAffiliatePartners")
        .WithSummary("List all registered affiliate partners, commission rates, and pending payouts");

        group.MapPost("/", (IAffiliateLoyaltyEngine engine, CreateAffiliateRequest request) =>
        {
            var partner = engine.RegisterAffiliate(request);
            return Results.Created($"/api/v1/growth/affiliates/{partner.PartnerId}", partner);
        })
        .WithName("RegisterAffiliatePartner")
        .WithSummary("Register a new affiliate partner with custom Tier 1 and Tier 2 commission splits");

        group.MapPost("/track-conversion", (IAffiliateLoyaltyEngine engine, TrackConversionRequest request) =>
        {
            try
            {
                var result = engine.ProcessReferralConversion(request);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .WithName("TrackReferralConversion")
        .WithSummary("Process an order referral conversion, calculate multi-tier commissions, and credit loyalty points");

        group.MapGet("/loyalty/{email}", (IAffiliateLoyaltyEngine engine, string email) =>
        {
            var loyalty = engine.GetOrCreateLoyaltyAccount(email);
            return Results.Ok(loyalty);
        })
        .WithName("GetLoyaltyAccountByEmail")
        .WithSummary("Retrieve customer loyalty points ledger and current tier status");

        group.MapGet("/audit-fraud", (IAffiliateLoyaltyEngine engine) =>
        {
            var report = engine.RunAntiFraudAudit();
            return Results.Ok(report);
        })
        .WithName("AuditAffiliateFraud")
        .WithSummary("Execute graph cycle detection and self-referral ring audits");

        return app;
    }
}
