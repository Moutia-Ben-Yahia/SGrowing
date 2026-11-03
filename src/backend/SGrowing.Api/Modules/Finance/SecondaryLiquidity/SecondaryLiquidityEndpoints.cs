namespace SGrowing.Api.Modules.Finance.SecondaryLiquidity;

public static class SecondaryLiquidityEndpoints
{
    public static IServiceCollection AddSecondaryLiquidityServices(this IServiceCollection services)
    {
        services.AddSingleton<ISecondaryLiquidityEngine, SecondaryLiquidityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSecondaryLiquidityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/finance/secondary-liquidity")
            .WithTags("Finance - Cap Table Secondary Liquidity & Tender Offers");

        group.MapGet("/overview", (ISecondaryLiquidityEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/create-tender-offer", (CreateTenderOfferRequest req, ISecondaryLiquidityEngine engine) =>
        {
            var offer = engine.CreateTenderOffer(req.ProgramName, req.MaxVolumeUsd, req.SharePriceUsd, req.DurationDays);
            return Results.Ok(offer);
        });

        group.MapPost("/submit-lot", (SubmitShareLotRequest req, ISecondaryLiquidityEngine engine) =>
        {
            var lot = engine.SubmitShareLot(req.OfferId, req.ShareholderId, req.ShareholderName, req.SharesCount);
            return Results.Ok(lot);
        });

        group.MapPost("/process-rofr", (ProcessRofrRequest req, ISecondaryLiquidityEngine engine) =>
        {
            var token = engine.ProcessRofrClearance(req.LotId, req.CompanyExercises);
            return Results.Ok(token);
        });

        group.MapPost("/simulate-dilution", (SimulateDilutionRequest req, ISecondaryLiquidityEngine engine) =>
        {
            var sim = engine.SimulateDilution(req.NewSharesIssued, req.TotalPreShares);
            return Results.Ok(sim);
        });

        return endpoints;
    }
}

public sealed record CreateTenderOfferRequest(string ProgramName, double MaxVolumeUsd, double SharePriceUsd, int DurationDays);
public sealed record SubmitShareLotRequest(string OfferId, string ShareholderId, string ShareholderName, int SharesCount);
public sealed record ProcessRofrRequest(string LotId, bool CompanyExercises);
public sealed record SimulateDilutionRequest(double NewSharesIssued, double TotalPreShares);
