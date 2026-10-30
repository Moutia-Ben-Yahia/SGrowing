using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Security;

/// <summary>
/// Request DTO for executing interactive idempotency test simulations.
/// </summary>
public record SimulateIdempotencyRequest(
    string? TenantId,
    string? IdempotencyKey,
    string? Method,
    string? Path,
    string? BodyJson,
    string? Mode // "normal", "replay", "conflict", "mismatch"
);

/// <summary>
/// Detailed response DTO returned by the idempotency test simulator.
/// </summary>
public record SimulateIdempotencyResponse(
    int StatusCode,
    bool Success,
    bool ReplayDetected,
    string Status,
    string Message,
    string? Fingerprint,
    IReadOnlyDictionary<string, string> Headers,
    string? ResponseBody,
    int ReplayCount,
    DateTimeOffset? ExpiresAt,
    long ExecutionDurationMs,
    IdempotencyMetricsDto Metrics
);

/// <summary>
/// Response DTO for key invalidation.
/// </summary>
public record InvalidateKeyResponse(
    bool Success,
    string Key,
    string Message
);

/// <summary>
/// Ledger query response with records and metrics.
/// </summary>
public record LedgerListResponse(
    IReadOnlyList<IdempotencyRecordDto> Records,
    int TotalCount,
    IdempotencyMetricsDto Metrics
);

/// <summary>
/// Minimal API endpoints for Distributed Multi-Tenant Idempotency Key & Safe Replay Ledger.
/// </summary>
public static class IdempotencyEndpoints
{
    public static IEndpointRouteBuilder MapIdempotencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/security/idempotency")
            .WithTags("Distributed Multi-Tenant Idempotency Key & Safe Replay Ledger");

        IDistributedIdempotencyService ResolveService(HttpContext ctx, IDistributedIdempotencyService? injected) =>
            injected ?? ctx.RequestServices.GetService<IDistributedIdempotencyService>() ?? DistributedIdempotencyService.DefaultInstance;

        // 1. GET /ledger - List idempotency records with pagination and filtering
        group.MapGet("/ledger", async (
            HttpContext ctx,
            [FromQuery] string? tenantId,
            [FromQuery] string? status,
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0,
            [FromServices] IDistributedIdempotencyService? svc = null) =>
        {
            var service = ResolveService(ctx, svc);
            var safeTenant = tenantId ?? "tenant-default";
            var records = await service.GetRecordsAsync(safeTenant, status, limit, offset);
            var metrics = await service.GetMetricsAsync(safeTenant);

            return Results.Ok(new LedgerListResponse(
                Records: records,
                TotalCount: records.Count,
                Metrics: metrics
            ));
        });

        // 2. GET /metrics - Deduplication and replay hit rate telemetry
        group.MapGet("/metrics", async (
            HttpContext ctx,
            [FromQuery] string? tenantId,
            [FromServices] IDistributedIdempotencyService? svc = null) =>
        {
            var service = ResolveService(ctx, svc);
            var metrics = await service.GetMetricsAsync(tenantId ?? "tenant-default");
            return Results.Ok(metrics);
        });

        // 3. POST /simulate - Interactive test simulator executing idempotency checks
        group.MapPost("/simulate", async (
            HttpContext ctx,
            [FromBody] SimulateIdempotencyRequest req,
            [FromServices] IDistributedIdempotencyService? svc = null) =>
        {
            var service = ResolveService(ctx, svc);
            var sw = Stopwatch.StartNew();

            var tenant = string.IsNullOrWhiteSpace(req.TenantId) ? "tenant-default" : req.TenantId.Trim();
            var key = string.IsNullOrWhiteSpace(req.IdempotencyKey)
                ? $"idem_tx_{Guid.NewGuid():N}"[..20]
                : req.IdempotencyKey.Trim();
            var method = string.IsNullOrWhiteSpace(req.Method) ? "POST" : req.Method.Trim().ToUpperInvariant();
            var path = string.IsNullOrWhiteSpace(req.Path) ? "/api/v1/payments/charges" : req.Path.Trim();
            var body = req.BodyJson ?? "{\"amount\": 9900, \"currency\": \"USD\", \"description\": \"Standard checkout item\"}";
            var mode = req.Mode?.Trim().ToLowerInvariant() ?? "normal";

            var headers = new Dictionary<string, string>
            {
                ["X-Idempotency-Key"] = key,
                ["X-Tenant-Id"] = tenant,
                ["Content-Type"] = "application/json"
            };

            // Handling Modes
            if (mode == "conflict")
            {
                // Simulate concurrent in-flight conflict:
                // First ensure key is in InProgress status
                await service.InvalidateKeyAsync(tenant, key);
                await service.TryAcquireLockAsync(tenant, key, method, path, body);

                // Second call while InProgress -> 409 Conflict
                var conflictResult = await service.TryAcquireLockAsync(tenant, key, method, path, body);
                sw.Stop();

                var metricsAfterConflict = await service.GetMetricsAsync(tenant);
                return Results.Json(new SimulateIdempotencyResponse(
                    StatusCode: 409,
                    Success: false,
                    ReplayDetected: false,
                    Status: "InProgress",
                    Message: conflictResult.Message,
                    Fingerprint: conflictResult.Fingerprint,
                    Headers: headers,
                    ResponseBody: JsonSerializer.Serialize(new
                    {
                        error = "conflict",
                        status = 409,
                        message = "Concurrent double-submit prevented. Request with this idempotency key is already actively executing.",
                        idempotency_key = key,
                        lock_expires_at = conflictResult.ExpiresAt
                    }),
                    ReplayCount: 0,
                    ExpiresAt: conflictResult.ExpiresAt,
                    ExecutionDurationMs: sw.ElapsedMilliseconds,
                    Metrics: metricsAfterConflict
                ), statusCode: 409);
            }

            if (mode == "mismatch")
            {
                // Ensure key exists as Completed with original body
                var originalBody = "{\"amount\": 5000, \"currency\": \"USD\", \"intent\": \"original_payment\"}";
                await service.InvalidateKeyAsync(tenant, key);
                await service.TryAcquireLockAsync(tenant, key, method, path, originalBody);
                await service.RecordCompletedResponseAsync(tenant, key, 200, headers, "{\"status\": \"paid\", \"amount\": 5000}");

                // Now attempt submission with altered/tampered payload
                var tamperedBody = string.IsNullOrWhiteSpace(req.BodyJson)
                    ? "{\"amount\": 999999, \"currency\": \"EUR\", \"intent\": \"tampered_charge\"}"
                    : req.BodyJson;

                var mismatchResult = await service.TryAcquireLockAsync(tenant, key, method, path, tamperedBody);
                sw.Stop();

                var metricsAfterMismatch = await service.GetMetricsAsync(tenant);
                return Results.Json(new SimulateIdempotencyResponse(
                    StatusCode: 422,
                    Success: false,
                    ReplayDetected: false,
                    Status: "PayloadMismatch",
                    Message: mismatchResult.Message,
                    Fingerprint: mismatchResult.Fingerprint,
                    Headers: headers,
                    ResponseBody: JsonSerializer.Serialize(new
                    {
                        error = "unprocessable_entity",
                        status = 422,
                        message = "Payload mismatch detected! Idempotency key reused with modified parameters or body.",
                        idempotency_key = key,
                        incoming_fingerprint = mismatchResult.Fingerprint
                    }),
                    ReplayCount: mismatchResult.ReplayCount,
                    ExpiresAt: mismatchResult.ExpiresAt,
                    ExecutionDurationMs: sw.ElapsedMilliseconds,
                    Metrics: metricsAfterMismatch
                ), statusCode: 422);
            }

            if (mode == "replay")
            {
                // Ensure key was previously recorded as Completed with this body
                var testLock = await service.TryAcquireLockAsync(tenant, key, method, path, body);
                if (!testLock.ReplayDetected)
                {
                    var initialResponse = JsonSerializer.Serialize(new
                    {
                        transaction_id = $"tx_{Guid.NewGuid():N}"[..16],
                        status = "confirmed",
                        amount_cents = 9900,
                        created_at = DateTimeOffset.UtcNow
                    });
                    await service.RecordCompletedResponseAsync(tenant, key, 200, headers, initialResponse);
                }

                // Now execute the replay
                var replayResult = await service.TryAcquireLockAsync(tenant, key, method, path, body);
                sw.Stop();

                headers["X-Idempotency-Replay"] = "true";
                var metricsAfterReplay = await service.GetMetricsAsync(tenant);

                return Results.Json(new SimulateIdempotencyResponse(
                    StatusCode: replayResult.CachedStatusCode ?? 200,
                    Success: true,
                    ReplayDetected: true,
                    Status: "Completed",
                    Message: replayResult.Message,
                    Fingerprint: replayResult.Fingerprint,
                    Headers: headers,
                    ResponseBody: replayResult.CachedResponseBody,
                    ReplayCount: replayResult.ReplayCount,
                    ExpiresAt: replayResult.ExpiresAt,
                    ExecutionDurationMs: sw.ElapsedMilliseconds,
                    Metrics: metricsAfterReplay
                ), statusCode: replayResult.CachedStatusCode ?? 200);
            }

            // Normal mode
            var lockResult = await service.TryAcquireLockAsync(tenant, key, method, path, body);

            if (lockResult.ReplayDetected)
            {
                sw.Stop();
                headers["X-Idempotency-Replay"] = "true";
                var metrics = await service.GetMetricsAsync(tenant);

                return Results.Json(new SimulateIdempotencyResponse(
                    StatusCode: lockResult.CachedStatusCode ?? 200,
                    Success: true,
                    ReplayDetected: true,
                    Status: "Completed",
                    Message: lockResult.Message,
                    Fingerprint: lockResult.Fingerprint,
                    Headers: headers,
                    ResponseBody: lockResult.CachedResponseBody,
                    ReplayCount: lockResult.ReplayCount,
                    ExpiresAt: lockResult.ExpiresAt,
                    ExecutionDurationMs: sw.ElapsedMilliseconds,
                    Metrics: metrics
                ), statusCode: lockResult.CachedStatusCode ?? 200);
            }

            if (!lockResult.Success)
            {
                sw.Stop();
                var metrics = await service.GetMetricsAsync(tenant);
                return Results.Json(new SimulateIdempotencyResponse(
                    StatusCode: lockResult.StatusCode,
                    Success: false,
                    ReplayDetected: false,
                    Status: lockResult.Status.ToString(),
                    Message: lockResult.Message,
                    Fingerprint: lockResult.Fingerprint,
                    Headers: headers,
                    ResponseBody: JsonSerializer.Serialize(new
                    {
                        error = lockResult.Status.ToString(),
                        status = lockResult.StatusCode,
                        message = lockResult.Message,
                        idempotency_key = key
                    }),
                    ReplayCount: lockResult.ReplayCount,
                    ExpiresAt: lockResult.ExpiresAt,
                    ExecutionDurationMs: sw.ElapsedMilliseconds,
                    Metrics: metrics
                ), statusCode: lockResult.StatusCode);
            }

            // Successfully acquired brand new in-flight lock -> complete work
            headers["X-Idempotency-Replay"] = "false";
            var simulatedResponsePayload = JsonSerializer.Serialize(new
            {
                transaction_id = $"tx_{Guid.NewGuid():N}"[..16],
                status = "created",
                route = path,
                method = method,
                processed_at = DateTimeOffset.UtcNow,
                confirmation_code = $"AUTH_{Random.Shared.Next(100000, 999999)}"
            });

            await service.RecordCompletedResponseAsync(tenant, key, 201, headers, simulatedResponsePayload);
            sw.Stop();

            var finalMetrics = await service.GetMetricsAsync(tenant);
            return Results.Json(new SimulateIdempotencyResponse(
                StatusCode: 201,
                Success: true,
                ReplayDetected: false,
                Status: "Completed",
                Message: "Operation successfully processed and cached in idempotency ledger.",
                Fingerprint: lockResult.Fingerprint,
                Headers: headers,
                ResponseBody: simulatedResponsePayload,
                ReplayCount: 0,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(24),
                ExecutionDurationMs: sw.ElapsedMilliseconds,
                Metrics: finalMetrics
            ), statusCode: 201);
        });

        // 4. DELETE /{key} - Invalidate an idempotency key
        group.MapDelete("/{key}", async (
            HttpContext ctx,
            [FromRoute] string key,
            [FromQuery] string? tenantId,
            [FromServices] IDistributedIdempotencyService? svc = null) =>
        {
            var service = ResolveService(ctx, svc);
            var safeTenant = tenantId ?? "tenant-default";
            var removed = await service.InvalidateKeyAsync(safeTenant, key);

            return Results.Ok(new InvalidateKeyResponse(
                Success: removed,
                Key: key,
                Message: removed ? $"Idempotency key '{key}' was successfully purged from the ledger." : $"Key '{key}' was not found in active ledger."
            ));
        });

        // 5. POST /reset - Reset ledger records to clean state
        group.MapPost("/reset", async (
            HttpContext ctx,
            [FromServices] IDistributedIdempotencyService? svc = null) =>
        {
            var service = ResolveService(ctx, svc);
            await service.ResetAllAsync();
            return Results.Ok(new { success = true, message = "Idempotency ledger reset to blank state." });
        });

        return app;
    }
}
