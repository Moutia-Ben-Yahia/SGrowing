using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Tenancy.MultiRegion;

public interface ISovereignDataShardingManager
{
    Task RegisterResidencyPolicyAsync(TenantDataResidencyPolicy policy);
    Task<TenantDataResidencyPolicy?> GetResidencyPolicyAsync(Guid tenantId);
    Task<IReadOnlyList<TenantDataResidencyPolicy>> GetAllPoliciesAsync();
    Task<SovereignValidationResult> ValidateReplicationPermittedAsync(
        Guid? tenantId,
        RegionIdentifier origin,
        RegionIdentifier target,
        bool isPii,
        string entityType,
        string entityId);
    IReadOnlyList<SovereignViolationAuditRecord> GetViolationAuditLogs(Guid? tenantId = null);
    void RecordViolation(SovereignViolationAuditRecord violation);
    void ClearViolationAuditLogs();
    Task<string> GetPhysicalConnectionStringAsync(Guid tenantId, RegionIdentifier requestingRegion, bool requiresWrite);
}

public class SovereignDataShardingManager : ISovereignDataShardingManager
{
    private readonly ConcurrentDictionary<Guid, TenantDataResidencyPolicy> _policies = new();
    private readonly ConcurrentBag<SovereignViolationAuditRecord> _auditLogs = new();

    public static readonly Guid AlgerianNationalBankTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid EuEnterpriseHealthTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DubaiLogisticsHubTenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid GlobalSaaSFloatingTenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public SovereignDataShardingManager()
    {
        SeedDefaultPolicies();
    }

    private void SeedDefaultPolicies()
    {
        _policies[AlgerianNationalBankTenantId] = new TenantDataResidencyPolicy(
            TenantId: AlgerianNationalBankTenantId,
            HomeRegion: RegionIdentifier.AfNorth1,
            EnforcedJurisdiction: SovereignJurisdiction.Algeria_Law1807,
            AllowCrossRegionReadReplicas: false,
            DataLocalityStrictness: DataLocalityStrictness.AbsoluteSovereign,
            ConfiguredAtUtc: DateTime.UtcNow.AddMonths(-3),
            ConfiguredBy: "DGT_Compliance_Engine",
            BlockedExportRegions: ["eu-west-3", "me-central-1", "us-east-1"]
        );

        _policies[EuEnterpriseHealthTenantId] = new TenantDataResidencyPolicy(
            TenantId: EuEnterpriseHealthTenantId,
            HomeRegion: RegionIdentifier.EuWest3,
            EnforcedJurisdiction: SovereignJurisdiction.Eu_Gdpr,
            AllowCrossRegionReadReplicas: false,
            DataLocalityStrictness: DataLocalityStrictness.AbsoluteSovereign,
            ConfiguredAtUtc: DateTime.UtcNow.AddMonths(-2),
            ConfiguredBy: "GDPR_Data_Protection_Officer",
            BlockedExportRegions: ["af-north-1", "me-central-1"]
        );

        _policies[DubaiLogisticsHubTenantId] = new TenantDataResidencyPolicy(
            TenantId: DubaiLogisticsHubTenantId,
            HomeRegion: RegionIdentifier.MeCentral1,
            EnforcedJurisdiction: SovereignJurisdiction.Uae_DIFC,
            AllowCrossRegionReadReplicas: true,
            DataLocalityStrictness: DataLocalityStrictness.RegionalPreferred,
            ConfiguredAtUtc: DateTime.UtcNow.AddMonths(-1),
            ConfiguredBy: "DIFC_Compliance",
            BlockedExportRegions: []
        );

        _policies[GlobalSaaSFloatingTenantId] = new TenantDataResidencyPolicy(
            TenantId: GlobalSaaSFloatingTenantId,
            HomeRegion: RegionIdentifier.EuWest3,
            EnforcedJurisdiction: SovereignJurisdiction.Global_Default,
            AllowCrossRegionReadReplicas: true,
            DataLocalityStrictness: DataLocalityStrictness.GlobalFloating,
            ConfiguredAtUtc: DateTime.UtcNow.AddDays(-15),
            ConfiguredBy: "Tenant_Admin",
            BlockedExportRegions: []
        );
    }

    public Task RegisterResidencyPolicyAsync(TenantDataResidencyPolicy policy)
    {
        _policies[policy.TenantId] = policy;
        return Task.CompletedTask;
    }

    public Task<TenantDataResidencyPolicy?> GetResidencyPolicyAsync(Guid tenantId)
    {
        _policies.TryGetValue(tenantId, out var policy);
        return Task.FromResult(policy);
    }

    public Task<IReadOnlyList<TenantDataResidencyPolicy>> GetAllPoliciesAsync()
    {
        IReadOnlyList<TenantDataResidencyPolicy> list = _policies.Values.ToList();
        return Task.FromResult(list);
    }

    public Task<SovereignValidationResult> ValidateReplicationPermittedAsync(
        Guid? tenantId,
        RegionIdentifier origin,
        RegionIdentifier target,
        bool isPii,
        string entityType,
        string entityId)
    {
        // 1. Intra-region replication is always permitted within same sovereign data plane
        if (origin == target)
        {
            return Task.FromResult(new SovereignValidationResult(
                IsPermitted: true,
                ViolationReason: null,
                Jurisdiction: SovereignJurisdiction.Global_Default,
                AuditLogged: false
            ));
        }

        // 2. Fetch or default tenant residency policy
        TenantDataResidencyPolicy policy;
        if (tenantId.HasValue && _policies.TryGetValue(tenantId.Value, out var existingPolicy))
        {
            policy = existingPolicy;
        }
        else
        {
            // If origin is AfNorth1, enforce Algeria Law 18-07 defaults
            if (origin == RegionIdentifier.AfNorth1)
            {
                policy = new TenantDataResidencyPolicy(
                    TenantId: tenantId ?? Guid.Empty,
                    HomeRegion: RegionIdentifier.AfNorth1,
                    EnforcedJurisdiction: SovereignJurisdiction.Algeria_Law1807,
                    AllowCrossRegionReadReplicas: false,
                    DataLocalityStrictness: DataLocalityStrictness.AbsoluteSovereign,
                    ConfiguredAtUtc: DateTime.UtcNow,
                    ConfiguredBy: "Sovereign_Default_Heuristic"
                );
            }
            else if (origin == RegionIdentifier.EuWest3)
            {
                policy = new TenantDataResidencyPolicy(
                    TenantId: tenantId ?? Guid.Empty,
                    HomeRegion: RegionIdentifier.EuWest3,
                    EnforcedJurisdiction: SovereignJurisdiction.Eu_Gdpr,
                    AllowCrossRegionReadReplicas: false,
                    DataLocalityStrictness: DataLocalityStrictness.AbsoluteSovereign,
                    ConfiguredAtUtc: DateTime.UtcNow,
                    ConfiguredBy: "Sovereign_Default_Heuristic"
                );
            }
            else
            {
                policy = new TenantDataResidencyPolicy(
                    TenantId: tenantId ?? Guid.Empty,
                    HomeRegion: origin,
                    EnforcedJurisdiction: SovereignJurisdiction.Global_Default,
                    AllowCrossRegionReadReplicas: true,
                    DataLocalityStrictness: DataLocalityStrictness.GlobalFloating,
                    ConfiguredAtUtc: DateTime.UtcNow,
                    ConfiguredBy: "Default"
                );
            }
        }

        // 3. Evaluate sovereignty strictness
        if (policy.EnforcedJurisdiction == SovereignJurisdiction.Algeria_Law1807)
        {
            if (policy.DataLocalityStrictness == DataLocalityStrictness.AbsoluteSovereign)
            {
                var violation = new SovereignViolationAuditRecord(
                    AuditId: Guid.NewGuid(),
                    TenantId: policy.TenantId,
                    AttemptedOrigin: origin,
                    AttemptedTarget: target,
                    ViolatedJurisdiction: SovereignJurisdiction.Algeria_Law1807,
                    EntityType: entityType,
                    EntityId: entityId,
                    Reason: $"Algerian Law 18-07 Sovereign Breach: Attempted cross-border replication from {origin} to foreign region {target} under AbsoluteSovereign strictness.",
                    TimestampUtc: DateTime.UtcNow
                );
                RecordViolation(violation);

                return Task.FromResult(new SovereignValidationResult(
                    IsPermitted: false,
                    ViolationReason: violation.Reason,
                    Jurisdiction: SovereignJurisdiction.Algeria_Law1807,
                    AuditLogged: true
                ));
            }

            if (isPii)
            {
                var violation = new SovereignViolationAuditRecord(
                    AuditId: Guid.NewGuid(),
                    TenantId: policy.TenantId,
                    AttemptedOrigin: origin,
                    AttemptedTarget: target,
                    ViolatedJurisdiction: SovereignJurisdiction.Algeria_Law1807,
                    EntityType: entityType,
                    EntityId: entityId,
                    Reason: $"Algerian Law 18-07 PII Export Violation: Personal Identifiable Information cannot cross national borders to {target}.",
                    TimestampUtc: DateTime.UtcNow
                );
                RecordViolation(violation);

                return Task.FromResult(new SovereignValidationResult(
                    IsPermitted: false,
                    ViolationReason: violation.Reason,
                    Jurisdiction: SovereignJurisdiction.Algeria_Law1807,
                    AuditLogged: true
                ));
            }
        }
        else if (policy.EnforcedJurisdiction == SovereignJurisdiction.Eu_Gdpr)
        {
            if (policy.DataLocalityStrictness == DataLocalityStrictness.AbsoluteSovereign)
            {
                var violation = new SovereignViolationAuditRecord(
                    AuditId: Guid.NewGuid(),
                    TenantId: policy.TenantId,
                    AttemptedOrigin: origin,
                    AttemptedTarget: target,
                    ViolatedJurisdiction: SovereignJurisdiction.Eu_Gdpr,
                    EntityType: entityType,
                    EntityId: entityId,
                    Reason: $"GDPR Chapter V Cross-Border Transfer Breach: Transfer of data from EU ({origin}) to third-country region {target} is blocked under AbsoluteSovereign strictness.",
                    TimestampUtc: DateTime.UtcNow
                );
                RecordViolation(violation);

                return Task.FromResult(new SovereignValidationResult(
                    IsPermitted: false,
                    ViolationReason: violation.Reason,
                    Jurisdiction: SovereignJurisdiction.Eu_Gdpr,
                    AuditLogged: true
                ));
            }
        }
        else if (policy.EnforcedJurisdiction == SovereignJurisdiction.Uae_DIFC)
        {
            if (policy.DataLocalityStrictness == DataLocalityStrictness.AbsoluteSovereign)
            {
                var violation = new SovereignViolationAuditRecord(
                    AuditId: Guid.NewGuid(),
                    TenantId: policy.TenantId,
                    AttemptedOrigin: origin,
                    AttemptedTarget: target,
                    ViolatedJurisdiction: SovereignJurisdiction.Uae_DIFC,
                    EntityType: entityType,
                    EntityId: entityId,
                    Reason: $"DIFC Data Localization Breach: Transfer from {origin} to {target} is blocked under AbsoluteSovereign strictness.",
                    TimestampUtc: DateTime.UtcNow
                );
                RecordViolation(violation);

                return Task.FromResult(new SovereignValidationResult(
                    IsPermitted: false,
                    ViolationReason: violation.Reason,
                    Jurisdiction: SovereignJurisdiction.Uae_DIFC,
                    AuditLogged: true
                ));
            }
        }

        // Permitted
        return Task.FromResult(new SovereignValidationResult(
            IsPermitted: true,
            ViolationReason: null,
            Jurisdiction: policy.EnforcedJurisdiction,
            AuditLogged: false
        ));
    }

    public IReadOnlyList<SovereignViolationAuditRecord> GetViolationAuditLogs(Guid? tenantId = null)
    {
        if (tenantId.HasValue)
        {
            return _auditLogs.Where(l => l.TenantId == tenantId.Value).OrderByDescending(l => l.TimestampUtc).ToList();
        }
        return _auditLogs.OrderByDescending(l => l.TimestampUtc).ToList();
    }

    public void RecordViolation(SovereignViolationAuditRecord violation)
    {
        _auditLogs.Add(violation);
    }

    public void ClearViolationAuditLogs()
    {
        _auditLogs.Clear();
    }

    public Task<string> GetPhysicalConnectionStringAsync(Guid tenantId, RegionIdentifier requestingRegion, bool requiresWrite)
    {
        _policies.TryGetValue(tenantId, out var policy);
        var homeRegion = policy?.HomeRegion ?? RegionIdentifier.AfNorth1;

        if (requiresWrite)
        {
            // Write must go to primary endpoint in the home sovereign region
            var endpoint = homeRegion switch
            {
                RegionIdentifier.AfNorth1 => "postgres://dz-primary.sgrowing.internal:5432/sgrowing_sovereign_dz",
                RegionIdentifier.EuWest3 => "postgres://fr-primary.sgrowing.internal:5432/sgrowing_sovereign_eu",
                RegionIdentifier.MeCentral1 => "postgres://uae-primary.sgrowing.internal:5432/sgrowing_sovereign_me",
                _ => "postgres://dz-primary.sgrowing.internal:5432/sgrowing_sovereign_dz"
            };
            return Task.FromResult(endpoint);
        }

        // Read query: check if cross-region read replicas are allowed
        if (policy is not null && policy.AllowCrossRegionReadReplicas)
        {
            // Use closest read replica to requesting region
            var replica = requestingRegion switch
            {
                RegionIdentifier.AfNorth1 => "postgres://dz-ro1.sgrowing.internal:5432/sgrowing_ro",
                RegionIdentifier.EuWest3 => "postgres://fr-ro1.sgrowing.internal:5432/sgrowing_ro",
                RegionIdentifier.MeCentral1 => "postgres://dxb-ro1.sgrowing.internal:5432/sgrowing_ro",
                _ => "postgres://dz-ro1.sgrowing.internal:5432/sgrowing_ro"
            };
            return Task.FromResult(replica);
        }

        // Read must stay inside home sovereign region
        var sovereignReplica = homeRegion switch
        {
            RegionIdentifier.AfNorth1 => "postgres://dz-ro1.sgrowing.internal:5432/sgrowing_ro",
            RegionIdentifier.EuWest3 => "postgres://fr-ro1.sgrowing.internal:5432/sgrowing_ro",
            RegionIdentifier.MeCentral1 => "postgres://dxb-ro1.sgrowing.internal:5432/sgrowing_ro",
            _ => "postgres://dz-ro1.sgrowing.internal:5432/sgrowing_ro"
        };
        return Task.FromResult(sovereignReplica);
    }
}
