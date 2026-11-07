using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.LeadRouting;

public interface ILeadRoutingCapacityService
{
    LeadAssignmentRecord RouteLead(RouteLeadRequest request);
    SdrRepProfile? UpdateSdrStatus(UpdateSdrStatusRequest request);
    LeadAssignmentRecord? LogFirstContact(LogContactAttemptRequest request);
    List<LeadAssignmentRecord> CheckAndTriggerSlaBreaches(Guid tenantId);
    LeadRoutingOverviewReport GetOverview(Guid tenantId);
    void AddSdr(SdrRepProfile profile);
    List<SdrRepProfile> GetSdrRoster(Guid tenantId);
    List<LeadAssignmentRecord> GetAssignments(Guid tenantId);
    void Reset();
}

public class LeadRoutingCapacityService : ILeadRoutingCapacityService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid TariqId = Guid.Parse("22222222-1111-1111-1111-111111111111");
    public static readonly Guid ReemId = Guid.Parse("33333333-1111-1111-1111-111111111111");
    public static readonly Guid ZaidId = Guid.Parse("44444444-1111-1111-1111-111111111111");
    public static readonly Guid SarahId = Guid.Parse("55555555-1111-1111-1111-111111111111");

    private readonly ConcurrentDictionary<Guid, SdrRepProfile> _sdrs = new();
    private readonly ConcurrentDictionary<Guid, LeadAssignmentRecord> _assignments = new();
    private readonly object _syncLock = new();

    public LeadRoutingCapacityService()
    {
        SeedDefaultData();
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            _sdrs.Clear();
            _assignments.Clear();
            SeedDefaultData();
        }
    }

    public void SeedDefaultData()
    {
        // Tariq Al-Ghamdi (Riyadh, B2B SaaS, Online, MaxCapacity=15, Current=6, QuotaWeight=1.2)
        var tariq = new SdrRepProfile(
            TariqId,
            DefaultTenantId,
            "Tariq Al-Ghamdi",
            "tariq.ghamdi@sgrowing.com",
            SdrAvailabilityStatus.Online,
            15,
            6,
            1.2m,
            ["Riyadh", "Central Province", "KSA"],
            ["B2B SaaS", "Enterprise", "Outbound AI"],
            DateTime.UtcNow.AddMinutes(-45)
        );

        // Reem Al-Khatib (Jeddah, Retail FinTech, Online, MaxCapacity=12, Current=4, QuotaWeight=1.0)
        var reem = new SdrRepProfile(
            ReemId,
            DefaultTenantId,
            "Reem Al-Khatib",
            "reem.khatib@sgrowing.com",
            SdrAvailabilityStatus.Online,
            12,
            4,
            1.0m,
            ["Jeddah", "Western Province", "KSA"],
            ["Retail FinTech", "Payment Gateways", "SME"],
            DateTime.UtcNow.AddMinutes(-30)
        );

        // Zaid Al-Otaibi (Dammam, Enterprise Logistics, Online, MaxCapacity=10, Current=8, QuotaWeight=0.8)
        var zaid = new SdrRepProfile(
            ZaidId,
            DefaultTenantId,
            "Zaid Al-Otaibi",
            "zaid.otaibi@sgrowing.com",
            SdrAvailabilityStatus.Online,
            10,
            8,
            0.8m,
            ["Dammam", "Eastern Province", "KSA"],
            ["Enterprise Logistics", "Supply Chain", "Heavy Industry"],
            DateTime.UtcNow.AddMinutes(-15)
        );

        // Sarah Mansour (Dubai / GCC, Cross-Border E-Commerce, Away, MaxCapacity=10, Current=2, QuotaWeight=1.0)
        var sarah = new SdrRepProfile(
            SarahId,
            DefaultTenantId,
            "Sarah Mansour",
            "sarah.mansour@sgrowing.com",
            SdrAvailabilityStatus.Away,
            10,
            2,
            1.0m,
            ["Dubai / GCC", "UAE", "GCC"],
            ["Cross-Border E-Commerce", "Omnichannel", "Luxury Retail"],
            DateTime.UtcNow.AddHours(-2)
        );

        _sdrs[tariq.SdrId] = tariq;
        _sdrs[reem.SdrId] = reem;
        _sdrs[zaid.SdrId] = zaid;
        _sdrs[sarah.SdrId] = sarah;

        // Seed some representative assignments for the radar
        var now = DateTime.UtcNow;
        var assign1 = new LeadAssignmentRecord(
            Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
            DefaultTenantId,
            Guid.Parse("baaaaaaa-1111-1111-1111-111111111111"),
            "Omar Al-Falih",
            "omar@cloudscale-sa.com",
            "CloudScale KSA",
            "Riyadh",
            TariqId,
            "Tariq Al-Ghamdi",
            now.AddMinutes(-5),
            now.AddMinutes(10),
            LeadSlaBreachStatus.WithinSla,
            null,
            null
        );

        var assign2 = new LeadAssignmentRecord(
            Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111"),
            DefaultTenantId,
            Guid.Parse("bbaaaaaa-1111-1111-1111-111111111111"),
            "Noor Bint Sultan",
            "noor@redseapay.com",
            "RedSea Pay FinTech",
            "Jeddah",
            ReemId,
            "Reem Al-Khatib",
            now.AddMinutes(-12),
            now.AddMinutes(3),
            LeadSlaBreachStatus.Warning,
            null,
            null
        );

        var assign3 = new LeadAssignmentRecord(
            Guid.Parse("cccccccc-1111-1111-1111-111111111111"),
            DefaultTenantId,
            Guid.Parse("bcaaaaaa-1111-1111-1111-111111111111"),
            "Khalid Al-Dossari",
            "khalid@gulfcoldchain.sa",
            "Gulf Cold Chain Logistics",
            "Dammam",
            ZaidId,
            "Zaid Al-Otaibi",
            now.AddMinutes(-25),
            now.AddMinutes(-10),
            LeadSlaBreachStatus.Breached,
            null,
            null
        );

        var assign4 = new LeadAssignmentRecord(
            Guid.Parse("dddddddd-1111-1111-1111-111111111111"),
            DefaultTenantId,
            Guid.Parse("bdaaaaaa-1111-1111-1111-111111111111"),
            "Fatima Al-Harbi",
            "fatima@saudiretail.com",
            "Saudi Retail Group",
            "Riyadh",
            TariqId,
            "Tariq Al-Ghamdi",
            now.AddHours(-1),
            now.AddMinutes(-45),
            LeadSlaBreachStatus.WithinSla,
            now.AddMinutes(-54),
            null
        );

        _assignments[assign1.AssignmentId] = assign1;
        _assignments[assign2.AssignmentId] = assign2;
        _assignments[assign3.AssignmentId] = assign3;
        _assignments[assign4.AssignmentId] = assign4;
    }

    public void AddSdr(SdrRepProfile profile)
    {
        _sdrs[profile.SdrId] = profile;
    }

    public List<SdrRepProfile> GetSdrRoster(Guid tenantId)
    {
        return _sdrs.Values
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.RepName)
            .ToList();
    }

    public List<LeadAssignmentRecord> GetAssignments(Guid tenantId)
    {
        return _assignments.Values
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.AssignedAtUtc)
            .ToList();
    }

    public LeadAssignmentRecord RouteLead(RouteLeadRequest request)
    {
        lock (_syncLock)
        {
            var tenantReps = _sdrs.Values
                .Where(s => s.TenantId == request.TenantId)
                .ToList();

            var onlineRepsWithCapacity = tenantReps
                .Where(s => s.Status == SdrAvailabilityStatus.Online && s.CurrentActiveLeads < s.MaxCapacity)
                .ToList();

            if (onlineRepsWithCapacity.Count == 0)
            {
                throw new InvalidOperationException($"No online SDR available with capacity in tenant {request.TenantId}.");
            }

            // Territory match check
            bool MatchesTerritory(SdrRepProfile sdr)
            {
                if (string.IsNullOrWhiteSpace(request.Territory)) return true;
                return sdr.AssignedTerritories.Any(t =>
                    t.Contains(request.Territory, StringComparison.OrdinalIgnoreCase) ||
                    request.Territory.Contains(t, StringComparison.OrdinalIgnoreCase));
            }

            // Skill match check
            bool MatchesSkill(SdrRepProfile sdr)
            {
                if (request.RequiredSkills == null || request.RequiredSkills.Count == 0) return true;
                return sdr.Skills.Any(sk => request.RequiredSkills.Any(r =>
                    sk.Contains(r, StringComparison.OrdinalIgnoreCase) ||
                    r.Contains(sk, StringComparison.OrdinalIgnoreCase)));
            }

            // Tier classification
            var tier1 = onlineRepsWithCapacity.Where(s => MatchesTerritory(s) && MatchesSkill(s)).ToList();
            var tier2 = onlineRepsWithCapacity.Where(MatchesTerritory).ToList();
            var tier3 = onlineRepsWithCapacity.Where(MatchesSkill).ToList();

            List<SdrRepProfile> selectedPool;
            if (tier1.Count > 0)
            {
                selectedPool = tier1;
            }
            else if (tier2.Count > 0)
            {
                selectedPool = tier2;
            }
            else if (tier3.Count > 0)
            {
                selectedPool = tier3;
            }
            else
            {
                // Capacity exhaustion overflow pool: any online SDR with capacity
                selectedPool = onlineRepsWithCapacity;
            }

            // Weighted round-robin / least-loaded selection
            // Score = (CurrentActiveLeads / MaxCapacity) / QuotaWeight
            // Tie-break with oldest LastAssignedAtUtc for round-robin fairness
            var selectedSdr = selectedPool
                .OrderBy(s =>
                {
                    double utilization = (double)s.CurrentActiveLeads / Math.Max(1, s.MaxCapacity);
                    double weight = (double)Math.Max(0.1m, s.QuotaWeight);
                    return utilization / weight;
                })
                .ThenBy(s => s.LastAssignedAtUtc)
                .First();

            // Update assigned SDR
            var updatedSdr = selectedSdr with
            {
                CurrentActiveLeads = selectedSdr.CurrentActiveLeads + 1,
                LastAssignedAtUtc = DateTime.UtcNow
            };
            _sdrs[updatedSdr.SdrId] = updatedSdr;

            var assignedAt = DateTime.UtcNow;
            var slaDueAt = assignedAt.AddMinutes(15);

            var assignment = new LeadAssignmentRecord(
                Guid.NewGuid(),
                request.TenantId,
                request.LeadId,
                request.LeadName,
                request.LeadEmail,
                request.CompanyName,
                request.Territory,
                selectedSdr.SdrId,
                selectedSdr.RepName,
                assignedAt,
                slaDueAt,
                LeadSlaBreachStatus.WithinSla,
                null,
                null
            );

            _assignments[assignment.AssignmentId] = assignment;
            return assignment;
        }
    }

    public SdrRepProfile? UpdateSdrStatus(UpdateSdrStatusRequest request)
    {
        lock (_syncLock)
        {
            if (!_sdrs.TryGetValue(request.SdrId, out var existing))
            {
                return null;
            }

            if (existing.TenantId != request.TenantId)
            {
                return null;
            }

            var updated = existing with
            {
                Status = request.NewStatus,
                MaxCapacity = request.NewMaxCapacity ?? existing.MaxCapacity
            };

            _sdrs[request.SdrId] = updated;
            return updated;
        }
    }

    public LeadAssignmentRecord? LogFirstContact(LogContactAttemptRequest request)
    {
        lock (_syncLock)
        {
            if (!_assignments.TryGetValue(request.AssignmentId, out var existing))
            {
                return null;
            }

            if (existing.TenantId != request.TenantId)
            {
                return null;
            }

            if (existing.FirstContactedAtUtc.HasValue)
            {
                return existing;
            }

            var contactTime = DateTime.UtcNow;
            var status = contactTime <= existing.SlaDueAtUtc
                ? LeadSlaBreachStatus.WithinSla
                : LeadSlaBreachStatus.Breached;

            var updated = existing with
            {
                FirstContactedAtUtc = contactTime,
                SlaStatus = status
            };

            _assignments[request.AssignmentId] = updated;

            // Decrement active lead count for the rep
            if (_sdrs.TryGetValue(existing.SdrId, out var sdr) && sdr.CurrentActiveLeads > 0)
            {
                _sdrs[existing.SdrId] = sdr with
                {
                    CurrentActiveLeads = Math.Max(0, sdr.CurrentActiveLeads - 1)
                };
            }

            return updated;
        }
    }

    public List<LeadAssignmentRecord> CheckAndTriggerSlaBreaches(Guid tenantId)
    {
        lock (_syncLock)
        {
            var now = DateTime.UtcNow;
            var affected = new List<LeadAssignmentRecord>();

            var pending = _assignments.Values
                .Where(a => a.TenantId == tenantId && a.FirstContactedAtUtc == null && a.SlaStatus != LeadSlaBreachStatus.Reassigned)
                .ToList();

            foreach (var assignment in pending)
            {
                if (now > assignment.SlaDueAtUtc)
                {
                    // Breached! Find next available SDR for reassignment
                    var candidateSdr = _sdrs.Values
                        .Where(s => s.TenantId == tenantId &&
                                    s.Status == SdrAvailabilityStatus.Online &&
                                    s.CurrentActiveLeads < s.MaxCapacity &&
                                    s.SdrId != assignment.SdrId)
                        .OrderBy(s => (double)s.CurrentActiveLeads / Math.Max(1, s.MaxCapacity) / (double)Math.Max(0.1m, s.QuotaWeight))
                        .ThenBy(s => s.LastAssignedAtUtc)
                        .FirstOrDefault();

                    if (candidateSdr != null)
                    {
                        // Decrement previous SDR's lead count
                        if (_sdrs.TryGetValue(assignment.SdrId, out var prevSdr) && prevSdr.CurrentActiveLeads > 0)
                        {
                            _sdrs[assignment.SdrId] = prevSdr with { CurrentActiveLeads = Math.Max(0, prevSdr.CurrentActiveLeads - 1) };
                        }

                        // Increment new SDR's lead count
                        _sdrs[candidateSdr.SdrId] = candidateSdr with
                        {
                            CurrentActiveLeads = candidateSdr.CurrentActiveLeads + 1,
                            LastAssignedAtUtc = now
                        };

                        var reassigned = assignment with
                        {
                            SdrId = candidateSdr.SdrId,
                            SdrName = candidateSdr.RepName,
                            ReassignedFromSdrId = assignment.SdrId,
                            SlaStatus = LeadSlaBreachStatus.Reassigned,
                            AssignedAtUtc = now,
                            SlaDueAtUtc = now.AddMinutes(15)
                        };

                        _assignments[assignment.AssignmentId] = reassigned;
                        affected.Add(reassigned);
                    }
                    else
                    {
                        var breached = assignment with
                        {
                            SlaStatus = LeadSlaBreachStatus.Breached
                        };

                        _assignments[assignment.AssignmentId] = breached;
                        affected.Add(breached);
                    }
                }
                else if ((assignment.SlaDueAtUtc - now).TotalMinutes <= 5 && assignment.SlaStatus == LeadSlaBreachStatus.WithinSla)
                {
                    var warning = assignment with
                    {
                        SlaStatus = LeadSlaBreachStatus.Warning
                    };

                    _assignments[assignment.AssignmentId] = warning;
                    affected.Add(warning);
                }
            }

            return affected;
        }
    }

    public LeadRoutingOverviewReport GetOverview(Guid tenantId)
    {
        lock (_syncLock)
        {
            var reps = _sdrs.Values
                .Where(s => s.TenantId == tenantId)
                .OrderBy(s => s.RepName)
                .ToList();

            var assignments = _assignments.Values
                .Where(a => a.TenantId == tenantId)
                .OrderByDescending(a => a.AssignedAtUtc)
                .ToList();

            var pending = assignments
                .Where(a => a.FirstContactedAtUtc == null)
                .ToList();

            var contacted = assignments
                .Where(a => a.FirstContactedAtUtc != null)
                .ToList();

            int onlineCount = reps.Count(r => r.Status == SdrAvailabilityStatus.Online);
            int breachesToday = assignments.Count(a =>
                a.SlaStatus == LeadSlaBreachStatus.Breached ||
                a.SlaStatus == LeadSlaBreachStatus.Reassigned ||
                (a.FirstContactedAtUtc.HasValue && a.FirstContactedAtUtc.Value > a.SlaDueAtUtc));

            double avgContactMins = contacted.Count > 0
                ? Math.Round(contacted.Average(a => (a.FirstContactedAtUtc!.Value - a.AssignedAtUtc).TotalMinutes), 1)
                : 4.5;

            return new LeadRoutingOverviewReport(
                tenantId,
                reps.Count,
                onlineCount,
                pending.Count,
                breachesToday,
                avgContactMins,
                reps,
                pending
            );
        }
    }
}
