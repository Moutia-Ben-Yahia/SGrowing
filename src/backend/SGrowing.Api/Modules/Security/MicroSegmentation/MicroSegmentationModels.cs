using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Security.MicroSegmentation;

public enum PolicyAction
{
    Allow,
    Deny
}

public record ServiceIdentity(
    string SpiffeId,
    string ServiceName,
    string Namespace,
    DateTime CertExpiresAtUtc,
    bool IsMtlsActive
);

public record SecurityPolicyRule(
    string RuleId,
    string SourceService,
    string TargetService,
    string AllowedPathPrefix,
    PolicyAction Action
);

public record MeshTrafficAudit(
    string SourceService,
    string TargetService,
    string RequestPath,
    bool WasAllowed,
    string Reason
);

public record RotateCertRequest(
    string ServiceName
);

public record EvaluateTrafficRequest(
    string SourceService,
    string TargetService,
    string RequestPath
);

public record MicroSegmentationTopology(
    List<ServiceIdentity> Services,
    List<SecurityPolicyRule> PolicyRules,
    int InterceptedRogueRequestsCount,
    DateTime LastAuditAtUtc
);
