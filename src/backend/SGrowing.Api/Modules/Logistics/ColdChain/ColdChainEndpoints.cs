using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.ColdChain;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 42:
/// Real-Time Decentralized Oracle Network, Satellite Geo-Fence & Cargo Cold-Chain Verification.
/// </summary>
public static class ColdChainEndpoints
{
    /// <summary>
    /// Registers all Phase 42 Decentralized Oracle Network, Satellite Geo-Fence, and Parametric Insurance services.
    /// </summary>
    public static IServiceCollection AddColdChainServices(this IServiceCollection services)
    {
        services.AddSingleton<IColdChainTelemetryOracleEngine, ColdChainTelemetryOracleEngine>();
        services.AddSingleton<ISatelliteGeofenceTrackerService, SatelliteGeofenceTrackerService>();
        services.AddSingleton<IParametricCargoInsuranceLiquidator, ParametricCargoInsuranceLiquidator>();

        return services;
    }

    /// <summary>
    /// Maps the Phase 42 Cold Chain Minimal API endpoints under /api/v1/logistics/coldchain.
    /// </summary>
    public static IEndpointRouteBuilder MapColdChainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/coldchain")
            .WithTags("Decentralized Oracle Network & Cargo Cold-Chain");

        // 1. POST /api/v1/logistics/coldchain/telemetry/ingest
        group.MapPost("/telemetry/ingest", (
            [FromBody] IngestColdChainTelemetryRequest request,
            [FromServices] IColdChainTelemetryOracleEngine oracleEngine) =>
        {
            try
            {
                var response = oracleEngine.IngestTelemetry(request.Point);
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("IngestColdChainTelemetry")
        .WithSummary("Ingests high-frequency environmental telemetry point from IoT container sensor.");

        // 2. POST /api/v1/logistics/coldchain/telemetry/ingest/batch
        group.MapPost("/telemetry/ingest/batch", (
            [FromBody] BatchIngestColdChainTelemetryRequest request,
            [FromServices] IColdChainTelemetryOracleEngine oracleEngine) =>
        {
            try
            {
                var responses = oracleEngine.IngestBatch(request.Points);
                return Results.Ok(responses);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("IngestColdChainBatch")
        .WithSummary("Ingests a batch of environmental telemetry points.");

        // 3. GET /api/v1/logistics/coldchain/shipments/{consignmentId}/status
        group.MapGet("/shipments/{consignmentId}/status", (
            [FromRoute] string consignmentId,
            [FromQuery] CargoType? cargoType,
            [FromServices] IColdChainTelemetryOracleEngine oracleEngine,
            [FromServices] ISatelliteGeofenceTrackerService geofenceTracker,
            [FromServices] IParametricCargoInsuranceLiquidator liquidator) =>
        {
            var history = oracleEngine.GetTelemetryHistory(consignmentId);
            if (history.Count == 0)
            {
                return Results.NotFound(new { Message = $"Consignment '{consignmentId}' has no recorded cold-chain telemetry." });
            }

            var profile = CargoThermalProfile.ForCargoType(cargoType ?? CargoType.VaccinesPharma);
            var mktResult = oracleEngine.CalculateMkt(consignmentId, profile);
            var latestPoint = oracleEngine.GetLatestTelemetryPoint(consignmentId);
            var transitions = geofenceTracker.GetTransitionHistory(consignmentId);
            var claims = liquidator.GetClaimsForConsignment(consignmentId);
            var hasLightBreach = oracleEngine.HasLightExposureBreach(consignmentId);
            var hasShockBreach = oracleEngine.HasShockBreach(consignmentId);

            var response = new ConsignmentColdChainStatusResponse(
                ConsignmentId: consignmentId,
                TotalTelemetryPoints: history.Count,
                LatestPoint: latestPoint,
                MktResult: mktResult,
                Transitions: transitions,
                Claims: claims,
                HasLightBreach: hasLightBreach,
                HasShockBreach: hasShockBreach);

            return Results.Ok(response);
        })
        .WithName("GetConsignmentColdChainStatus")
        .WithSummary("Retrieves complete environmental telemetry, MKT status, geofence border crossings, and parametric claims for a consignment.");

        // 4. POST /api/v1/logistics/coldchain/geofences
        group.MapPost("/geofences", (
            [FromBody] RegisterGeofenceRequest request,
            [FromServices] ISatelliteGeofenceTrackerService geofenceTracker) =>
        {
            try
            {
                var geofence = new PolygonalGeofence(
                    request.GeofenceId,
                    request.Name,
                    request.Type,
                    request.Coordinates,
                    request.IsActive);

                geofenceTracker.RegisterGeofence(geofence);
                return Results.Created($"/api/v1/logistics/coldchain/geofences/{geofence.GeofenceId}", geofence);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("RegisterSatelliteGeofence")
        .WithSummary("Registers a satellite polygonal geo-fence corridor or customs bonded zone.");

        // 5. POST /api/v1/logistics/coldchain/geofences/evaluate
        group.MapPost("/geofences/evaluate", (
            [FromBody] EvaluateCoordinateRequest request,
            [FromServices] ISatelliteGeofenceTrackerService geofenceTracker) =>
        {
            try
            {
                var transitions = geofenceTracker.EvaluateCoordinate(
                    request.ConsignmentId,
                    request.Latitude,
                    request.Longitude,
                    request.TimestampUtc ?? DateTime.UtcNow,
                    request.SecretKey);

                return Results.Ok(transitions);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("EvaluateGeofenceCoordinate")
        .WithSummary("Evaluates GPS/satellite coordinates against active geo-fences, generating cryptographic border crossing proofs.");

        // 6. POST /api/v1/logistics/coldchain/claims/parametric-liquidate
        group.MapPost("/claims/parametric-liquidate", (
            [FromBody] FileParametricClaimRequest request,
            [FromServices] IParametricCargoInsuranceLiquidator liquidator) =>
        {
            try
            {
                var profile = CargoThermalProfile.ForCargoType(request.CargoType);
                var claim = liquidator.ProcessTelemetryForClaims(
                    request.ConsignmentId,
                    profile,
                    request.PolicyPayoutDzd,
                    request.RecipientId,
                    request.SecretKey,
                    request.PolicyNumber);

                return Results.Ok(claim);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        })
        .WithName("ParametricClaimLiquidate")
        .WithSummary("Executes zero-touch autonomous liquidation of parametric cargo insurance claim.");

        // 7. GET /api/v1/logistics/coldchain/claims/{claimId}
        group.MapGet("/claims/{claimId}", (
            [FromRoute] string claimId,
            [FromServices] IParametricCargoInsuranceLiquidator liquidator) =>
        {
            var claim = liquidator.GetClaim(claimId);
            if (claim is null)
            {
                return Results.NotFound(new { Message = $"Parametric claim '{claimId}' was not found." });
            }

            return Results.Ok(claim);
        })
        .WithName("GetParametricClaim")
        .WithSummary("Retrieves a parametric cargo insurance claim and its cryptographic liquidation certificate.");

        return app;
    }
}
