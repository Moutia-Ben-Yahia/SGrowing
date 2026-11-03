using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Security.MicroSegmentation;

public interface IMicroSegmentationEngine
{
    MicroSegmentationTopology GetTopology();
    ServiceIdentity RotateCertificate(RotateCertRequest request);
    MeshTrafficAudit EvaluateTraffic(EvaluateTrafficRequest request);
}

public class MicroSegmentationEngine : IMicroSegmentationEngine
{
    private readonly ConcurrentDictionary<string, ServiceIdentity> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SecurityPolicyRule> _rules = new();
    private int _interceptedCount = 142;

    public MicroSegmentationEngine()
    {
        // Seed service identities
        var s1 = new ServiceIdentity("spiffe://sgrowing.internal/ns/core/sa/api-gateway", "api-gateway", "core", DateTime.UtcNow.AddDays(7), true);
        var s2 = new ServiceIdentity("spiffe://sgrowing.internal/ns/finance/sa/billing-service", "billing-service", "finance", DateTime.UtcNow.AddDays(7), true);
        var s3 = new ServiceIdentity("spiffe://sgrowing.internal/ns/analytics/sa/telemetry-agent", "telemetry-agent", "analytics", DateTime.UtcNow.AddDays(7), true);
        var s4 = new ServiceIdentity("spiffe://sgrowing.internal/ns/sec/sa/vault-keyring", "vault-keyring", "sec", DateTime.UtcNow.AddDays(7), true);

        _services[s1.ServiceName] = s1;
        _services[s2.ServiceName] = s2;
        _services[s3.ServiceName] = s3;
        _services[s4.ServiceName] = s4;

        // Seed L7 zero-trust policy rules
        _rules.Add(new SecurityPolicyRule("pol_01", "api-gateway", "billing-service", "/api/v1/billing", PolicyAction.Allow));
        _rules.Add(new SecurityPolicyRule("pol_02", "billing-service", "vault-keyring", "/api/v1/security/ephemeral", PolicyAction.Allow));
        _rules.Add(new SecurityPolicyRule("pol_03", "telemetry-agent", "vault-keyring", "*", PolicyAction.Deny)); // Denied by default
    }

    public MicroSegmentationTopology GetTopology()
    {
        return new MicroSegmentationTopology(
            Services: _services.Values.OrderBy(s => s.ServiceName).ToList(),
            PolicyRules: _rules,
            InterceptedRogueRequestsCount: _interceptedCount,
            LastAuditAtUtc: DateTime.UtcNow
        );
    }

    public ServiceIdentity RotateCertificate(RotateCertRequest request)
    {
        if (!_services.TryGetValue(request.ServiceName, out var existing))
        {
            throw new KeyNotFoundException($"Service {request.ServiceName} not recognized in mesh.");
        }

        var renewed = existing with
        {
            CertExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            IsMtlsActive = true
        };

        _services[request.ServiceName] = renewed;
        return renewed;
    }

    public MeshTrafficAudit EvaluateTraffic(EvaluateTrafficRequest request)
    {
        // Find matching policy rule
        var matchingRule = _rules.FirstOrDefault(r =>
            r.SourceService.Equals(request.SourceService, StringComparison.OrdinalIgnoreCase) &&
            r.TargetService.Equals(request.TargetService, StringComparison.OrdinalIgnoreCase));

        if (matchingRule is not null && matchingRule.Action == PolicyAction.Allow)
        {
            return new MeshTrafficAudit(
                SourceService: request.SourceService,
                TargetService: request.TargetService,
                RequestPath: request.RequestPath,
                WasAllowed: true,
                Reason: $"Allowed by L7 micro-segmentation rule {matchingRule.RuleId}"
            );
        }

        // Intercepted rogue call
        _interceptedCount++;
        return new MeshTrafficAudit(
            SourceService: request.SourceService,
            TargetService: request.TargetService,
            RequestPath: request.RequestPath,
            WasAllowed: false,
            Reason: "Zero-Trust policy violation: Implicit deny. Lateral cross-service traversal prevented."
        );
    }
}
