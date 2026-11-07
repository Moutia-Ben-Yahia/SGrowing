using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.Outbound.LinkedInCadence;

/// <summary>
/// Service contract for LinkedIn B2B Outreach and Social Selling Cadence Studio (Domain D4, D8 & F2).
/// </summary>
public interface ILinkedInCadenceService
{
    Task<IReadOnlyList<LinkedInCampaignCadence>> GetCadencesAsync(Guid tenantId, CancellationToken ct = default);
    Task<LinkedInCampaignCadence> CreateCadenceAsync(CreateCadenceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LinkedInCadenceStepRecord>> GetCadenceStepsAsync(Guid tenantId, Guid cadenceId, CancellationToken ct = default);
    Task<LinkedInCadenceStepRecord?> ExecuteStepAsync(ExecuteCadenceStepRequest request, CancellationToken ct = default);
    Task<DraftMessageAiResponse> DraftAiMessageAsync(DraftMessageAiRequest request, CancellationToken ct = default);
    Task<LinkedInCadenceOverviewReport> GetOverviewReportAsync(Guid tenantId, CancellationToken ct = default);

    // Synchronous convenience overloads
    IReadOnlyList<LinkedInCampaignCadence> GetCadences(Guid tenantId);
    LinkedInCampaignCadence CreateCadence(CreateCadenceRequest request);
    IReadOnlyList<LinkedInCadenceStepRecord> GetCadenceSteps(Guid tenantId, Guid cadenceId);
    LinkedInCadenceStepRecord? ExecuteStep(ExecuteCadenceStepRequest request);
    DraftMessageAiResponse DraftAiMessage(DraftMessageAiRequest request);
    LinkedInCadenceOverviewReport GetOverviewReport(Guid tenantId);
}

/// <summary>
/// In-memory thread-safe implementation of LinkedIn Social Selling Cadence Engine with strict tenant isolation
/// and anti-bot pacing rate limits.
/// </summary>
public sealed class LinkedInCadenceService : ILinkedInCadenceService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultCadenceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly ConcurrentDictionary<Guid, List<LinkedInCampaignCadence>> _cadencesByTenant = new();
    private readonly ConcurrentDictionary<Guid, List<LinkedInCadenceStepRecord>> _stepsByCadence = new();
    private readonly ConcurrentDictionary<Guid, int> _dailyQuotaUsedByTenant = new();
    private readonly object _syncLock = new();

    public LinkedInCadenceService()
    {
        SeedDefaultTenantData();
    }

    private void SeedDefaultTenantData()
    {
        var seededCadence = new LinkedInCampaignCadence(
            CadenceId: DefaultCadenceId,
            TenantId: DefaultTenantId,
            Name: "GCC Enterprise SaaS Decision-Makers (KSA & UAE)",
            TargetPersona: "Enterprise CTOs, CISOs & Heads of Digital Transformation",
            Description: "Multi-touch outbound social selling cadence designed for high-ACV enterprise accounts in Riyadh, Dubai, and Abu Dhabi with GCC compliance hooks and bilingual AI personalization.",
            IsActive: true,
            DailyActionLimit: 40,
            CreatedAtUtc: DateTime.UtcNow.AddDays(-14),
            Steps: Array.Empty<LinkedInCadenceStepRecord>(),
            ActiveProspectCount: 5,
            ConnectionRatePercent: 42.5,
            ReplyRatePercent: 28.0
        );

        var prospect1 = new LinkedInProspectProfile(
            ProspectId: Guid.Parse("33333333-3333-3333-3333-333333333331"),
            FullName: "Tariq Al-Mansoor",
            JobTitle: "VP of Digital Transformation",
            CompanyName: "Saudi Telecom Cloud Services",
            Seniority: ProspectSeniority.VP,
            LinkedInProfileUrl: "https://www.linkedin.com/in/tariq-almansoor-ksa",
            Location: "Riyadh, Saudi Arabia",
            Industry: "Telecommunications & Cloud Infrastructure",
            Connected: true,
            CompanyDomain: "stccloud.com.sa",
            EnrichedBio: "Leading national hyperscale cloud adoption, sovereign AI initiatives, and digital infrastructure across KSA.",
            RecentInterests: new[] { "Saudi Vision 2030", "Cloud Sovereignty", "PDPL Compliance" }
        );

        var prospect2 = new LinkedInProspectProfile(
            ProspectId: Guid.Parse("33333333-3333-3333-3333-333333333332"),
            FullName: "Noura Al-Zahrani",
            JobTitle: "Head of Enterprise Architecture",
            CompanyName: "SABIC Digital",
            Seniority: ProspectSeniority.Director,
            LinkedInProfileUrl: "https://www.linkedin.com/in/noura-alzahrani-sabic",
            Location: "Al-Khobar, Saudi Arabia",
            Industry: "Industrial Chemicals & Manufacturing",
            Connected: false,
            CompanyDomain: "sabic.com",
            EnrichedBio: "Architecting event-driven SAP integrations and low-latency industrial edge computing for SABIC manufacturing sites.",
            RecentInterests: new[] { "Industrial IoT", "Event-Driven Architecture", "Enterprise AI" }
        );

        var prospect3 = new LinkedInProspectProfile(
            ProspectId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            FullName: "Faisal Bin Rashid",
            JobTitle: "Chief Technology Officer",
            CompanyName: "Emirates FinTech Holdings",
            Seniority: ProspectSeniority.CXO,
            LinkedInProfileUrl: "https://www.linkedin.com/in/faisal-binrashid-difc",
            Location: "Dubai, United Arab Emirates",
            Industry: "Financial Services & FinTech",
            Connected: false,
            CompanyDomain: "emiratesfintech.ae",
            EnrichedBio: "Pioneering real-time instant payment schemes and ISO 20022 compliance across GCC banking rails.",
            RecentInterests: new[] { "DIFC Innovation Hub", "Instant Payments", "Microservices" }
        );

        var prospect4 = new LinkedInProspectProfile(
            ProspectId: Guid.Parse("33333333-3333-3333-3333-333333333334"),
            FullName: "Sara Al-Otaibi",
            JobTitle: "Director of Cloud Infrastructure",
            CompanyName: "Aramco Digital Ventures",
            Seniority: ProspectSeniority.Director,
            LinkedInProfileUrl: "https://www.linkedin.com/in/sara-alotaibi-aramco",
            Location: "Dhahran, Saudi Arabia",
            Industry: "Energy & Industrial Automation",
            Connected: false,
            CompanyDomain: "aramcodigital.sa",
            EnrichedBio: "Orchestrating hybrid cloud and sovereign security policies for critical infrastructure operations.",
            RecentInterests: new[] { "Cybersecurity", "Zero Trust", "Cloud Native" }
        );

        var prospect5 = new LinkedInProspectProfile(
            ProspectId: Guid.Parse("33333333-3333-3333-3333-333333333335"),
            FullName: "Omar Al-Ghamdi",
            JobTitle: "IT Operations Manager",
            CompanyName: "Al Rajhi Banking Group",
            Seniority: ProspectSeniority.Manager,
            LinkedInProfileUrl: "https://www.linkedin.com/in/omar-alghamdi-alrajhi",
            Location: "Riyadh, Saudi Arabia",
            Industry: "Banking & Financial Technology",
            Connected: false,
            CompanyDomain: "alrajhibank.com.sa",
            EnrichedBio: "Managing high-availability core banking systems and SAMA cybersecurity regulatory frameworks.",
            RecentInterests: new[] { "SAMA Regulations", "High Availability", "DevSecOps" }
        );

        var steps = new List<LinkedInCadenceStepRecord>
        {
            // Prospect 1 (Tariq Al-Mansoor)
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444401"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect1.ProspectId,
                Prospect: prospect1,
                DayOffset: 1,
                TouchType: LinkedInTouchType.ProfileView,
                Status: CadenceStepStatus.Sent,
                MessageTemplate: "View profile & interaction with recent Vision 2030 cloud infrastructure announcement.",
                CustomizedContent: "Warm profile view from Riyadh residential IP node with 72s dwell time.",
                ExecutionTimestampUtc: DateTime.UtcNow.AddDays(-3),
                PacingDelaySeconds: 45,
                AiPersonalizationScore: 0.94,
                ResponseReceived: null
            ),
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444402"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect1.ProspectId,
                Prospect: prospect1,
                DayOffset: 2,
                TouchType: LinkedInTouchType.PostEngagement,
                Status: CadenceStepStatus.Sent,
                MessageTemplate: "Like & comment on Saudi data residency compliance framework post.",
                CustomizedContent: "Insightful contribution on KSA PDPL requirements and latency optimizations.",
                ExecutionTimestampUtc: DateTime.UtcNow.AddDays(-2),
                PacingDelaySeconds: 75,
                AiPersonalizationScore: 0.96,
                ResponseReceived: null
            ),
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444403"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect1.ProspectId,
                Prospect: prospect1,
                DayOffset: 4,
                TouchType: LinkedInTouchType.ConnectionRequest,
                Status: CadenceStepStatus.Replied,
                MessageTemplate: "Tariq, following STC Cloud's regional expansion with great interest. Would value connecting with fellow GCC infrastructure leaders.",
                CustomizedContent: "Tariq, following STC Cloud's sovereign data initiatives with high admiration. Would love to share our GCC enterprise benchmarks with your architecture team.",
                ExecutionTimestampUtc: DateTime.UtcNow.AddDays(-1),
                PacingDelaySeconds: 120,
                AiPersonalizationScore: 0.98,
                ResponseReceived: "Thanks for reaching out! Let's connect and review the benchmark report next week."
            ),

            // Prospect 2 (Noura Al-Zahrani)
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444404"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect2.ProspectId,
                Prospect: prospect2,
                DayOffset: 1,
                TouchType: LinkedInTouchType.ProfileView,
                Status: CadenceStepStatus.Sent,
                MessageTemplate: "View profile and review enterprise architecture publications.",
                CustomizedContent: "Profile view logged from Khobar edge hub.",
                ExecutionTimestampUtc: DateTime.UtcNow.AddDays(-2),
                PacingDelaySeconds: 60,
                AiPersonalizationScore: 0.91,
                ResponseReceived: null
            ),
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444405"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect2.ProspectId,
                Prospect: prospect2,
                DayOffset: 2,
                TouchType: LinkedInTouchType.PostEngagement,
                Status: CadenceStepStatus.Sent,
                MessageTemplate: "Engage with latest industrial IoT integration case study.",
                CustomizedContent: "Great synthesis on edge-to-cloud telemetry in heavy manufacturing. Agreed on sub-5ms SLA necessity.",
                ExecutionTimestampUtc: DateTime.UtcNow.AddHours(-18),
                PacingDelaySeconds: 90,
                AiPersonalizationScore: 0.93,
                ResponseReceived: null
            ),
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444406"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect2.ProspectId,
                Prospect: prospect2,
                DayOffset: 4,
                TouchType: LinkedInTouchType.ConnectionRequest,
                Status: CadenceStepStatus.PendingApproval,
                MessageTemplate: "Noura, your work on SABIC's event-driven architecture is inspiring. Would love to share our GCC benchmark report.",
                CustomizedContent: "Noura, your work scaling SABIC's distributed event brokers is remarkable. Would value connecting and sharing our recent benchmark on industrial data mesh resilience.",
                ExecutionTimestampUtc: null,
                PacingDelaySeconds: 110,
                AiPersonalizationScore: 0.95,
                ResponseReceived: null
            ),
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444407"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect2.ProspectId,
                Prospect: prospect2,
                DayOffset: 7,
                TouchType: LinkedInTouchType.PersonalizedFollowUp,
                Status: CadenceStepStatus.Scheduled,
                MessageTemplate: "Noura, following up on our connection—here is our benchmark on microservice latency under high load.",
                CustomizedContent: null,
                ExecutionTimestampUtc: null,
                PacingDelaySeconds: 120,
                AiPersonalizationScore: 0.92,
                ResponseReceived: null
            ),

            // Prospect 3 (Faisal Bin Rashid)
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444408"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect3.ProspectId,
                Prospect: prospect3,
                DayOffset: 1,
                TouchType: LinkedInTouchType.ProfileView,
                Status: CadenceStepStatus.Sent,
                MessageTemplate: "Targeted profile view ahead of DIFC FinTech Summit.",
                CustomizedContent: "Profile review and recent article scan on UAE Central Bank sandbox.",
                ExecutionTimestampUtc: DateTime.UtcNow.AddHours(-6),
                PacingDelaySeconds: 60,
                AiPersonalizationScore: 0.95,
                ResponseReceived: null
            ),
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444409"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect3.ProspectId,
                Prospect: prospect3,
                DayOffset: 3,
                TouchType: LinkedInTouchType.ConnectionRequest,
                Status: CadenceStepStatus.PendingApproval,
                MessageTemplate: "Faisal, impressed by Emirates FinTech's cross-border settlement latency. Would value connecting with you.",
                CustomizedContent: "Faisal, thrilled to see Emirates FinTech's cross-border settlement milestones in DIFC. Connecting to exchange notes on distributed ledger throughput.",
                ExecutionTimestampUtc: null,
                PacingDelaySeconds: 130,
                AiPersonalizationScore: 0.97,
                ResponseReceived: null
            ),
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444410"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect3.ProspectId,
                Prospect: prospect3,
                DayOffset: 6,
                TouchType: LinkedInTouchType.InMailMessage,
                Status: CadenceStepStatus.Scheduled,
                MessageTemplate: "Executive briefing: Accelerating real-time ISO20022 compliance in the UAE.",
                CustomizedContent: null,
                ExecutionTimestampUtc: null,
                PacingDelaySeconds: 180,
                AiPersonalizationScore: 0.94,
                ResponseReceived: null
            ),

            // Prospect 4 (Sara Al-Otaibi)
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444411"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect4.ProspectId,
                Prospect: prospect4,
                DayOffset: 1,
                TouchType: LinkedInTouchType.ProfileView,
                Status: CadenceStepStatus.Sent,
                MessageTemplate: "Warm profile view from Dhahran residential cluster.",
                CustomizedContent: "Dwell time 68s with skill endorsement check.",
                ExecutionTimestampUtc: DateTime.UtcNow.AddHours(-2),
                PacingDelaySeconds: 50,
                AiPersonalizationScore: 0.92,
                ResponseReceived: null
            ),
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444412"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect4.ProspectId,
                Prospect: prospect4,
                DayOffset: 2,
                TouchType: LinkedInTouchType.PostEngagement,
                Status: CadenceStepStatus.PendingApproval,
                MessageTemplate: "High-value engagement drafted for Aramco industrial AI post.",
                CustomizedContent: "Remarkable initiative by Aramco Digital on cyber-physical plant isolation. Truly sovereign.",
                ExecutionTimestampUtc: null,
                PacingDelaySeconds: 85,
                AiPersonalizationScore: 0.94,
                ResponseReceived: null
            ),

            // Prospect 5 (Omar Al-Ghamdi)
            new(
                StepId: Guid.Parse("44444444-4444-4444-4444-444444444413"),
                CadenceId: DefaultCadenceId,
                ProspectId: prospect5.ProspectId,
                Prospect: prospect5,
                DayOffset: 1,
                TouchType: LinkedInTouchType.ProfileView,
                Status: CadenceStepStatus.Scheduled,
                MessageTemplate: "Scheduled warm profile view with randomized anti-bot jitter.",
                CustomizedContent: null,
                ExecutionTimestampUtc: null,
                PacingDelaySeconds: 55,
                AiPersonalizationScore: 0.89,
                ResponseReceived: null
            )
        };

        _cadencesByTenant[DefaultTenantId] = new List<LinkedInCampaignCadence>
        {
            seededCadence with { Steps = steps }
        };

        _stepsByCadence[DefaultCadenceId] = steps;
        _dailyQuotaUsedByTenant[DefaultTenantId] = 14; // 14 actions executed today
    }

    public Task<IReadOnlyList<LinkedInCampaignCadence>> GetCadencesAsync(Guid tenantId, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<LinkedInCampaignCadence>>(GetCadences(tenantId));
    }

    public IReadOnlyList<LinkedInCampaignCadence> GetCadences(Guid tenantId)
    {
        lock (_syncLock)
        {
            if (_cadencesByTenant.TryGetValue(tenantId, out var list))
            {
                // Refresh steps in each cadence
                return list.Select(c =>
                {
                    if (_stepsByCadence.TryGetValue(c.CadenceId, out var steps))
                    {
                        var activeProspects = steps.Select(s => s.ProspectId).Distinct().Count();
                        var connectionSteps = steps.Where(s => s.TouchType == LinkedInTouchType.ConnectionRequest).ToList();
                        var replies = steps.Count(s => s.Status == CadenceStepStatus.Replied);
                        var sentCount = steps.Count(s => s.Status == CadenceStepStatus.Sent || s.Status == CadenceStepStatus.Replied);

                        var connRate = connectionSteps.Count > 0
                            ? Math.Round((double)connectionSteps.Count(s => s.Status == CadenceStepStatus.Sent || s.Status == CadenceStepStatus.Replied) / connectionSteps.Count * 100.0, 1)
                            : c.ConnectionRatePercent;

                        var replyRate = sentCount > 0
                            ? Math.Round((double)replies / sentCount * 100.0, 1)
                            : c.ReplyRatePercent;

                        return c with
                        {
                            Steps = steps.ToList(),
                            ActiveProspectCount = activeProspects > 0 ? activeProspects : c.ActiveProspectCount,
                            ConnectionRatePercent = connRate,
                            ReplyRatePercent = replyRate
                        };
                    }
                    return c;
                }).ToList();
            }

            return Array.Empty<LinkedInCampaignCadence>();
        }
    }

    public Task<LinkedInCampaignCadence> CreateCadenceAsync(CreateCadenceRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(CreateCadence(request));
    }

    public LinkedInCampaignCadence CreateCadence(CreateCadenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Cadence name must not be empty.", nameof(request));
        }

        lock (_syncLock)
        {
            var cadenceId = Guid.NewGuid();
            var stepTemplates = request.InitialSteps ?? new List<CreateCadenceStepDto>
            {
                new(1, LinkedInTouchType.ProfileView, "Warm profile view with automated jitter", 60),
                new(2, LinkedInTouchType.PostEngagement, "Like and contextual thought leadership comment", 90),
                new(4, LinkedInTouchType.ConnectionRequest, "Personalized value-first connection request", 120),
                new(7, LinkedInTouchType.PersonalizedFollowUp, "Bilingual follow-up with enterprise benchmark asset", 150),
                new(11, LinkedInTouchType.InMailMessage, "Executive InMail invitation for peer benchmarking", 180)
            };

            var initialSteps = stepTemplates.Select(st => new LinkedInCadenceStepRecord(
                StepId: Guid.NewGuid(),
                CadenceId: cadenceId,
                ProspectId: Guid.Empty,
                Prospect: null,
                DayOffset: st.DayOffset,
                TouchType: st.TouchType,
                Status: CadenceStepStatus.Scheduled,
                MessageTemplate: st.MessageTemplate,
                CustomizedContent: null,
                ExecutionTimestampUtc: null,
                PacingDelaySeconds: st.PacingDelaySeconds,
                AiPersonalizationScore: 0.90,
                ResponseReceived: null
            )).ToList();

            var newCadence = new LinkedInCampaignCadence(
                CadenceId: cadenceId,
                TenantId: request.TenantId,
                Name: request.Name,
                TargetPersona: request.TargetPersona,
                Description: request.Description,
                IsActive: true,
                DailyActionLimit: request.DailyActionLimit > 0 ? request.DailyActionLimit : 50,
                CreatedAtUtc: DateTime.UtcNow,
                Steps: initialSteps,
                ActiveProspectCount: 0,
                ConnectionRatePercent: 0.0,
                ReplyRatePercent: 0.0
            );

            if (!_cadencesByTenant.TryGetValue(request.TenantId, out var cadenceList))
            {
                cadenceList = new List<LinkedInCampaignCadence>();
                _cadencesByTenant[request.TenantId] = cadenceList;
            }

            cadenceList.Add(newCadence);
            _stepsByCadence[cadenceId] = initialSteps;

            if (!_dailyQuotaUsedByTenant.ContainsKey(request.TenantId))
            {
                _dailyQuotaUsedByTenant[request.TenantId] = 0;
            }

            return newCadence;
        }
    }

    public Task<IReadOnlyList<LinkedInCadenceStepRecord>> GetCadenceStepsAsync(Guid tenantId, Guid cadenceId, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<LinkedInCadenceStepRecord>>(GetCadenceSteps(tenantId, cadenceId));
    }

    public IReadOnlyList<LinkedInCadenceStepRecord> GetCadenceSteps(Guid tenantId, Guid cadenceId)
    {
        lock (_syncLock)
        {
            // Verify tenant ownership of this cadence
            if (!_cadencesByTenant.TryGetValue(tenantId, out var cadences) ||
                !cadences.Any(c => c.CadenceId == cadenceId))
            {
                return Array.Empty<LinkedInCadenceStepRecord>();
            }

            if (_stepsByCadence.TryGetValue(cadenceId, out var steps))
            {
                return steps.OrderBy(s => s.DayOffset).ThenBy(s => s.StepId).ToList();
            }

            return Array.Empty<LinkedInCadenceStepRecord>();
        }
    }

    public Task<LinkedInCadenceStepRecord?> ExecuteStepAsync(ExecuteCadenceStepRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(ExecuteStep(request));
    }

    public LinkedInCadenceStepRecord? ExecuteStep(ExecuteCadenceStepRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_syncLock)
        {
            // Verify tenant owns the cadence
            if (!_cadencesByTenant.TryGetValue(request.TenantId, out var cadences))
            {
                return null;
            }

            var cadence = cadences.FirstOrDefault(c => c.CadenceId == request.CadenceId);
            if (cadence is null)
            {
                return null;
            }

            if (!_stepsByCadence.TryGetValue(request.CadenceId, out var steps))
            {
                return null;
            }

            var index = steps.FindIndex(s => s.StepId == request.StepId);
            if (index == -1)
            {
                return null;
            }

            var existingStep = steps[index];

            // Anti-bot pacing limit verification
            var currentQuotaUsed = _dailyQuotaUsedByTenant.GetOrAdd(request.TenantId, 0);
            if (currentQuotaUsed >= cadence.DailyActionLimit)
            {
                throw new InvalidOperationException(
                    $"Daily anti-bot pacing limit reached ({cadence.DailyActionLimit} actions/day). Step queued to prevent LinkedIn account restriction.");
            }

            // Increase quota used
            _dailyQuotaUsedByTenant[request.TenantId] = currentQuotaUsed + 1;

            var newStatus = request.MarkAsReplied ? CadenceStepStatus.Replied : CadenceStepStatus.Sent;
            var customizedContent = !string.IsNullOrWhiteSpace(request.ApprovedContent)
                ? request.ApprovedContent
                : existingStep.CustomizedContent ?? existingStep.MessageTemplate;

            var updatedStep = existingStep with
            {
                Status = newStatus,
                CustomizedContent = customizedContent,
                ExecutionTimestampUtc = DateTime.UtcNow,
                ResponseReceived = request.MarkAsReplied ? "Prospect replied to social touchpoint." : existingStep.ResponseReceived
            };

            steps[index] = updatedStep;
            return updatedStep;
        }
    }

    public Task<DraftMessageAiResponse> DraftAiMessageAsync(DraftMessageAiRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(DraftAiMessage(request));
    }

    public DraftMessageAiResponse DraftAiMessage(DraftMessageAiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isArabic = string.Equals(request.Language, "ar", StringComparison.OrdinalIgnoreCase);
        var painPoint = !string.IsNullOrWhiteSpace(request.KeyPainPoint)
            ? request.KeyPainPoint
            : isArabic ? "تحقيق الامتثال السحابي وتقليل زمن استجابة الأنظمة المؤسسية" : "achieving regulatory compliance and lowering enterprise system latency";

        string subject;
        string message;
        string hook;
        List<string> hashtags;
        int waitMinutes;
        double confidence;

        switch (request.TouchType)
        {
            case LinkedInTouchType.ProfileView:
                waitMinutes = 15;
                confidence = 0.94;
                hashtags = new() { "#GCCEnterprise", "#DigitalTransformation" };
                if (isArabic)
                {
                    subject = "متابعة الملف الشخصي - اهتمام مشترك";
                    hook = $"الاطلاع على مبادرات {request.CompanyName} في التحول الرقمي";
                    message = $"أهلاً بك أستاذ {request.ProspectName}، تشرفت بالاطلاع على مسيرتكم الرائدة في {request.CompanyName} وخاصة فيما يتعلق بـ {painPoint}.";
                }
                else
                {
                    subject = "Profile Interaction - Shared Enterprise Focus";
                    hook = $"Tracking {request.CompanyName}'s strategic initiatives in {request.Industry}";
                    message = $"Hello {request.ProspectName}, reviewed your recent updates regarding {request.CompanyName}'s technology milestones, particularly around {painPoint}. Impressive leadership.";
                }
                break;

            case LinkedInTouchType.ConnectionRequest:
                waitMinutes = 30;
                confidence = 0.97;
                hashtags = new() { "#Vision2030", "#SaudiTech", "#FinTechGCC", "#CloudResilience" };
                if (isArabic)
                {
                    subject = "طلب تواصل - شبكة قيادات التقنية الخليجية";
                    hook = $"التوافق مع مستهدفات رؤية 2030 وتحديثات {request.CompanyName}";
                    message = $"السلام عليكم أستاذ {request.ProspectName}، يسعدني التواصل معكم لمتابعة تجاربكم في {request.CompanyName}، وتبادل الرؤى حول أفضل ممارسات {painPoint} في البيئة المؤسسية الخليجية.";
                }
                else
                {
                    subject = "Connection Request - GCC Technology Leadership";
                    hook = $"Alignment with GCC digital modernization and {request.CompanyName}'s architecture";
                    message = $"Hi {request.ProspectName}, following {request.CompanyName}'s growth with high admiration. Connecting to exchange peer benchmarks on solving {painPoint} across the GCC enterprise ecosystem.";
                }
                break;

            case LinkedInTouchType.PostEngagement:
                waitMinutes = 20;
                confidence = 0.95;
                hashtags = new() { "#EnterpriseArchitecture", "#GCCBusiness", "#Leadership" };
                if (isArabic)
                {
                    subject = "مشاركة تعليق نوعي على منشور القيادة";
                    hook = $"تحليل معمق يدعم أفكار {request.ProspectName} المنشورة";
                    message = $"طرح قيّم جداً أستاذ {request.ProspectName}. بالفعل أثبتت التجربة في السوق الإقليمي أن معالجة {painPoint} هي الأساس لتسريع العائد على الاستثمار التقني.";
                }
                else
                {
                    subject = "Thought Leadership Insight Commentary";
                    hook = $"Value-add comment addressing {request.ProspectName}'s recent published viewpoints";
                    message = $"Exceptional insights, {request.ProspectName}. In our recent GCC benchmark data, prioritizing {painPoint} reduced operational friction by over 38% for enterprise leaders.";
                }
                break;

            case LinkedInTouchType.PersonalizedFollowUp:
                waitMinutes = 45;
                confidence = 0.96;
                hashtags = new() { "#CaseStudy", "#EnterpriseBenchmark", "#GCCSuccess" };
                if (isArabic)
                {
                    subject = "تقرير مقارن حصري: قياس أداء المنظومات الرقمية في الخليج";
                    hook = $"دراسة حالة حصرية لقطاع {request.Industry}";
                    message = $"مرحباً أستاذ {request.ProspectName}، شكراً لتواصلكم الكريم. أرفق لكم دراسة حالة موجزة حول كيفية مساعدة قيادات {request.Industry} في التغلب على {painPoint} بكفاءة عالية وفق المعايير المحلية.";
                }
                else
                {
                    subject = "Exclusive Benchmark: GCC Enterprise System Scalability";
                    hook = $"Dedicated case study for {request.Industry} decision-makers";
                    message = $"Hi {request.ProspectName}, thanks for connecting. Sharing our newly released GCC benchmark briefing highlighting how {request.Industry} enterprises are successfully addressing {painPoint}. Happy to share full findings.";
                }
                break;

            case LinkedInTouchType.InMailMessage:
            default:
                waitMinutes = 60;
                confidence = 0.93;
                hashtags = new() { "#ExecutiveBriefing", "#GCCExpansion", "#CTOCommunity" };
                if (isArabic)
                {
                    subject = $"إحاطة تنفيذية موجزة: أثر حلول {painPoint} في {request.CompanyName}";
                    hook = $"جلسة حوارية مخصصة للرؤساء التنفيذيين في الرياض ودبي";
                    message = $"تحية طيبة أستاذ {request.ProspectName}، بصفتكم {request.JobTitle} في {request.CompanyName}، نود دعوتكم لجلسة نقاشية مغلقة لـ 15 دقيقة لاستعراض كيف تعالج المؤسسات الكبرى في المملكة والإمارات تحديات {painPoint}. هل يناسبكم الثلاثاء القادم؟";
                }
                else
                {
                    subject = $"Executive Briefing: Accelerating Resilience for {request.CompanyName}";
                    hook = $"Direct peer-to-peer 15-minute briefing invitation for {request.Seniority} leaders";
                    message = $"Dear {request.ProspectName}, given your leadership as {request.JobTitle} at {request.CompanyName}, I would welcome a concise 15-minute peer exchange to discuss how leading GCC firms tackle {painPoint}. Let me know if midweek works for you.";
                }
                break;
        }

        return new DraftMessageAiResponse(
            DraftedSubject: subject,
            DraftedMessage: message,
            TouchType: request.TouchType,
            PersonalizationHook: hook,
            PacingWaitMinutes: waitMinutes,
            ConfidenceScore: confidence,
            RecommendedHashtags: hashtags
        );
    }

    public Task<LinkedInCadenceOverviewReport> GetOverviewReportAsync(Guid tenantId, CancellationToken ct = default)
    {
        return Task.FromResult(GetOverviewReport(tenantId));
    }

    public LinkedInCadenceOverviewReport GetOverviewReport(Guid tenantId)
    {
        lock (_syncLock)
        {
            var cadences = GetCadences(tenantId);
            var allSteps = cadences.SelectMany(c => c.Steps).ToList();

            var totalCadences = cadences.Count;
            var activeProspects = allSteps.Select(s => s.ProspectId).Where(id => id != Guid.Empty).Distinct().Count();
            var totalExecuted = allSteps.Count(s => s.Status == CadenceStepStatus.Sent || s.Status == CadenceStepStatus.Replied);
            var pendingApproval = allSteps.Count(s => s.Status == CadenceStepStatus.PendingApproval);

            var avgConnRate = cadences.Count > 0 ? Math.Round(cadences.Average(c => c.ConnectionRatePercent), 1) : 0.0;
            var avgReplyRate = cadences.Count > 0 ? Math.Round(cadences.Average(c => c.ReplyRatePercent), 1) : 0.0;

            var dailyQuotaUsed = _dailyQuotaUsedByTenant.GetOrAdd(tenantId, 0);
            var dailyQuotaLimit = cadences.Count > 0 ? cadences.Max(c => c.DailyActionLimit) : 50;

            string pacingStatus = dailyQuotaUsed >= dailyQuotaLimit
                ? $"Daily Quota Exhausted ({dailyQuotaUsed}/{dailyQuotaLimit}) - Pacing safety locked until next 24h cycle"
                : $"Optimal ({dailyQuotaUsed}/{dailyQuotaLimit} Actions Used) - Anti-bot jitter active (45-180s delays)";

            var recentSteps = allSteps
                .Where(s => s.ExecutionTimestampUtc.HasValue)
                .OrderByDescending(s => s.ExecutionTimestampUtc)
                .Take(10)
                .ToList();

            return new LinkedInCadenceOverviewReport(
                TenantId: tenantId,
                TotalCadences: totalCadences,
                ActiveProspectsCount: activeProspects,
                TotalStepsExecuted: totalExecuted,
                PendingApprovalCount: pendingApproval,
                AvgConnectionRatePercent: avgConnRate,
                AvgReplyRatePercent: avgReplyRate,
                PacingQueueStatus: pacingStatus,
                DailyQuotaUsed: dailyQuotaUsed,
                DailyQuotaLimit: dailyQuotaLimit,
                RecentSteps: recentSteps
            );
        }
    }
}
