using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SGrowing.Api.Modules.CRM;

#region Domain Enums & Models

public enum DeliverabilityStatus
{
    Valid,
    Risky,
    Disposable,
    Undeliverable
}

public enum QualificationStatus
{
    SalesQualifiedLead,
    MarketingQualifiedLead,
    Unqualified
}

public record TechnographicItem(
    string Name,
    string Category, // CRM, E-commerce, Cloud, Frontend, Analytics
    int Confidence
);

public record DnsVerificationDetails(
    bool HasValidMxRecord,
    IReadOnlyList<string> MxRecords,
    int DnsLookupMs,
    bool IsDisposable,
    string? DisposableDomainMatched,
    bool SpfRecordFound,
    int SpfScore,
    bool DkimValid,
    int DkimScore,
    bool DmarcEnforced,
    string DmarcPolicy, // reject, quarantine, none, missing
    int DmarcScore,
    int DeliverabilityScore, // 0..100
    DeliverabilityStatus Status,
    string Reason
);

public record FirmographicDetails(
    string Domain,
    string RootDomain,
    string CompanyName,
    string Industry, // Fintech, E-commerce, Logistics, B2B SaaS, Healthtech, Retail
    string HeadcountTier, // 1-10, 11-50, 51-200, 201-1000, 1000+
    string EstimatedAnnualRevenue, // <$1M, $1M-$5M, $5M-$20M, $20M-$100M, $100M+
    string Headquarters,
    int FoundedYear,
    int FirmographicScore // 0..100
);

public record TechnographicDetails(
    IReadOnlyList<TechnographicItem> Technologies,
    int TechnographicScore, // 0..100
    string StackSummary
);

public record ScoreBreakdown(
    double FirmographicWeight,
    int FirmographicScore,
    double FirmographicWeighted,
    double TechnographicWeight,
    int TechnographicScore,
    double TechnographicWeighted,
    double IntentWeight,
    int IntentScore,
    double IntentWeighted,
    double DeliverabilityWeight,
    int DeliverabilityScore,
    double DeliverabilityWeighted,
    int CompositeScore
);

public record EnrichedLead(
    Guid Id,
    string Email,
    string CompanyName,
    string Domain,
    DnsVerificationDetails DnsVerification,
    FirmographicDetails Firmographics,
    TechnographicDetails Technographics,
    int IntentScore,
    int CompositeScore,
    ScoreBreakdown ScoreBreakdown,
    QualificationStatus QualificationStatus,
    string? AssignedSalesRep,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public record EnrichLeadRequest(
    string Email,
    string? CompanyName = null,
    int? IntentScore = null,
    string? IndustryHint = null,
    string? HeadcountHint = null
);

public record EmailVerificationResult(
    string Email,
    string Domain,
    DnsVerificationDetails Details
);

public record BatchRecalculateResult(
    int TotalProcessed,
    int SqlCount,
    int MqlCount,
    int UnqualifiedCount,
    double AverageScore,
    DateTime TimestampUtc
);

public record EnrichmentKpiMetrics(
    int TotalLeads,
    int SqlCount,
    int MqlCount,
    int UnqualifiedCount,
    int DisposableCount,
    double SqlRatePercent,
    double DeliverabilityPassRatePercent,
    double AverageLeadScore,
    DateTime ComputedAtUtc
);

#endregion

#region Service Interface

public interface ILeadEnrichmentRadarService
{
    Task<IReadOnlyList<EnrichedLead>> GetAllLeadsAsync(CancellationToken ct = default);
    Task<EnrichedLead?> GetLeadByIdAsync(Guid id, CancellationToken ct = default);
    Task<EnrichedLead> EnrichLeadAsync(EnrichLeadRequest request, CancellationToken ct = default);
    Task<EmailVerificationResult> VerifyEmailAsync(string email, CancellationToken ct = default);
    Task<BatchRecalculateResult> RecalculateAllScoresAsync(CancellationToken ct = default);
    Task<EnrichmentKpiMetrics> GetKpiMetricsAsync(CancellationToken ct = default);
    Task<bool> DeleteLeadAsync(Guid id, CancellationToken ct = default);
}

#endregion

#region Service Implementation

public class LeadEnrichmentRadarService : ILeadEnrichmentRadarService
{
    private readonly ConcurrentDictionary<Guid, EnrichedLead> _leads = new();
    private int _roundRobinIndex = 0;

    // Available Account Executive (AE) pool for round-robin routing of SQL leads
    public static readonly string[] SalesRepPool =
    [
        "Elena Rostova (Enterprise AE)",
        "Marcus Vance (Strategic AE)",
        "Sarah Jenkins (Senior AE)",
        "David Kim (Commercial AE)"
    ];

    // 50+ known disposable/burner email providers blacklist
    public static readonly HashSet<string> DisposableDomainsBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "tempmail.com", "10minutemail.com", "throwawaymail.com",
        "yopmail.com", "sharklasers.com", "getairmail.com", "mailnesia.com", "trashmail.com",
        "dispostable.com", "burnermail.io", "dropmail.me", "fakeinbox.com", "mytrashmailer.com",
        "mailexpire.com", "maildrop.cc", "mohmal.com", "crazymailing.com", "nada.ltd",
        "temp-mail.org", "getnada.com", "emailondeck.com", "zillamail.com", "throwawayemail.com",
        "mailcatch.com", "spambox.us", "meltmail.com", "incognitomail.org", "anonbox.net",
        "tempail.com", "generator.email", "fake-mail.org", "tempinbox.com", "mailforspam.com",
        "disbox.org", "receive-sms-free.cc", "trashmail.net", "discard.email", "spamevader.com",
        "throwaway.email", "trashmail.de", "tmpmail.org", "disposablemail.com", "tempr.email",
        "byom.de", "01482.com", "spamgourmet.com", "trashinbox.com", "mailsac.com",
        "mailnull.com", "mailgw.com", "tempmailo.com", "minutemailbox.com", "fakemailgenerator.com",
        "armyspy.com", "cuvox.de", "dayrep.com", "einrot.com", "flectone.com",
        "gustr.com", "jourrapide.com", "rhyta.com", "superrito.com", "teleworm.us"
    };

    // Public freemail providers where corporate affiliation is unverified / risky
    public static readonly HashSet<string> FreemailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "icloud.com",
        "aol.com", "zoho.com", "proton.me", "protonmail.com", "gmx.com", "mail.com"
    };

    // Multi-part country code second-level domains for root domain extraction
    private static readonly HashSet<string> MultiPartTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.uk", "org.uk", "gov.uk", "ac.uk", "com.au", "net.au", "org.au",
        "co.jp", "ne.jp", "co.nz", "com.br", "co.za", "com.sg", "com.mx"
    };

    public LeadEnrichmentRadarService()
    {
        SeedRealisticLeads();
    }

    public Task<IReadOnlyList<EnrichedLead>> GetAllLeadsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<EnrichedLead> list = _leads.Values
            .OrderByDescending(l => l.CompositeScore)
            .ThenByDescending(l => l.CreatedAtUtc)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<EnrichedLead?> GetLeadByIdAsync(Guid id, CancellationToken ct = default)
    {
        _leads.TryGetValue(id, out var lead);
        return Task.FromResult(lead);
    }

    public Task<EnrichedLead> EnrichLeadAsync(EnrichLeadRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var enriched = SynthesizeLead(
            id: Guid.NewGuid(),
            rawEmail: request.Email,
            companyNameHint: request.CompanyName,
            intentScoreInput: request.IntentScore,
            industryHint: request.IndustryHint,
            headcountHint: request.HeadcountHint,
            nowUtc: DateTime.UtcNow
        );

        _leads[enriched.Id] = enriched;
        return Task.FromResult(enriched);
    }

    public Task<EmailVerificationResult> VerifyEmailAsync(string email, CancellationToken ct = default)
    {
        var (cleanEmail, domain, rootDomain, isValidSyntax) = ParseEmailAndDomain(email);
        var dnsDetails = RunDnsDeliverabilitySimulation(cleanEmail, domain, rootDomain, isValidSyntax);
        return Task.FromResult(new EmailVerificationResult(cleanEmail, domain, dnsDetails));
    }

    public Task<BatchRecalculateResult> RecalculateAllScoresAsync(CancellationToken ct = default)
    {
        var keys = _leads.Keys.ToList();
        int sqlCount = 0;
        int mqlCount = 0;
        int unqCount = 0;
        double scoreSum = 0;

        foreach (var key in keys)
        {
            if (_leads.TryGetValue(key, out var lead))
            {
                var recalculatedBreakdown = ComputeScoreBreakdown(
                    lead.Firmographics.FirmographicScore,
                    lead.Technographics.TechnographicScore,
                    lead.IntentScore,
                    lead.DnsVerification.DeliverabilityScore
                );

                var newStatus = DetermineQualification(recalculatedBreakdown.CompositeScore);
                string? assignedRep = lead.AssignedSalesRep;

                // If lead transitioned into SQL or didn't have a rep, route round-robin
                if (newStatus == QualificationStatus.SalesQualifiedLead && string.IsNullOrWhiteSpace(assignedRep))
                {
                    assignedRep = RouteNextSalesRep();
                }
                else if (newStatus != QualificationStatus.SalesQualifiedLead)
                {
                    assignedRep = null;
                }

                var updated = lead with
                {
                    CompositeScore = recalculatedBreakdown.CompositeScore,
                    ScoreBreakdown = recalculatedBreakdown,
                    QualificationStatus = newStatus,
                    AssignedSalesRep = assignedRep,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                _leads[key] = updated;

                switch (newStatus)
                {
                    case QualificationStatus.SalesQualifiedLead:
                        sqlCount++;
                        break;
                    case QualificationStatus.MarketingQualifiedLead:
                        mqlCount++;
                        break;
                    default:
                        unqCount++;
                        break;
                }
                scoreSum += updated.CompositeScore;
            }
        }

        int total = keys.Count;
        double avgScore = total > 0 ? Math.Round(scoreSum / total, 1) : 0;

        return Task.FromResult(new BatchRecalculateResult(
            TotalProcessed: total,
            SqlCount: sqlCount,
            MqlCount: mqlCount,
            UnqualifiedCount: unqCount,
            AverageScore: avgScore,
            TimestampUtc: DateTime.UtcNow
        ));
    }

    public Task<EnrichmentKpiMetrics> GetKpiMetricsAsync(CancellationToken ct = default)
    {
        var leads = _leads.Values.ToList();
        int total = leads.Count;
        int sqlCount = leads.Count(l => l.QualificationStatus == QualificationStatus.SalesQualifiedLead);
        int mqlCount = leads.Count(l => l.QualificationStatus == QualificationStatus.MarketingQualifiedLead);
        int unqCount = leads.Count(l => l.QualificationStatus == QualificationStatus.Unqualified);
        int disposableCount = leads.Count(l => l.DnsVerification.Status == DeliverabilityStatus.Disposable);
        int validDeliverableCount = leads.Count(l => l.DnsVerification.Status == DeliverabilityStatus.Valid);

        double sqlRate = total > 0 ? Math.Round((double)sqlCount / total * 100.0, 1) : 0.0;
        double delivPassRate = total > 0 ? Math.Round((double)validDeliverableCount / total * 100.0, 1) : 0.0;
        double avgScore = total > 0 ? Math.Round(leads.Average(l => l.CompositeScore), 1) : 0.0;

        return Task.FromResult(new EnrichmentKpiMetrics(
            TotalLeads: total,
            SqlCount: sqlCount,
            MqlCount: mqlCount,
            UnqualifiedCount: unqCount,
            DisposableCount: disposableCount,
            SqlRatePercent: sqlRate,
            DeliverabilityPassRatePercent: delivPassRate,
            AverageLeadScore: avgScore,
            ComputedAtUtc: DateTime.UtcNow
        ));
    }

    public Task<bool> DeleteLeadAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_leads.TryRemove(id, out _));
    }

    #endregion

    #region Radar Domain Synthesis & Scoring Pipeline

    public static (string CleanEmail, string Domain, string RootDomain, bool IsValidSyntax) ParseEmailAndDomain(string? rawEmail)
    {
        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            return (string.Empty, string.Empty, string.Empty, false);
        }

        string cleanEmail = rawEmail.Trim().ToLowerInvariant();
        int atIndex = cleanEmail.IndexOf('@');
        if (atIndex <= 0 || atIndex == cleanEmail.Length - 1)
        {
            return (cleanEmail, string.Empty, string.Empty, false);
        }

        string localPart = cleanEmail[..atIndex];
        string domainPart = cleanEmail[(atIndex + 1)..];

        // Validate basic RFC email structure and domain
        if (domainPart.Contains(' ') || !domainPart.Contains('.') || domainPart.StartsWith('.') || domainPart.EndsWith('.'))
        {
            return (cleanEmail, domainPart, string.Empty, false);
        }

        // Extract root domain for multi-part TLD support (e.g. corp.example.co.uk -> example.co.uk)
        string rootDomain = ExtractRootDomain(domainPart);

        return (cleanEmail, domainPart, rootDomain, true);
    }

    public static string ExtractRootDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return string.Empty;
        var parts = domain.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2) return domain;

        // Check if last two segments form a known multi-part TLD (e.g. "co.uk")
        string potentialMultiTld = $"{parts[^2]}.{parts[^1]}";
        if (MultiPartTlds.Contains(potentialMultiTld) && parts.Length >= 3)
        {
            return $"{parts[^3]}.{parts[^2]}.{parts[^1]}";
        }

        // Default to last two segments (e.g. "sub.example.com" -> "example.com")
        return $"{parts[^2]}.{parts[^1]}";
    }

    public static DnsVerificationDetails RunDnsDeliverabilitySimulation(string cleanEmail, string domain, string rootDomain, bool isValidSyntax)
    {
        if (!isValidSyntax || string.IsNullOrWhiteSpace(domain))
        {
            return new DnsVerificationDetails(
                HasValidMxRecord: false,
                MxRecords: Array.Empty<string>(),
                DnsLookupMs: 12,
                IsDisposable: false,
                DisposableDomainMatched: null,
                SpfRecordFound: false,
                SpfScore: 0,
                DkimValid: false,
                DkimScore: 0,
                DmarcEnforced: false,
                DmarcPolicy: "missing",
                DmarcScore: 0,
                DeliverabilityScore: 0,
                Status: DeliverabilityStatus.Undeliverable,
                Reason: "Invalid email syntax or unresolvable domain format."
            );
        }

        // Check disposable email blacklist on both domain and rootDomain
        if (DisposableDomainsBlacklist.Contains(domain) || (!string.IsNullOrEmpty(rootDomain) && DisposableDomainsBlacklist.Contains(rootDomain)))
        {
            string matched = DisposableDomainsBlacklist.Contains(domain) ? domain : rootDomain;
            return new DnsVerificationDetails(
                HasValidMxRecord: true,
                MxRecords: new[] { $"mail.{matched}", $"smtp.{matched}" },
                DnsLookupMs: 45,
                IsDisposable: true,
                DisposableDomainMatched: matched,
                SpfRecordFound: false,
                SpfScore: 0,
                DkimValid: false,
                DkimScore: 0,
                DmarcEnforced: false,
                DmarcPolicy: "none",
                DmarcScore: 0,
                DeliverabilityScore: 5,
                Status: DeliverabilityStatus.Disposable,
                Reason: $"Domain '{matched}' is identified as a disposable or burner mailbox provider."
            );
        }

        // Check for test or unresolvable domains (e.g., .invalid, .test, .local, nonexistent-*)
        if (domain.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith(".test", StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            domain.StartsWith("nonexistent", StringComparison.OrdinalIgnoreCase))
        {
            return new DnsVerificationDetails(
                HasValidMxRecord: false,
                MxRecords: Array.Empty<string>(),
                DnsLookupMs: 120,
                IsDisposable: false,
                DisposableDomainMatched: null,
                SpfRecordFound: false,
                SpfScore: 0,
                DkimValid: false,
                DkimScore: 0,
                DmarcEnforced: false,
                DmarcPolicy: "missing",
                DmarcScore: 0,
                DeliverabilityScore: 10,
                Status: DeliverabilityStatus.Undeliverable,
                Reason: $"DNS MX query failed: Host '{domain}' has no valid mail exchange records in authoritative DNS."
            );
        }

        // Freemail checks
        if (FreemailDomains.Contains(domain) || FreemailDomains.Contains(rootDomain))
        {
            return new DnsVerificationDetails(
                HasValidMxRecord: true,
                MxRecords: new[] { $"gmail-smtp-in.l.google.com", $"alt1.gmail-smtp-in.l.google.com" },
                DnsLookupMs: 28,
                IsDisposable: false,
                DisposableDomainMatched: null,
                SpfRecordFound: true,
                SpfScore: 85,
                DkimValid: true,
                DkimScore: 85,
                DmarcEnforced: true,
                DmarcPolicy: "quarantine",
                DmarcScore: 70,
                DeliverabilityScore: 60,
                Status: DeliverabilityStatus.Risky,
                Reason: "Public webmail provider detected. Lacks dedicated corporate identity verification."
            );
        }

        // Verified Enterprise Domain
        var simulatedMx = new[]
        {
            $"aspmx.l.google.com",
            $"alt1.aspmx.l.google.com",
            $"mail.{domain}"
        };

        return new DnsVerificationDetails(
            HasValidMxRecord: true,
            MxRecords: simulatedMx,
            DnsLookupMs: 34,
            IsDisposable: false,
            DisposableDomainMatched: null,
            SpfRecordFound: true,
            SpfScore: 98,
            DkimValid: true,
            DkimScore: 95,
            DmarcEnforced: true,
            DmarcPolicy: "reject",
            DmarcScore: 100,
            DeliverabilityScore: 96,
            Status: DeliverabilityStatus.Valid,
            Reason: "Full DNS mail routing health verified: Valid MX, SPF pass, DKIM alignment, and DMARC enforcement."
        );
    }

    public static FirmographicDetails SynthesizeFirmographics(
        string domain,
        string rootDomain,
        string? companyNameHint,
        string? industryHint,
        string? headcountHint)
    {
        string companyName = companyNameHint ?? string.Empty;
        if (string.IsNullOrWhiteSpace(companyName))
        {
            var brandPart = rootDomain.Contains('.') ? rootDomain.Split('.')[0] : domain;
            companyName = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(brandPart);
        }

        // Detect or apply industry
        string industry = industryHint switch
        {
            not null and { Length: > 0 } when IsValidIndustry(industryHint) => NormalizeIndustry(industryHint),
            _ => ClassifyIndustryByDomain(domain, companyName)
        };

        // Detect or apply headcount tier
        string headcountTier = headcountHint switch
        {
            not null and { Length: > 0 } when IsValidHeadcount(headcountHint) => headcountHint,
            _ => ClassifyHeadcountByDomain(domain)
        };

        string revenueRange = MapHeadcountToRevenue(headcountTier);
        int firmoScore = CalculateFirmographicScore(headcountTier, industry, revenueRange);
        string headquarters = ClassifyHeadquarters(domain);
        int foundedYear = 2018;

        return new FirmographicDetails(
            Domain: domain,
            RootDomain: rootDomain,
            CompanyName: companyName,
            Industry: industry,
            HeadcountTier: headcountTier,
            EstimatedAnnualRevenue: revenueRange,
            Headquarters: headquarters,
            FoundedYear: foundedYear,
            FirmographicScore: firmoScore
        );
    }

    public static TechnographicDetails SynthesizeTechnographics(string domain, string industry, string headcountTier)
    {
        var stack = new List<TechnographicItem>();

        // Heuristic stack generation according to industry & scale
        if (headcountTier is "1000+" or "201-1000")
        {
            stack.Add(new TechnographicItem("Salesforce CRM", "CRM", 98));
            stack.Add(new TechnographicItem("Amazon Web Services (AWS)", "Cloud", 95));
            stack.Add(new TechnographicItem("Datadog Enterprise APM", "Analytics", 90));
            stack.Add(new TechnographicItem("Next.js / React Enterprise", "Frontend", 92));
            stack.Add(new TechnographicItem("Segment CDP", "Analytics", 88));
        }
        else if (headcountTier is "51-200")
        {
            stack.Add(new TechnographicItem("HubSpot Enterprise", "CRM", 92));
            stack.Add(new TechnographicItem("Microsoft Azure", "Cloud", 89));
            stack.Add(new TechnographicItem("Mixpanel Product Analytics", "Analytics", 85));
            stack.Add(new TechnographicItem("Angular Core", "Frontend", 88));
        }
        else if (headcountTier is "11-50")
        {
            stack.Add(new TechnographicItem("HubSpot Starter", "CRM", 80));
            stack.Add(new TechnographicItem("Google Cloud Platform", "Cloud", 82));
            stack.Add(new TechnographicItem("Google Analytics 4", "Analytics", 85));
            stack.Add(new TechnographicItem("React Vite", "Frontend", 78));
        }
        else
        {
            stack.Add(new TechnographicItem("Notion / Airtable CRM", "CRM", 65));
            stack.Add(new TechnographicItem("Vercel Edge", "Cloud", 70));
            stack.Add(new TechnographicItem("Tailwind CSS", "Frontend", 72));
        }

        // Add domain/industry-specific tech
        if (industry == "E-commerce" || industry == "Retail")
        {
            stack.Add(new TechnographicItem("Shopify Plus", "E-commerce", 96));
            stack.Add(new TechnographicItem("Klaviyo Automation", "CRM", 91));
        }
        else if (industry == "Fintech")
        {
            stack.Add(new TechnographicItem("Stripe Connect", "Cloud", 97));
            stack.Add(new TechnographicItem("Plaid Identity", "Analytics", 92));
        }

        // Compute Technographic Score based on breadth and sophistication of detected stack
        int technoScore = Math.Min(100, (int)Math.Round(stack.Average(t => t.Confidence) * (stack.Count >= 4 ? 1.0 : 0.85)));
        string summary = string.Join(", ", stack.Select(s => s.Name));

        return new TechnographicDetails(stack, technoScore, summary);
    }

    public static ScoreBreakdown ComputeScoreBreakdown(
        int firmoScore,
        int technoScore,
        int intentScore,
        int deliverabilityScore)
    {
        // Composite MQL-to-SQL Lead Scoring Formula:
        // LeadScore = min(100, floor(0.35 * S_firmo + 0.25 * S_techno + 0.25 * S_intent + 0.15 * S_deliv))
        const double wFirmo = 0.35;
        const double wTechno = 0.25;
        const double wIntent = 0.25;
        const double wDeliv = 0.15;

        double weightedFirmo = wFirmo * firmoScore;
        double weightedTechno = wTechno * technoScore;
        double weightedIntent = wIntent * intentScore;
        double weightedDeliv = wDeliv * deliverabilityScore;

        double rawComposite = weightedFirmo + weightedTechno + weightedIntent + weightedDeliv;
        int compositeScore = Math.Max(0, (int)Math.Min(100, Math.Floor(rawComposite)));

        return new ScoreBreakdown(
            FirmographicWeight: wFirmo,
            FirmographicScore: firmoScore,
            FirmographicWeighted: Math.Round(weightedFirmo, 2),
            TechnographicWeight: wTechno,
            TechnographicScore: technoScore,
            TechnographicWeighted: Math.Round(weightedTechno, 2),
            IntentWeight: wIntent,
            IntentScore: intentScore,
            IntentWeighted: Math.Round(weightedIntent, 2),
            DeliverabilityWeight: wDeliv,
            DeliverabilityScore: deliverabilityScore,
            DeliverabilityWeighted: Math.Round(weightedDeliv, 2),
            CompositeScore: compositeScore
        );
    }

    public static QualificationStatus DetermineQualification(int compositeScore)
    {
        if (compositeScore >= 75)
        {
            return QualificationStatus.SalesQualifiedLead;
        }

        if (compositeScore >= 50)
        {
            return QualificationStatus.MarketingQualifiedLead;
        }

        return QualificationStatus.Unqualified;
    }

    public string RouteNextSalesRep()
    {
        int index = Interlocked.Increment(ref _roundRobinIndex);
        int sanitizedIndex = Math.Abs(index) % SalesRepPool.Length;
        return SalesRepPool[sanitizedIndex];
    }

    private EnrichedLead SynthesizeLead(
        Guid id,
        string rawEmail,
        string? companyNameHint,
        int? intentScoreInput,
        string? industryHint,
        string? headcountHint,
        DateTime nowUtc)
    {
        var (cleanEmail, domain, rootDomain, isValidSyntax) = ParseEmailAndDomain(rawEmail);
        var dnsDetails = RunDnsDeliverabilitySimulation(cleanEmail, domain, rootDomain, isValidSyntax);
        var firmoDetails = SynthesizeFirmographics(domain, rootDomain, companyNameHint, industryHint, headcountHint);
        var technoDetails = SynthesizeTechnographics(domain, firmoDetails.Industry, firmoDetails.HeadcountTier);

        int intentScore = Math.Clamp(intentScoreInput ?? GenerateIntentScore(domain, dnsDetails.Status), 0, 100);

        var scoreBreakdown = ComputeScoreBreakdown(
            firmoDetails.FirmographicScore,
            technoDetails.TechnographicScore,
            intentScore,
            dnsDetails.DeliverabilityScore
        );

        var qualification = DetermineQualification(scoreBreakdown.CompositeScore);
        string? assignedRep = qualification == QualificationStatus.SalesQualifiedLead
            ? RouteNextSalesRep()
            : null;

        return new EnrichedLead(
            Id: id,
            Email: cleanEmail,
            CompanyName: firmoDetails.CompanyName,
            Domain: domain,
            DnsVerification: dnsDetails,
            Firmographics: firmoDetails,
            Technographics: technoDetails,
            IntentScore: intentScore,
            CompositeScore: scoreBreakdown.CompositeScore,
            ScoreBreakdown: scoreBreakdown,
            QualificationStatus: qualification,
            AssignedSalesRep: assignedRep,
            CreatedAtUtc: nowUtc,
            UpdatedAtUtc: nowUtc
        );
    }

    #endregion

    #region Classification Helpers

    private static bool IsValidIndustry(string val) =>
        val is "Fintech" or "E-commerce" or "Logistics" or "B2B SaaS" or "Healthtech" or "Retail";

    private static string NormalizeIndustry(string val)
    {
        if (val.Equals("Ecommerce", StringComparison.OrdinalIgnoreCase) || val.Equals("E-commerce", StringComparison.OrdinalIgnoreCase)) return "E-commerce";
        if (val.Equals("Fintech", StringComparison.OrdinalIgnoreCase)) return "Fintech";
        if (val.Equals("Logistics", StringComparison.OrdinalIgnoreCase)) return "Logistics";
        if (val.Equals("B2bSaas", StringComparison.OrdinalIgnoreCase) || val.Equals("B2B SaaS", StringComparison.OrdinalIgnoreCase)) return "B2B SaaS";
        if (val.Equals("Healthtech", StringComparison.OrdinalIgnoreCase)) return "Healthtech";
        if (val.Equals("Retail", StringComparison.OrdinalIgnoreCase)) return "Retail";
        return val;
    }

    private static bool IsValidHeadcount(string val) =>
        val is "1-10" or "11-50" or "51-200" or "201-1000" or "1000+";

    private static string ClassifyIndustryByDomain(string domain, string companyName)
    {
        var text = $"{domain} {companyName}".ToLowerInvariant();
        if (text.Contains("pay") || text.Contains("fin") || text.Contains("bank") || text.Contains("credit") || text.Contains("stripe") || text.Contains("capital"))
            return "Fintech";
        if (text.Contains("logistic") || text.Contains("freight") || text.Contains("ship") || text.Contains("delivery") || text.Contains("cargo") || text.Contains("fleet"))
            return "Logistics";
        if (text.Contains("health") || text.Contains("med") || text.Contains("pharma") || text.Contains("clinic") || text.Contains("bio"))
            return "Healthtech";
        if (text.Contains("shop") || text.Contains("store") || text.Contains("cart") || text.Contains("market") || text.Contains("commerce"))
            return "E-commerce";
        if (text.Contains("retail") || text.Contains("brand") || text.Contains("apparel") || text.Contains("goods"))
            return "Retail";
        return "B2B SaaS";
    }

    private static string ClassifyHeadcountByDomain(string domain)
    {
        var d = domain.ToLowerInvariant();
        if (d.Contains("enterprise") || d.Contains("global") || d.Contains("cloud.io") || d.Contains("stripe.com") || d.Contains("corp."))
            return "1000+";
        if (d.Contains("flow.co") || d.Contains("scale") || d.Contains("tech") || d.Contains("group"))
            return "201-1000";
        if (d.Contains("hub") || d.Contains("retail") || d.Contains("solutions"))
            return "51-200";
        if (d.Contains("studio") || d.Contains("agency") || d.Contains("team"))
            return "11-50";
        return "1-10";
    }

    private static string MapHeadcountToRevenue(string headcountTier) => headcountTier switch
    {
        "1000+" => "$100M+",
        "201-1000" => "$20M-$100M",
        "51-200" => "$5M-$20M",
        "11-50" => "$1M-$5M",
        _ => "<$1M"
    };

    private static int CalculateFirmographicScore(string headcountTier, string industry, string revenue)
    {
        int baseScore = headcountTier switch
        {
            "1000+" => 95,
            "201-1000" => 85,
            "51-200" => 70,
            "11-50" => 55,
            _ => 35
        };

        // Strategic industries have a slight fit multiplier
        if (industry is "Fintech" or "B2B SaaS" or "Logistics")
        {
            baseScore = Math.Min(100, baseScore + 5);
        }

        return baseScore;
    }

    private static string ClassifyHeadquarters(string domain)
    {
        if (domain.EndsWith(".co.uk") || domain.EndsWith(".uk")) return "London, United Kingdom";
        if (domain.EndsWith(".de")) return "Berlin, Germany";
        if (domain.EndsWith(".fr")) return "Paris, France";
        if (domain.EndsWith(".com.au")) return "Sydney, Australia";
        if (domain.EndsWith(".sg")) return "Singapore";
        if (domain.Contains("finovate") || domain.Contains("cloud")) return "San Francisco, CA";
        return "New York, NY";
    }

    private static int GenerateIntentScore(string domain, DeliverabilityStatus status)
    {
        if (status == DeliverabilityStatus.Disposable || status == DeliverabilityStatus.Undeliverable)
        {
            return 10;
        }

        if (status == DeliverabilityStatus.Risky)
        {
            return 35;
        }

        return 75;
    }

    #endregion

    #region Seed Realistic Initial Leads

    private void SeedRealisticLeads()
    {
        var now = DateTime.UtcNow;

        // Lead 1: SQL Enterprise Lead (Tier 1000+, Fintech)
        var lead1 = SynthesizeLead(
            id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            rawEmail: "alex.thorne@finovatecloud.io",
            companyNameHint: "Finovate Cloud Inc",
            intentScoreInput: 92,
            industryHint: "Fintech",
            headcountHint: "1000+",
            nowUtc: now.AddHours(-18)
        );
        _leads[lead1.Id] = lead1;

        // Lead 2: SQL Mid-Market Lead (Tier 201-1000, Logistics, sub-domain .co.uk)
        var lead2 = SynthesizeLead(
            id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            rawEmail: "clara.dupuis@corp.logisticsflow.co.uk",
            companyNameHint: "Logistics Flow UK",
            intentScoreInput: 82,
            industryHint: "Logistics",
            headcountHint: "201-1000",
            nowUtc: now.AddHours(-12)
        );
        _leads[lead2.Id] = lead2;

        // Lead 3: MQL Growth Lead (Tier 51-200, E-commerce, MQL score ~73)
        var lead3 = SynthesizeLead(
            id: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            rawEmail: "mark.b@nordicretailer.com",
            companyNameHint: "Nordic Retailer Hub",
            intentScoreInput: 50,
            industryHint: "E-commerce",
            headcountHint: "51-200",
            nowUtc: now.AddHours(-6)
        );
        _leads[lead3.Id] = lead3;

        // Lead 4: Unqualified Early Lead (Tier 1-10, Freemail Gmail)
        var lead4 = SynthesizeLead(
            id: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            rawEmail: "sam.freelance@gmail.com",
            companyNameHint: "Sam Creative Studio",
            intentScoreInput: 30,
            industryHint: "Retail",
            headcountHint: "1-10",
            nowUtc: now.AddHours(-3)
        );
        _leads[lead4.Id] = lead4;

        // Lead 5: Disposable / Blacklisted Lead (Mailinator burner)
        var lead5 = SynthesizeLead(
            id: Guid.Parse("55555555-5555-5555-5555-555555555555"),
            rawEmail: "burner1299@mailinator.com",
            companyNameHint: "Unknown Test Corp",
            intentScoreInput: 10,
            industryHint: "B2B SaaS",
            headcountHint: "1-10",
            nowUtc: now.AddHours(-1)
        );
        _leads[lead5.Id] = lead5;
    }

    #endregion
}
