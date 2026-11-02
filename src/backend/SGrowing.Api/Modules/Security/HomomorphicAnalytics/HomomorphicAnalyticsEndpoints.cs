using System.Numerics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security.HomomorphicAnalytics;

public static class HomomorphicAnalyticsEndpoints
{
    public static IServiceCollection AddHomomorphicAnalyticsServices(this IServiceCollection services)
    {
        services.AddSingleton<PaillierHomomorphicAnalyticsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHomomorphicAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/homomorphic-analytics")
            .WithTags("Autonomous Privacy-Preserving Homomorphic Encrypted Analytics (Paillier Cryptosystem)");

        group.MapPost("/generate-keys", (
            GenerateKeysRequest request,
            PaillierHomomorphicAnalyticsEngine engine) =>
        {
            var bits = request.KeyBits is >= 128 and <= 1024 ? request.KeyBits : 256;
            var (pub, priv) = engine.GenerateKeyPair(bits);
            return Results.Ok(new { PublicKey = pub, PrivateKey = priv });
        });

        group.MapPost("/aggregate-sum", (
            HomomorphicSumAggregationRequest request,
            PaillierHomomorphicAnalyticsEngine engine) =>
        {
            var ciphertexts = request.CiphertextsHex
                .Select(h => BigInteger.Parse(h, System.Globalization.NumberStyles.HexNumber));

            var aggregate = engine.AggregateSum(ciphertexts, request.PublicKey);

            return Results.Ok(new HomomorphicSumAggregationResponse(
                AggregateCiphertextHex: aggregate.ToString("X"),
                ItemsAggregated: request.CiphertextsHex.Count,
                ExecutedAtUtc: DateTime.UtcNow
            ));
        });

        group.MapPost("/decrypt-aggregate", (
            DecryptQueryRequest request,
            PaillierHomomorphicAnalyticsEngine engine) =>
        {
            var aggregateBigInt = BigInteger.Parse(request.AggregateCiphertextHex, System.Globalization.NumberStyles.HexNumber);
            var sum = engine.Decrypt(aggregateBigInt, request.PrivateKey);
            var avg = request.TotalItems > 0 ? (double)sum / request.TotalItems : 0.0;

            return Results.Ok(new DecryptQueryResponse(
                DecryptedSum: sum,
                DecryptedAverage: avg,
                RecordCount: request.TotalItems,
                DecryptedAtUtc: DateTime.UtcNow
            ));
        });

        return app;
    }
}

public sealed record GenerateKeysRequest(int KeyBits = 256);
