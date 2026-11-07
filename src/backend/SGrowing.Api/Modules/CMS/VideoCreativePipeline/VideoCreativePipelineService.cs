using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.CMS.VideoCreativePipeline;

public interface IVideoCreativePipelineService
{
    VideoAdCampaignProject CreateProject(CreateVideoProjectRequest request);
    IReadOnlyList<VideoAdCampaignProject> GetProjects(Guid tenantId);
    VideoAdCampaignProject? GetProject(Guid tenantId, Guid projectId);
    IReadOnlyList<CreativeVariantRecord> GenerateVariants(GenerateVariantsRequest request);
    IReadOnlyList<CreativeVariantRecord> GetVariants(Guid tenantId, Guid projectId);
    SynthesizedDialectScriptResponse SynthesizeDialectScript(SynthesizeDialectScriptRequest request);
}

public class VideoCreativePipelineService : IVideoCreativePipelineService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ConcurrentDictionary<Guid, VideoAdCampaignProject> _projects = new();
    private readonly ConcurrentDictionary<Guid, List<CreativeVariantRecord>> _projectVariants = new();

    public VideoCreativePipelineService()
    {
        SeedDefaultTenantProjects();
    }

    private void SeedDefaultTenantProjects()
    {
        // Seeded Project 1: Ramadan Luxury Oud Collection - TikTok Hook
        var p1Id = Guid.Parse("a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d");
        var p1Scenes = new List<VideoStoryboardScene>
        {
            new(
                SceneNumber: 1,
                Stage: AdCreativeStage.HookIntro,
                VisualPrompt: "Cinematic close-up of dark vintage agarwood resin bubbling on charcoal, amber smoke rising with golden Ramadan crescent bokeh in background",
                VoiceoverScript: "وش رايك في عطر يثبت فخامتك بكل مجلس برمضان؟ أبشر بعزك مع دهن العود الملكي المعتّق",
                DurationSeconds: 4,
                BackgroundTrack: "Ambient Oud & Subtle Reverb",
                TextOverlayText: "وش رايك في الفخامة الحقيقية؟ 🌙"
            ),
            new(
                SceneNumber: 2,
                Stage: AdCreativeStage.ProblemStatement,
                VisualPrompt: "Frustrated Gulf shopper looking at regular perfumes that evaporate within 30 minutes in Saudi heat and crowded Taraweeh gatherings",
                VoiceoverScript: "العطور التجارية تطير بربع ساعة وما تثبت بحرارة الصيف أو زحمة التراويح",
                DurationSeconds: 5,
                BackgroundTrack: "Low Tension Strings",
                TextOverlayText: "العطور العادية ما تدوم؟ ⏳"
            ),
            new(
                SceneNumber: 3,
                Stage: AdCreativeStage.ProductShowcase,
                VisualPrompt: "Ultra-luxury 3D rotating crystal bottle of Royal Heritage Aged Cambodi Oud with 24k gold leaf and velvet presentation box",
                VoiceoverScript: "خلطتنا النادرة من دهن عود كمبودي صافي معتق لأكثر من 15 سنة، تركيز زيت 100% بدون كحول",
                DurationSeconds: 6,
                BackgroundTrack: "Majestic Arabic Dulcimer Beats",
                TextOverlayText: "معتق 15 سنة • ثبات 72 ساعة 🔥"
            ),
            new(
                SceneNumber: 4,
                Stage: AdCreativeStage.SocialProofTestimonial,
                VisualPrompt: "Saudi VIP client in traditional white thobe receiving compliments at Riyadh majlis gathering, verified 5-star review carousel",
                VoiceoverScript: "أكثر من 18 ألف عميل بالرياض وجدة يشهدون لثبات الرائحة لأكثر من يومين كاملة",
                DurationSeconds: 5,
                BackgroundTrack: "Inspiring Acoustic Pulse",
                TextOverlayText: "تقييم 4.9/5 من أكثر من 18,000 عميل ⭐"
            ),
            new(
                SceneNumber: 5,
                Stage: AdCreativeStage.CallToAction,
                VisualPrompt: "Dynamic animated badge: Ramadan 40% OFF with fast Riyadh delivery in 2 hours box and free luxury gift packaging",
                VoiceoverScript: "يا هلا ومسهلا! اطلب الحين قبل نفاد الكمية واستلم خلال ساعتين بالرياض مع شحن مجاني",
                DurationSeconds: 5,
                BackgroundTrack: "Energetic Arabic Trap Riser",
                TextOverlayText: "خصم 40% لرمضان • اطلب الحين! 🚀"
            )
        };

        var project1 = new VideoAdCampaignProject(
            ProjectId: p1Id,
            TenantId: DefaultTenantId,
            ProjectTitle: "Ramadan Luxury Oud Collection - TikTok Hook",
            ProductSku: "OUD-ROYAL-RAMADAN-2026",
            TargetAudience: "Affluent GCC Perfume Collectors & Ramadan Gift Buyers",
            Dialect: ArabicRegionalDialect.SaudiNajdi,
            AspectRatio: VideoAspectRatio.Vertical_9_16,
            Scenes: p1Scenes,
            TotalDurationSeconds: 25,
            EstimatedRenderCostSar: 45.00m,
            CreatedAtUtc: new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc)
        );

        _projects[p1Id] = project1;

        var p1Variants = new List<CreativeVariantRecord>
        {
            new(
                VariantId: Guid.Parse("11112222-3333-4444-5555-666677778881"),
                ProjectId: p1Id,
                TenantId: DefaultTenantId,
                VariantCode: "VAR-NAJ-01",
                HookText: "وش رايك في عطر يثبت فخامتك بكل مجلس برمضان؟",
                CtaText: "أبشر بعزك! اطلب الحين واستلم بساعتين 🚀",
                BanditExplorationScore: 0.88,
                PredictedCtrPercent: 5.42,
                GeneratedAtUtc: new DateTime(2026, 11, 2, 8, 30, 0, DateTimeKind.Utc)
            ),
            new(
                VariantId: Guid.Parse("11112222-3333-4444-5555-666677778882"),
                ProjectId: p1Id,
                TenantId: DefaultTenantId,
                VariantCode: "VAR-NAJ-02",
                HookText: "عطورك تطير بربع ساعة؟ دهن العود الملكي يثبت 72 ساعة",
                CtaText: "لا يفوتك عرض رمضان 40% خصم 🌙",
                BanditExplorationScore: 0.65,
                PredictedCtrPercent: 4.85,
                GeneratedAtUtc: new DateTime(2026, 11, 2, 8, 35, 0, DateTimeKind.Utc)
            ),
            new(
                VariantId: Guid.Parse("11112222-3333-4444-5555-666677778883"),
                ProjectId: p1Id,
                TenantId: DefaultTenantId,
                VariantCode: "VAR-NAJ-03",
                HookText: "يا هلا ومسهلا! سر الفخامة الملكية صار بين إيديك",
                CtaText: "اطلب الحين قبل نفاد الكمية وشحن مجاني 📦",
                BanditExplorationScore: 0.42,
                PredictedCtrPercent: 4.15,
                GeneratedAtUtc: new DateTime(2026, 11, 2, 8, 40, 0, DateTimeKind.Utc)
            )
        };
        _projectVariants[p1Id] = p1Variants;

        // Seeded Project 2: Smart Cloud POS for Cafes & Restaurants
        var p2Id = Guid.Parse("b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e");
        var p2Scenes = new List<VideoStoryboardScene>
        {
            new(
                SceneNumber: 1,
                Stage: AdCreativeStage.HookIntro,
                VisualPrompt: "Fast-paced split screen of long cafe queues vs seamless NFC tap payment on modern tablet POS",
                VoiceoverScript: "شنو رايك بطابور يخلص بثواني وفواتيرك كلها مربوطة مباشرة مع الزكاة والضريبة؟",
                DurationSeconds: 4,
                BackgroundTrack: "Upbeat Modern Tech Groove",
                TextOverlayText: "وداعاً لزحمة الكاشير! ☕💳"
            ),
            new(
                SceneNumber: 2,
                Stage: AdCreativeStage.ProblemStatement,
                VisualPrompt: "Cafe owner stressed over tablet crashing during rush hour and missing order tickets",
                VoiceoverScript: "الأنظمة القديمة تعلق وقت الذروة وتضيع الطلبات وتخرب تجربة الزبون",
                DurationSeconds: 5,
                BackgroundTrack: "Dynamic Synths & Hi-Hats",
                TextOverlayText: "النظام علق وقت الذروة؟ ⚠️"
            ),
            new(
                SceneNumber: 3,
                Stage: AdCreativeStage.ProductShowcase,
                VisualPrompt: "Sleek dual-screen cloud POS showing live inventory sync, ZATCA Phase 2 QR generation, and split-bill flow",
                VoiceoverScript: "نظامنا السحابي الذكي يشتغل أوفلاين وأونلاين، تقارير مبيعات لحظية وإدارة طاولات متكاملة",
                DurationSeconds: 6,
                BackgroundTrack: "Confident Tech Momentum",
                TextOverlayText: "سحابي • معتمد من هيئة الزكاة والضريبة 📊"
            ),
            new(
                SceneNumber: 4,
                Stage: AdCreativeStage.CallToAction,
                VisualPrompt: "Customer smiling holding takeaway coffee cup, clean POS screen displaying 14-day free trial CTA",
                VoiceoverScript: "فالك طيب! سجل الحين وجرّب النظام مجاناً لمدة 14 يوم بدون أي التزام مالي",
                DurationSeconds: 5,
                BackgroundTrack: "Smooth Corporate Finish",
                TextOverlayText: "جرّب مجاناً 14 يوم • فالك طيب 💼"
            )
        };

        var project2 = new VideoAdCampaignProject(
            ProjectId: p2Id,
            TenantId: DefaultTenantId,
            ProjectTitle: "Smart Cloud POS for Cafes & Restaurants",
            ProductSku: "POS-CLOUD-CAFE-ENTERPRISE",
            TargetAudience: "F&B Business Owners & Coffee Shop Managers in GCC",
            Dialect: ArabicRegionalDialect.GulfKhaleeji,
            AspectRatio: VideoAspectRatio.Square_1_1,
            Scenes: p2Scenes,
            TotalDurationSeconds: 20,
            EstimatedRenderCostSar: 35.00m,
            CreatedAtUtc: new DateTime(2026, 11, 3, 14, 0, 0, DateTimeKind.Utc)
        );

        _projects[p2Id] = project2;

        var p2Variants = new List<CreativeVariantRecord>
        {
            new(
                VariantId: Guid.Parse("22223333-4444-5555-6666-777788889991"),
                ProjectId: p2Id,
                TenantId: DefaultTenantId,
                VariantCode: "VAR-GUL-01",
                HookText: "شنو رايك بكاشير ذكي يخلص الطابور بثواني مع الزكاة والضريبة؟",
                CtaText: "فالك طيب! جرّب مجاناً 14 يوم بدون أي التزام ☕",
                BanditExplorationScore: 0.91,
                PredictedCtrPercent: 5.10,
                GeneratedAtUtc: new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)
            ),
            new(
                VariantId: Guid.Parse("22223333-4444-5555-6666-777788889992"),
                ProjectId: p2Id,
                TenantId: DefaultTenantId,
                VariantCode: "VAR-GUL-02",
                HookText: "تعبت من تعليق نظام الكاشير وقت ذروة المقاهي؟",
                CtaText: "انضم لأكثر من 500 كافيه وابدأ التجربة المجانية ⚡",
                BanditExplorationScore: 0.58,
                PredictedCtrPercent: 4.30,
                GeneratedAtUtc: new DateTime(2026, 11, 4, 9, 10, 0, DateTimeKind.Utc)
            )
        };
        _projectVariants[p2Id] = p2Variants;
    }

    public VideoAdCampaignProject CreateProject(CreateVideoProjectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ProjectTitle))
        {
            throw new ArgumentException("ProjectTitle cannot be empty.", nameof(request));
        }

        var scenes = request.CustomScenes != null && request.CustomScenes.Count > 0
            ? request.CustomScenes.OrderBy(s => s.SceneNumber).ToList()
            : GenerateDefaultStageScenes(request);

        var totalDuration = scenes.Sum(s => s.DurationSeconds);
        // Base setup fee 15 SAR + 1.20 SAR per second of rendered AI video
        var estimatedCost = Math.Max(15.00m, Math.Round(15.00m + (totalDuration * 1.20m), 2));

        var project = new VideoAdCampaignProject(
            ProjectId: Guid.NewGuid(),
            TenantId: request.TenantId,
            ProjectTitle: request.ProjectTitle.Trim(),
            ProductSku: string.IsNullOrWhiteSpace(request.ProductSku) ? "SKU-AUTO-01" : request.ProductSku.Trim(),
            TargetAudience: string.IsNullOrWhiteSpace(request.TargetAudience) ? "Regional Growth Audience" : request.TargetAudience.Trim(),
            Dialect: request.Dialect,
            AspectRatio: request.AspectRatio,
            Scenes: scenes,
            TotalDurationSeconds: totalDuration,
            EstimatedRenderCostSar: estimatedCost,
            CreatedAtUtc: DateTime.UtcNow
        );

        _projects[project.ProjectId] = project;
        return project;
    }

    public IReadOnlyList<VideoAdCampaignProject> GetProjects(Guid tenantId)
    {
        return _projects.Values
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();
    }

    public VideoAdCampaignProject? GetProject(Guid tenantId, Guid projectId)
    {
        if (_projects.TryGetValue(projectId, out var project) && project.TenantId == tenantId)
        {
            return project;
        }

        return null;
    }

    public IReadOnlyList<CreativeVariantRecord> GetVariants(Guid tenantId, Guid projectId)
    {
        var project = GetProject(tenantId, projectId);
        if (project is null)
        {
            return Array.Empty<CreativeVariantRecord>();
        }

        if (_projectVariants.TryGetValue(projectId, out var variants))
        {
            return variants.Where(v => v.TenantId == tenantId).ToList();
        }

        return Array.Empty<CreativeVariantRecord>();
    }

    public IReadOnlyList<CreativeVariantRecord> GenerateVariants(GenerateVariantsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var project = GetProject(request.TenantId, request.ProjectId);
        if (project is null)
        {
            throw new KeyNotFoundException($"Video project {request.ProjectId} not found for tenant {request.TenantId}.");
        }

        var count = request.VariantCount <= 0 ? 3 : Math.Min(request.VariantCount, 15);
        var existingVariants = _projectVariants.GetOrAdd(request.ProjectId, _ => new List<CreativeVariantRecord>());

        var newVariants = new List<CreativeVariantRecord>();
        var dialectPrefix = GetDialectPrefix(project.Dialect);

        for (int i = 1; i <= count; i++)
        {
            var variantIndex = existingVariants.Count + i;
            var variantCode = $"VAR-{dialectPrefix}-{variantIndex:D2}";
            var (hook, cta) = GenerateVariantCopy(project.Dialect, project.ProjectTitle, variantIndex);

            // Multi-armed bandit UCB exploration score and predictive CTR
            var explorationScore = Math.Round(0.35 + (0.60 * (1.0 / Math.Sqrt(variantIndex))), 2);
            var predictedCtr = Math.Round(3.50 + ((variantIndex % 5) * 0.55), 2);

            var variant = new CreativeVariantRecord(
                VariantId: Guid.NewGuid(),
                ProjectId: project.ProjectId,
                TenantId: request.TenantId,
                VariantCode: variantCode,
                HookText: hook,
                CtaText: cta,
                BanditExplorationScore: explorationScore,
                PredictedCtrPercent: predictedCtr,
                GeneratedAtUtc: DateTime.UtcNow
            );

            newVariants.Add(variant);
        }

        lock (existingVariants)
        {
            existingVariants.AddRange(newVariants);
        }

        return newVariants;
    }

    public SynthesizedDialectScriptResponse SynthesizeDialectScript(SynthesizeDialectScriptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var standardScript = string.IsNullOrWhiteSpace(request.StandardScript)
            ? "ما رأيك في تجربة منتجنا المميز اليوم؟ نحن هنا لخدمتكم، لا تفوتوا فرصة الاستفادة من العرض الحصري."
            : request.StandardScript.Trim();

        var idiomsUsed = new HashSet<string>(StringComparer.Ordinal);
        var localized = standardScript;

        switch (request.TargetDialect)
        {
            case ArabicRegionalDialect.SaudiNajdi:
                localized = ReplaceToken(localized, ["ما رأيك", "هل تعلم", "ما رأيكم", "what do you think"], "وش رايك", idiomsUsed);
                localized = ReplaceToken(localized, ["يسرنا", "بكل سرور", "يسعدنا", "نحن هنا لخدمتكم", "we are happy to help"], "أبشر بعزك", idiomsUsed);
                localized = ReplaceToken(localized, ["مرحباً", "أهلاً وسهلاً", "أهلاً", "welcome"], "يا هلا ومسهلا", idiomsUsed);
                localized = ReplaceToken(localized, ["لا تفوت", "لا تفوتوا", "سارع", "سارعوا", "don't miss"], "لا يفوتك", idiomsUsed);
                localized = ReplaceToken(localized, ["كثيراً", "جداً", "very"], "بالحيل", idiomsUsed);
                localized = ReplaceToken(localized, ["الآن", "فوراً", "now"], "الحين", idiomsUsed);

                // Ensure key regional idioms are prominently included
                if (!idiomsUsed.Contains("يا هلا ومسهلا"))
                {
                    localized = "يا هلا ومسهلا! " + localized;
                    idiomsUsed.Add("يا هلا ومسهلا");
                }
                if (!idiomsUsed.Contains("وش رايك"))
                {
                    localized = localized + " وش رايك نجربها سوا؟";
                    idiomsUsed.Add("وش رايك");
                }
                if (!idiomsUsed.Contains("أبشر بعزك"))
                {
                    localized = localized + " أبشر بعزك بأفضل جودة.";
                    idiomsUsed.Add("أبشر بعزك");
                }
                if (!idiomsUsed.Contains("لا يفوتك"))
                {
                    localized = localized + " لا يفوتك العرض!";
                    idiomsUsed.Add("لا يفوتك");
                }
                break;

            case ArabicRegionalDialect.SaudiHejazi:
                localized = ReplaceToken(localized, ["ما رأيك", "ما الأمر", "ما القصة", "ما المشكلة", "what's going on"], "إيش الهرجة", idiomsUsed);
                localized = ReplaceToken(localized, ["سيدي", "عزيزي", "يا صديقي", "sir"], "يا سيدي", idiomsUsed);
                localized = ReplaceToken(localized, ["كما تريد", "حسب رغبتك", "كما تحب", "as you wish"], "على كيفك", idiomsUsed);
                localized = ReplaceToken(localized, ["الآن", "فوراً", "حالياً", "now"], "دحين", idiomsUsed);
                localized = ReplaceToken(localized, ["لا داعي", "بدون شك", "no need"], "ما يحتاج", idiomsUsed);

                if (!idiomsUsed.Contains("إيش الهرجة"))
                {
                    localized = "إيش الهرجة يا سيدي؟ " + localized;
                    idiomsUsed.Add("إيش الهرجة");
                    idiomsUsed.Add("يا سيدي");
                }
                if (!idiomsUsed.Contains("على كيفك"))
                {
                    localized = localized + " وكل شيء على كيفك دحين!";
                    idiomsUsed.Add("على كيفك");
                    idiomsUsed.Add("دحين");
                }
                break;

            case ArabicRegionalDialect.GulfKhaleeji:
                localized = ReplaceToken(localized, ["ما رأيك", "ماذا تعتقد", "what do you think"], "شنو رايك", idiomsUsed);
                localized = ReplaceToken(localized, ["يسرنا", "أبشر", "بكل سرور", "يسعدنا", "you're welcome"], "فالك طيب", idiomsUsed);
                localized = ReplaceToken(localized, ["كثيراً", "جداً", "a lot"], "وايد", idiomsUsed);
                localized = ReplaceToken(localized, ["الآن", "فوراً", "now"], "الحين", idiomsUsed);
                localized = ReplaceToken(localized, ["شكراً", "thank you"], "ما تقصر", idiomsUsed);

                if (!idiomsUsed.Contains("شنو رايك"))
                {
                    localized = "شنو رايك؟ " + localized;
                    idiomsUsed.Add("شنو رايك");
                }
                if (!idiomsUsed.Contains("فالك طيب"))
                {
                    localized = localized + " وفالك طيب ويانا وايد!";
                    idiomsUsed.Add("فالك طيب");
                    idiomsUsed.Add("وايد");
                }
                break;

            case ArabicRegionalDialect.Egyptian:
                localized = ReplaceToken(localized, ["ما رأيك", "what do you think"], "إيه رأيك", idiomsUsed);
                localized = ReplaceToken(localized, ["سيدي", "يا صديقي", "عزيزي"], "يا باشا", idiomsUsed);
                localized = ReplaceToken(localized, ["الآن", "فوراً"], "دلوقتي", idiomsUsed);
                localized = ReplaceToken(localized, ["كثيراً", "جداً"], "أوي", idiomsUsed);
                localized = ReplaceToken(localized, ["بالتوفيق", "مبارك"], "على البركة", idiomsUsed);
                localized = ReplaceToken(localized, ["لا يفوتك", "لا تفوتوا"], "ما يتفوتش", idiomsUsed);

                if (!idiomsUsed.Contains("إيه رأيك"))
                {
                    localized = "يا باشا إيه رأيك؟ " + localized;
                    idiomsUsed.Add("يا باشا");
                    idiomsUsed.Add("إيه رأيك");
                }
                if (!idiomsUsed.Contains("دلوقتي"))
                {
                    localized = localized + " العرض ما يتفوتش دلوقتي خالص!";
                    idiomsUsed.Add("ما يتفوتش");
                    idiomsUsed.Add("دلوقتي");
                }
                break;

            case ArabicRegionalDialect.ModernStandardArabic:
            default:
                idiomsUsed.Add("ما رأيكم");
                idiomsUsed.Add("أهلاً وسهلاً");
                idiomsUsed.Add("الآن وحصرياً");
                if (!localized.Contains("أهلاً وسهلاً"))
                {
                    localized = "أهلاً وسهلاً بكم! " + localized;
                }
                break;
        }

        return new SynthesizedDialectScriptResponse(
            TargetDialect: request.TargetDialect,
            StandardScript: standardScript,
            LocalizedScript: localized,
            DialectIdiomsUsed: idiomsUsed.ToList()
        );
    }

    private static string ReplaceToken(string text, string[] searchTokens, string replacement, HashSet<string> idiomsUsed)
    {
        bool replaced = false;
        foreach (var token in searchTokens)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                text = text.Replace(token, replacement, StringComparison.OrdinalIgnoreCase);
                replaced = true;
            }
        }

        if (replaced)
        {
            idiomsUsed.Add(replacement);
        }

        return text;
    }

    private static List<VideoStoryboardScene> GenerateDefaultStageScenes(CreateVideoProjectRequest request)
    {
        return new List<VideoStoryboardScene>
        {
            new(
                SceneNumber: 1,
                Stage: AdCreativeStage.HookIntro,
                VisualPrompt: $"Cinematic dynamic hook introducing {request.ProductSku} tailored for {request.TargetAudience}, vibrant contrast lighting",
                VoiceoverScript: GetDialectHookIntro(request.Dialect, request.ProjectTitle),
                DurationSeconds: 5,
                BackgroundTrack: "Energetic Riser Beat",
                TextOverlayText: $"اكتشف {request.ProjectTitle} 🔥"
            ),
            new(
                SceneNumber: 2,
                Stage: AdCreativeStage.ProblemStatement,
                VisualPrompt: $"Frustrated target customer persona representing {request.TargetAudience} facing traditional market hassles",
                VoiceoverScript: GetDialectProblem(request.Dialect),
                DurationSeconds: 5,
                BackgroundTrack: "Low Tension Underscore",
                TextOverlayText: "المشكلة اللي الكل يعاني منها ⏳"
            ),
            new(
                SceneNumber: 3,
                Stage: AdCreativeStage.ProductShowcase,
                VisualPrompt: $"Macro 3D reveal of {request.ProductSku} detailing key advantages and premium finish",
                VoiceoverScript: GetDialectShowcase(request.Dialect, request.ProductSku),
                DurationSeconds: 7,
                BackgroundTrack: "Melodic Synth Groove",
                TextOverlayText: $"الحل الأذكى: {request.ProductSku} ⭐"
            ),
            new(
                SceneNumber: 4,
                Stage: AdCreativeStage.SocialProofTestimonial,
                VisualPrompt: $"Delighted regional customers with verified ratings badge and high social credibility",
                VoiceoverScript: GetDialectSocialProof(request.Dialect),
                DurationSeconds: 5,
                BackgroundTrack: "Inspiring Acoustic Pulse",
                TextOverlayText: "موثوق ومجرّب من آلاف العملاء 🏆"
            ),
            new(
                SceneNumber: 5,
                Stage: AdCreativeStage.CallToAction,
                VisualPrompt: $"Bold limited-time promotional CTA badge with fast regional shipping guarantee",
                VoiceoverScript: GetDialectCta(request.Dialect),
                DurationSeconds: 5,
                BackgroundTrack: "Upbeat Climax Finish",
                TextOverlayText: "اطلب الآن واستفد من العرض! 🚀"
            )
        };
    }

    private static string GetDialectHookIntro(ArabicRegionalDialect dialect, string title) => dialect switch
    {
        ArabicRegionalDialect.SaudiNajdi => $"يا هلا ومسهلا! وش رايك في {title} اللي يغيّر المعادلة بالكامل؟",
        ArabicRegionalDialect.SaudiHejazi => $"إيش الهرجة يا سيدي؟ مع {title} كل مشاكلك لها حل جذري!",
        ArabicRegionalDialect.GulfKhaleeji => $"شنو رايك بتجربة غير تماماً مع {title}؟",
        ArabicRegionalDialect.Egyptian => $"إيه رأيك يا باشا في {title} اللي هيكسر الدنيا؟",
        _ => $"ما رأيكم في تجربة استثنائية مع {title}؟"
    };

    private static string GetDialectProblem(ArabicRegionalDialect dialect) => dialect switch
    {
        ArabicRegionalDialect.SaudiNajdi => "دايم تتعب مع الحلول القديمة اللي تضيّع وقتك وفلوسك بدون نتيجة تبرد الخاطر؟",
        ArabicRegionalDialect.SaudiHejazi => "تعبت من الحلول اللي تدوخك وما تعطيك نتيجة على كيفك؟",
        ArabicRegionalDialect.GulfKhaleeji => "وايد تعانون من الأنظمة القديمة وبطئها وقت الذروة؟",
        ArabicRegionalDialect.Egyptian => "زهقت من الطرق التقليدية اللي بتضيع وقتك ومجهودك عالفاضي؟",
        _ => "هل تعانون من الطرق التقليدية التي تستنزف الوقت والجهد دون نتائج مرضية؟"
    };

    private static string GetDialectShowcase(ArabicRegionalDialect dialect, string sku) => dialect switch
    {
        ArabicRegionalDialect.SaudiNajdi => $"أبشر بعزك! مع {sku} بتلقى الجودة اللي تناسبك والأداء اللي يستاهل.",
        ArabicRegionalDialect.SaudiHejazi => $"ما يحتاج تلف وتدور، {sku} يقدّم لك كل الميزات اللي تحتاجها دحين.",
        ArabicRegionalDialect.GulfKhaleeji => $"فالك طيب مع {sku}، أداء سريع وموثوق يريح بالك وايد.",
        ArabicRegionalDialect.Egyptian => $"مع {sku} يا باشا، هتاخد أعلى إمكانيات وأضمن كفاءة على البركة.",
        _ => $"مع {sku} نقدم لكم الحل المتكامل بأعلى معايير الابتكار والاعتمادية."
    };

    private static string GetDialectSocialProof(ArabicRegionalDialect dialect) => dialect switch
    {
        ArabicRegionalDialect.SaudiNajdi => "أكثر من 15 ألف عميل بالمملكة جرّبوا وشهدوا بفرق التجربة وجودتها العالية.",
        ArabicRegionalDialect.SaudiHejazi => "ما يحتاج، آلاف العملاء في مكة وجدة يشيدون بالفرق الواضح وجودة الخدمة.",
        ArabicRegionalDialect.GulfKhaleeji => "آلاف الشركات والعملاء بالخليج معتمدين علينا ومستمتعين بأعلى درجات الرضا.",
        ArabicRegionalDialect.Egyptian => "آلاف العملاء في كل مكان جربوا وشهدوا بالفرق من أول يوم.",
        _ => "آلاف العملاء والشركات في منطقة الشرق الأوسط يثقون في حلولنا المبتكرة."
    };

    private static string GetDialectCta(ArabicRegionalDialect dialect) => dialect switch
    {
        ArabicRegionalDialect.SaudiNajdi => "لا يفوتك العرض! اطلب الحين واستمتع بالشحن السريع والضمان الذهبي.",
        ArabicRegionalDialect.SaudiHejazi => "روق واطلب دحين، وعلى كيفك نوصل لك بأسرع وقت!",
        ArabicRegionalDialect.GulfKhaleeji => "فالك طيب! سجل الحين وجرّب بدون أي التزام مالي.",
        ArabicRegionalDialect.Egyptian => "يلا يا باشا ما يتفوتش العرض! اطلب دلوقتي واستلم فوراً.",
        _ => "سارعوا بالطلب الآن واستفيدوا من العرض الحصري مع التوصيل السريع."
    };

    private static string GetDialectPrefix(ArabicRegionalDialect dialect) => dialect switch
    {
        ArabicRegionalDialect.SaudiNajdi => "NAJ",
        ArabicRegionalDialect.SaudiHejazi => "HEJ",
        ArabicRegionalDialect.GulfKhaleeji => "GUL",
        ArabicRegionalDialect.Egyptian => "EGY",
        _ => "MSA"
    };

    private static (string Hook, string Cta) GenerateVariantCopy(ArabicRegionalDialect dialect, string title, int index)
    {
        return (dialect, index % 4) switch
        {
            (ArabicRegionalDialect.SaudiNajdi, 0) =>
                ("وش رايك في تجربة ملكية تثبت حضورك بكل مكان؟", "أبشر بعزك! اطلب الحين واستلم خلال ساعتين 🚀"),
            (ArabicRegionalDialect.SaudiNajdi, 1) =>
                ("يا هلا ومسهلا! الحل الحقيقي اللي كان ناقصك وصل", "لا يفوتك عرض الموسم • اطلب الحين 🌙"),
            (ArabicRegionalDialect.SaudiNajdi, 2) =>
                ("عطورك ومنتجاتك ما تدوم؟ جرّب الفرق النادر", "أبشر بالخير والشحن مجاني للباب 📦"),
            (ArabicRegionalDialect.SaudiNajdi, _) =>
                ("وش رايك في عرض استثنائي ما يتكرر؟", "اضغط الرابط واستفد من الخصم فوراً ✨"),

            (ArabicRegionalDialect.SaudiHejazi, 0) =>
                ("إيش الهرجة يا سيدي؟ الحل السريع بين إيديك", "روق واطلب دحين وعلى كيفك ☕"),
            (ArabicRegionalDialect.SaudiHejazi, 1) =>
                ("ما يحتاج تلف وتدور، هنا الفخامة الحقيقية", "يا سيدي لا يفوتك العرض دحين 💫"),
            (ArabicRegionalDialect.SaudiHejazi, 2) =>
                ("ودك بشيء على كيفك وما يتعبك؟", "سجل دحين واستمتع بالخدمة 🚀"),
            (ArabicRegionalDialect.SaudiHejazi, _) =>
                ("إيش الهرجة؟ أفضل جودة وصلت لعندك", "اطلب الحين وبكل سهولة 🌟"),

            (ArabicRegionalDialect.GulfKhaleeji, 0) =>
                ("شنو رايك بطريقة تريحك وايد وتوفر وقتك؟", "فالك طيب! جرّب مجاناً 14 يوم 💼"),
            (ArabicRegionalDialect.GulfKhaleeji, 1) =>
                ("تعبت من الأنظمة البطيئة؟ هذا الحل الأنسب لك", "انضم لأكثر من 500 شريك الحين ⚡"),
            (ArabicRegionalDialect.GulfKhaleeji, 2) =>
                ("شنو رايك بأداء ما يوقف حتى بأصعب الأوقات؟", "فالك طيب مع الضمان الذهبي 🏆"),
            (ArabicRegionalDialect.GulfKhaleeji, _) =>
                ("فالك طيب ويانا وايد مع أسرع نظام بالخليج", "سجل الآن وابدأ فورا ☕"),

            (ArabicRegionalDialect.Egyptian, 0) =>
                ("إيه رأيك يا باشا في حاجة هتوفرلك وقت وفلوس؟", "اطلب دلوقتي وعلى البركة يا باشا 🚀"),
            (ArabicRegionalDialect.Egyptian, 1) =>
                ("العرض ده ما يتفوتش خالص دلوقتي!", "سجل دلوقتي واستفد من الخصم 🌟"),
            (ArabicRegionalDialect.Egyptian, 2) =>
                ("تعبت من المشاكل دي؟ الحل الأسهل هنا", "يلا بينا اطلب الحين واستلم فوراً 📦"),
            (ArabicRegionalDialect.Egyptian, _) =>
                ("يا باشا إيه رأيك في جودة ملهاش مثيل؟", "اطلب دلوقتي قبل ما يخلص العرض 🔥"),

            _ =>
                ($"ما رأيكم في تجربة استثنائية مع {title}؟", "سارعوا بالطلب الآن واستفيدوا من العرض 🚀")
        };
    }
}
