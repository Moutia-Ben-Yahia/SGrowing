using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Support.TicketEscalation;

public interface ITicketEscalationService
{
    List<SupportTicketRecord> GetTickets(Guid tenantId, SupportTier? tier = null, EscalationState? state = null);
    SupportTicketRecord? GetTicketById(Guid tenantId, Guid ticketId);
    SupportTicketRecord CreateTicket(CreateTicketRequest request);
    SupportTicketRecord? EscalateTicket(EscalateTicketRequest request);
    SupportTicketRecord? ResolveTicket(ResolveTicketRequest request);
    SuggestCannedMacroResponse SuggestMacro(Guid tenantId, Guid ticketId);
    TicketEscalationOverviewReport GetOverviewReport(Guid tenantId);
    void Reset();
}

public class TicketEscalationService : ITicketEscalationService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid SeedTicket1Id = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid SeedTicket2Id = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid SeedTicket3Id = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid SeedTicket4Id = Guid.Parse("10000000-0000-0000-0000-000000000004");
    public static readonly Guid SeedTicket5Id = Guid.Parse("10000000-0000-0000-0000-000000000005");

    private readonly ConcurrentDictionary<Guid, SupportTicketRecord> _tickets = new();
    private readonly object _syncLock = new();
    private int _ticketCounter = 1040;

    public TicketEscalationService()
    {
        SeedDefaultData();
    }

    public void Reset()
    {
        lock (_syncLock)
        {
            _tickets.Clear();
            _ticketCounter = 1040;
            SeedDefaultData();
        }
    }

    public List<SupportTicketRecord> GetTickets(Guid tenantId, SupportTier? tier = null, EscalationState? state = null)
    {
        return _tickets.Values
            .Where(t => t.TenantId == tenantId)
            .Where(t => tier == null || t.CurrentTier == tier)
            .Where(t => state == null || t.State == state)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.SlaDueAtUtc)
            .ToList();
    }

    public SupportTicketRecord? GetTicketById(Guid tenantId, Guid ticketId)
    {
        if (_tickets.TryGetValue(ticketId, out var ticket) && ticket.TenantId == tenantId)
        {
            return ticket;
        }

        return null;
    }

    public SupportTicketRecord CreateTicket(CreateTicketRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new ArgumentException("Subject is required.", nameof(request));
        }

        var ticketId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var ticketNum = $"TICK-{now:yyyy}-{Interlocked.Increment(ref _ticketCounter):D4}";

        var slaMinutes = request.Priority switch
        {
            TicketPriority.Urgent => 30,
            TicketPriority.High => 60,
            TicketPriority.Medium => 240,
            TicketPriority.Low => 1440,
            _ => 120
        };

        var initialTier = SupportTier.Tier1_Frontline;
        var initialState = request.Priority == TicketPriority.Urgent
            ? EscalationState.WarningSlaDueSoon
            : EscalationState.Normal;

        var initialAgent = request.Category switch
        {
            "Billing" or "Invoicing" => "Sara Al-Harbi (Billing Triage)",
            "Integration" or "API" => "Hala Al-Mansoor (API Specialist)",
            "Performance" => "Zaid Al-Najjar (Performance Triage)",
            _ => "Nouf Al-Qurashi (Frontline Desk)"
        };

        var initialLog = new TicketEscalationLogRecord(
            Guid.NewGuid(),
            ticketId,
            initialTier,
            initialTier,
            $"Ticket created with priority {request.Priority}: {request.InitialNotes}",
            request.CustomerName,
            now
        );

        var ticket = new SupportTicketRecord(
            ticketId,
            request.TenantId,
            ticketNum,
            request.Subject,
            request.CustomerName,
            request.CustomerEmail,
            request.CompanyName,
            request.Priority,
            initialTier,
            initialState,
            request.Sentiment,
            initialAgent,
            request.Category,
            now,
            now.AddMinutes(slaMinutes),
            null,
            null,
            null,
            [initialLog],
            ["Inbound", request.Category, request.Priority.ToString()]
        );

        _tickets[ticketId] = ticket;
        return ticket;
    }

    public SupportTicketRecord? EscalateTicket(EscalateTicketRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_tickets.TryGetValue(request.TicketId, out var existing) || existing.TenantId != request.TenantId)
        {
            return null;
        }

        if (existing.State == EscalationState.Resolved)
        {
            return existing;
        }

        var now = DateTime.UtcNow;
        var newState = request.TargetTier switch
        {
            SupportTier.Tier2_TechnicalSpecialist => EscalationState.EscalatedTier2,
            SupportTier.Tier3_EngineeringOps => EscalationState.EscalatedTier3,
            SupportTier.ExecutiveOnCall => EscalationState.EscalatedTier3,
            _ => EscalationState.Normal
        };

        var newAgent = request.TargetTier switch
        {
            SupportTier.Tier2_TechnicalSpecialist => "Zaid Al-Najjar (Tier 2 Technical Specialist)",
            SupportTier.Tier3_EngineeringOps => "DevOps Core SWAT Desk",
            SupportTier.ExecutiveOnCall => "VP Customer Success Executive On-Call",
            _ => existing.AssignedAgent
        };

        var log = new TicketEscalationLogRecord(
            Guid.NewGuid(),
            request.TicketId,
            existing.CurrentTier,
            request.TargetTier,
            request.Reason,
            request.EscalatedBy,
            now
        );

        var updatedLogs = new List<TicketEscalationLogRecord>(existing.EscalationLogs) { log };
        var updatedTags = new HashSet<string>(existing.Tags, StringComparer.OrdinalIgnoreCase)
        {
            $"Escalated-{request.TargetTier}"
        }.ToList();

        var updated = existing with
        {
            CurrentTier = request.TargetTier,
            State = newState,
            AssignedAgent = newAgent,
            EscalationLogs = updatedLogs,
            Tags = updatedTags
        };

        _tickets[request.TicketId] = updated;
        return updated;
    }

    public SupportTicketRecord? ResolveTicket(ResolveTicketRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_tickets.TryGetValue(request.TicketId, out var existing) || existing.TenantId != request.TenantId)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var log = new TicketEscalationLogRecord(
            Guid.NewGuid(),
            request.TicketId,
            existing.CurrentTier,
            existing.CurrentTier,
            $"Ticket marked resolved: {request.ResolutionNotes}",
            request.ResolvedBy,
            now
        );

        var updatedLogs = new List<TicketEscalationLogRecord>(existing.EscalationLogs) { log };
        var updatedTags = new HashSet<string>(existing.Tags, StringComparer.OrdinalIgnoreCase)
        {
            "Resolved"
        }.ToList();

        var updated = existing with
        {
            State = EscalationState.Resolved,
            ResolvedAtUtc = now,
            ResolutionNotes = request.ResolutionNotes,
            EscalationLogs = updatedLogs,
            Tags = updatedTags
        };

        _tickets[request.TicketId] = updated;
        return updated;
    }

    public SuggestCannedMacroResponse SuggestMacro(Guid tenantId, Guid ticketId)
    {
        var ticket = GetTicketById(tenantId, ticketId)
            ?? throw new KeyNotFoundException($"Support ticket '{ticketId}' not found for tenant '{tenantId}'.");

        return ticket.Sentiment switch
        {
            CustomerSentiment.CriticalChurnRisk => new SuggestCannedMacroResponse(
                ticket.TicketId,
                "Executive Incident Hotline & High-Priority Bridge",
                $"Dear {ticket.CustomerName},\n\nWe recognize the critical operational impact on {ticket.CompanyName} regarding '{ticket.Subject}'. This case has been escalated directly to our Tier 3 Engineering SWAT and Executive Command Desk. Our team is actively investigating with root-cause checkpoints scheduled every 20 minutes until complete restoration.",
                ticket.Sentiment,
                [
                    "Establish live bridge with Customer Success Executive",
                    "Engage SRE On-Call lead for diagnostics",
                    "Issue proactive SLA service-credit memo"
                ],
                0.98
            ),
            CustomerSentiment.Frustrated => new SuggestCannedMacroResponse(
                ticket.TicketId,
                "Specialist Deep-Dive & Root-Cause Assurance",
                $"Hello {ticket.CustomerName},\n\nThank you for reaching out. We apologize for the friction caused by '{ticket.Subject}'. A dedicated Tier 2 Technical Specialist has taken full ownership of your ticket and is reviewing system telemetry and error logs right now. We will provide our next diagnostic update within 45 minutes.",
                ticket.Sentiment,
                [
                    "Query telemetry cluster for correlated trace spans",
                    "Verify tenant webhook retry queues",
                    "Deliver interim update within 45 minutes"
                ],
                0.94
            ),
            _ => new SuggestCannedMacroResponse(
                ticket.TicketId,
                "Standard Resolution Walkthrough & Verification",
                $"Hello {ticket.CustomerName},\n\nThank you for contacting SGrowing Support. We have verified your request for '{ticket.Subject}'. Our support team has analyzed the configuration and prepared actionable resolution steps for your account at {ticket.CompanyName}.",
                ticket.Sentiment,
                [
                    "Walk through settings verification steps",
                    "Validate API credentials and tenant permissions",
                    "Confirm customer resolution and solicit CSAT feedback"
                ],
                0.91
            )
        };
    }

    public TicketEscalationOverviewReport GetOverviewReport(Guid tenantId)
    {
        var tickets = GetTickets(tenantId);
        var activeTickets = tickets.Where(t => t.State != EscalationState.Resolved).ToList();
        var totalActive = activeTickets.Count;

        var now = DateTime.UtcNow;
        var warningSla = activeTickets.Count(t =>
            t.State == EscalationState.WarningSlaDueSoon ||
            t.SlaDueAtUtc <= now.AddMinutes(30));

        var tier2Count = activeTickets.Count(t => t.State == EscalationState.EscalatedTier2);
        var tier3Count = activeTickets.Count(t => t.State == EscalationState.EscalatedTier3);

        var todayUtc = now.Date;
        var resolvedToday = tickets.Count(t =>
            t.State == EscalationState.Resolved &&
            t.ResolvedAtUtc.HasValue &&
            t.ResolvedAtUtc.Value >= todayUtc);

        var totalEvaluated = tickets.Count;
        var breachedCount = tickets.Count(t =>
            (t.ResolvedAtUtc.HasValue && t.ResolvedAtUtc.Value > t.SlaDueAtUtc) ||
            (!t.ResolvedAtUtc.HasValue && now > t.SlaDueAtUtc));

        var compliance = totalEvaluated > 0
            ? Math.Round(((double)(totalEvaluated - breachedCount) / totalEvaluated) * 100.0, 1)
            : 100.0;

        return new TicketEscalationOverviewReport(
            tenantId,
            totalActive,
            warningSla,
            tier2Count,
            tier3Count,
            resolvedToday,
            compliance,
            tickets
        );
    }

    private void SeedDefaultData()
    {
        var now = DateTime.UtcNow;

        // Ticket 1: Tier 1 Warning SLA Due Soon
        var t1 = new SupportTicketRecord(
            SeedTicket1Id,
            DefaultTenantId,
            "TICK-2026-1041",
            "Payment Webhook Delivery Delay in Jeddah Hub",
            "Tariq Al-Otaibi",
            "tariq@redsealogistics.sa",
            "RedSea Logistics KSA",
            TicketPriority.High,
            SupportTier.Tier1_Frontline,
            EscalationState.WarningSlaDueSoon,
            CustomerSentiment.Frustrated,
            "Hala Al-Mansoor (Frontline Desk)",
            "Webhooks",
            now.AddMinutes(-45),
            now.AddMinutes(15),
            now.AddMinutes(-35),
            null,
            null,
            [
                new TicketEscalationLogRecord(
                    Guid.NewGuid(),
                    SeedTicket1Id,
                    SupportTier.Tier1_Frontline,
                    SupportTier.Tier1_Frontline,
                    "Ticket logged via customer portal. SLA clock running.",
                    "System Ingestion",
                    now.AddMinutes(-45)
                )
            ],
            ["Inbound", "Webhooks", "High", "KSA-West"]
        );

        // Ticket 2: Tier 2 Escalated Technical
        var t2 = new SupportTicketRecord(
            SeedTicket2Id,
            DefaultTenantId,
            "TICK-2026-1042",
            "Database Replication Timeout on Bulk Order Ingest",
            "Khalid Al-Harbi",
            "khalid.harbi@rajhicommerce.com",
            "Al-Rajhi Retail Group",
            TicketPriority.Urgent,
            SupportTier.Tier2_TechnicalSpecialist,
            EscalationState.EscalatedTier2,
            CustomerSentiment.Frustrated,
            "Zaid Al-Najjar (Tier 2 Technical Specialist)",
            "Database",
            now.AddMinutes(-90),
            now.AddMinutes(-10),
            now.AddMinutes(-70),
            null,
            null,
            [
                new TicketEscalationLogRecord(
                    Guid.NewGuid(),
                    SeedTicket2Id,
                    SupportTier.Tier1_Frontline,
                    SupportTier.Tier1_Frontline,
                    "Initial inquiry received regarding API 504 timeouts.",
                    "Nouf Al-Qurashi",
                    now.AddMinutes(-90)
                ),
                new TicketEscalationLogRecord(
                    Guid.NewGuid(),
                    SeedTicket2Id,
                    SupportTier.Tier1_Frontline,
                    SupportTier.Tier2_TechnicalSpecialist,
                    "Query plan degradation detected on replica cluster. Escalated to Tier 2 specialist.",
                    "Nouf Al-Qurashi",
                    now.AddMinutes(-50)
                )
            ],
            ["Inbound", "Database", "Urgent", "Escalated-Tier2"]
        );

        // Ticket 3: Tier 3 Engineering Ops / SWAT
        var t3 = new SupportTicketRecord(
            SeedTicket3Id,
            DefaultTenantId,
            "TICK-2026-1043",
            "Zero-Day Checkout Auth Exception for Enterprise Tenants",
            "Dr. Amina Al-Zahrani",
            "amina@saudipostcloud.gov.sa",
            "Saudi Post Cloud Infrastructure",
            TicketPriority.Urgent,
            SupportTier.Tier3_EngineeringOps,
            EscalationState.EscalatedTier3,
            CustomerSentiment.CriticalChurnRisk,
            "DevOps Core SWAT Desk",
            "Security",
            now.AddMinutes(-180),
            now.AddMinutes(-60),
            now.AddMinutes(-160),
            null,
            null,
            [
                new TicketEscalationLogRecord(
                    Guid.NewGuid(),
                    SeedTicket3Id,
                    SupportTier.Tier1_Frontline,
                    SupportTier.Tier2_TechnicalSpecialist,
                    "Customer reported token signature validation failure across distributed nodes.",
                    "Hala Al-Mansoor",
                    now.AddMinutes(-140)
                ),
                new TicketEscalationLogRecord(
                    Guid.NewGuid(),
                    SeedTicket3Id,
                    SupportTier.Tier2_TechnicalSpecialist,
                    SupportTier.Tier3_EngineeringOps,
                    "Security key cache synchronization bug identified. Escalated to SWAT for hotfix deployment.",
                    "Zaid Al-Najjar",
                    now.AddMinutes(-100)
                )
            ],
            ["Inbound", "Security", "Urgent", "Critical-Risk", "Escalated-Tier3"]
        );

        // Ticket 4: Tier 1 Normal
        var t4 = new SupportTicketRecord(
            SeedTicket4Id,
            DefaultTenantId,
            "TICK-2026-1044",
            "Custom Domain DNS Validation Guidance Request",
            "Sami Bakhsh",
            "sami@riyadhretail.com",
            "Riyadh Retail Holdings",
            TicketPriority.Low,
            SupportTier.Tier1_Frontline,
            EscalationState.Normal,
            CustomerSentiment.Positive,
            "Nouf Al-Qurashi (Frontline Desk)",
            "Configuration",
            now.AddMinutes(-20),
            now.AddHours(23),
            now.AddMinutes(-15),
            null,
            null,
            [
                new TicketEscalationLogRecord(
                    Guid.NewGuid(),
                    SeedTicket4Id,
                    SupportTier.Tier1_Frontline,
                    SupportTier.Tier1_Frontline,
                    "DNS record validation steps provided to client administrator.",
                    "Nouf Al-Qurashi",
                    now.AddMinutes(-20)
                )
            ],
            ["Inbound", "Configuration", "Low"]
        );

        // Ticket 5: Resolved
        var t5 = new SupportTicketRecord(
            SeedTicket5Id,
            DefaultTenantId,
            "TICK-2026-1040",
            "Arabic Fiscal Invoice RTL Formatting Fixed",
            "Maryam Al-Sabah",
            "maryam@gulftrading.com",
            "Gulf Trading Enterprises",
            TicketPriority.Medium,
            SupportTier.Tier1_Frontline,
            EscalationState.Resolved,
            CustomerSentiment.Positive,
            "Hala Al-Mansoor (Frontline Desk)",
            "Billing",
            now.AddHours(-6),
            now.AddHours(-2),
            now.AddHours(-5),
            now.AddHours(-1),
            "Adjusted CSS stylesheet direction to RTL and regenerated ZATCA compliant invoice QR code.",
            [
                new TicketEscalationLogRecord(
                    Guid.NewGuid(),
                    SeedTicket5Id,
                    SupportTier.Tier1_Frontline,
                    SupportTier.Tier1_Frontline,
                    "Report of inverted table columns in generated PDF invoices.",
                    "System Ingestion",
                    now.AddHours(-6)
                ),
                new TicketEscalationLogRecord(
                    Guid.NewGuid(),
                    SeedTicket5Id,
                    SupportTier.Tier1_Frontline,
                    SupportTier.Tier1_Frontline,
                    "Ticket marked resolved: Adjustments deployed and validated with customer.",
                    "Hala Al-Mansoor",
                    now.AddHours(-1)
                )
            ],
            ["Inbound", "Billing", "ZATCA", "Resolved"]
        );

        _tickets[SeedTicket1Id] = t1;
        _tickets[SeedTicket2Id] = t2;
        _tickets[SeedTicket3Id] = t3;
        _tickets[SeedTicket4Id] = t4;
        _tickets[SeedTicket5Id] = t5;
    }
}
