using FluentAssertions;
using SGrowing.Api.Modules.CMS.VideoCreativePipeline;
using Xunit;

namespace SGrowing.Tests.Unit;

public class VideoCreativePipelineTests
{
    private readonly VideoCreativePipelineService _service = new();
    private static readonly Guid DefaultTenantId = VideoCreativePipelineService.DefaultTenantId;

    [Fact]
    public void SeededProjects_ShouldHaveCorrectDurationCostAndScenes()
    {
        // Act
        var projects = _service.GetProjects(DefaultTenantId);

        // Assert
        projects.Should().HaveCount(2);

        // Project 1: Ramadan Luxury Oud Collection
        var oudProject = projects.FirstOrDefault(p => p.ProjectTitle.Contains("Oud"));
        oudProject.Should().NotBeNull();
        oudProject!.Dialect.Should().Be(ArabicRegionalDialect.SaudiNajdi);
        oudProject.AspectRatio.Should().Be(VideoAspectRatio.Vertical_9_16);
        oudProject.Scenes.Should().HaveCount(5);
        oudProject.TotalDurationSeconds.Should().Be(25);
        oudProject.EstimatedRenderCostSar.Should().Be(45.00m);
        oudProject.Scenes.Select(s => s.Stage).Should().ContainInOrder(
            AdCreativeStage.HookIntro,
            AdCreativeStage.ProblemStatement,
            AdCreativeStage.ProductShowcase,
            AdCreativeStage.SocialProofTestimonial,
            AdCreativeStage.CallToAction
        );

        // Project 2: Smart Cloud POS
        var posProject = projects.FirstOrDefault(p => p.ProjectTitle.Contains("POS"));
        posProject.Should().NotBeNull();
        posProject!.Dialect.Should().Be(ArabicRegionalDialect.GulfKhaleeji);
        posProject.AspectRatio.Should().Be(VideoAspectRatio.Square_1_1);
        posProject.Scenes.Should().HaveCount(4);
        posProject.TotalDurationSeconds.Should().Be(20);
        posProject.EstimatedRenderCostSar.Should().Be(35.00m);
    }

    [Fact]
    public void CreateProject_WithDefaultStageBlueprints_ComputesTotalDurationAndRenderCost()
    {
        // Arrange
        var request = new CreateVideoProjectRequest(
            TenantId: DefaultTenantId,
            ProjectTitle: "Summer Nitro Cold Brew Coffee Ad",
            ProductSku: "COFFEE-NITRO-2026",
            TargetAudience: "Young Saudi Cafe Lovers & Office Workers",
            Dialect: ArabicRegionalDialect.SaudiNajdi,
            AspectRatio: VideoAspectRatio.Vertical_9_16,
            CustomScenes: null
        );

        // Act
        var project = _service.CreateProject(request);

        // Assert
        project.Should().NotBeNull();
        project.ProjectId.Should().NotBeEmpty();
        project.TenantId.Should().Be(DefaultTenantId);
        project.Scenes.Should().HaveCount(5);
        project.TotalDurationSeconds.Should().Be(27); // 5 + 5 + 7 + 5 + 5
        project.EstimatedRenderCostSar.Should().Be(47.40m); // 15.00 + (27 * 1.20)
        project.Scenes[0].Stage.Should().Be(AdCreativeStage.HookIntro);
        project.Scenes[0].VoiceoverScript.Should().Contain("يا هلا ومسهلا");
        project.Scenes[2].Stage.Should().Be(AdCreativeStage.ProductShowcase);
        project.Scenes[4].Stage.Should().Be(AdCreativeStage.CallToAction);
    }

    [Fact]
    public void CreateProject_WithCustomScenes_PreservesCustomTimingAndCalculatesCost()
    {
        // Arrange
        var customScenes = new List<VideoStoryboardScene>
        {
            new(
                SceneNumber: 1,
                Stage: AdCreativeStage.HookIntro,
                VisualPrompt: "Ultra fast product reveal",
                VoiceoverScript: "شوف هذا الابتكار!",
                DurationSeconds: 3,
                BackgroundTrack: "Beat 1",
                TextOverlayText: "شوف الحين!"
            ),
            new(
                SceneNumber: 2,
                Stage: AdCreativeStage.CallToAction,
                VisualPrompt: "Direct CTA link",
                VoiceoverScript: "اطلب الآن مع خصم 30%",
                DurationSeconds: 4,
                BackgroundTrack: "Beat 2",
                TextOverlayText: "اطلب فورا!"
            )
        };

        var request = new CreateVideoProjectRequest(
            TenantId: DefaultTenantId,
            ProjectTitle: "Quick Flash Sale 7s",
            ProductSku: "FLASH-SALE-7S",
            TargetAudience: "Impulse Mobile Shoppers",
            Dialect: ArabicRegionalDialect.SaudiHejazi,
            AspectRatio: VideoAspectRatio.Vertical_9_16,
            CustomScenes: customScenes
        );

        // Act
        var project = _service.CreateProject(request);

        // Assert
        project.TotalDurationSeconds.Should().Be(7);
        project.EstimatedRenderCostSar.Should().Be(23.40m); // 15.00 + (7 * 1.20)
        project.Scenes.Should().HaveCount(2);
    }

    [Fact]
    public void SynthesizeDialectScript_SaudiNajdi_InjectsVernacularIdioms()
    {
        // Arrange
        var request = new SynthesizeDialectScriptRequest(
            StandardScript: "ما رأيك في تجربة منتجنا المميز اليوم؟ يسرنا خدمتك، ولا تفوت فرصة الاستفادة من العرض.",
            TargetDialect: ArabicRegionalDialect.SaudiNajdi
        );

        // Act
        var response = _service.SynthesizeDialectScript(request);

        // Assert
        response.TargetDialect.Should().Be(ArabicRegionalDialect.SaudiNajdi);
        response.LocalizedScript.Should().Contain("وش رايك");
        response.LocalizedScript.Should().Contain("أبشر بعزك");
        response.LocalizedScript.Should().Contain("لا يفوتك");
        response.DialectIdiomsUsed.Should().Contain("وش رايك");
        response.DialectIdiomsUsed.Should().Contain("أبشر بعزك");
        response.DialectIdiomsUsed.Should().Contain("يا هلا ومسهلا");
        response.DialectIdiomsUsed.Should().Contain("لا يفوتك");
    }

    [Fact]
    public void SynthesizeDialectScript_SaudiHejazi_InjectsHejaziPhrases()
    {
        // Arrange
        var request = new SynthesizeDialectScriptRequest(
            StandardScript: "ما الأمر يا صديقي؟ يمكنك التسوق حسب رغبتك الآن ولا داعي للانتظار.",
            TargetDialect: ArabicRegionalDialect.SaudiHejazi
        );

        // Act
        var response = _service.SynthesizeDialectScript(request);

        // Assert
        response.TargetDialect.Should().Be(ArabicRegionalDialect.SaudiHejazi);
        response.LocalizedScript.Should().Contain("إيش الهرجة");
        response.LocalizedScript.Should().Contain("يا سيدي");
        response.LocalizedScript.Should().Contain("على كيفك");
        response.DialectIdiomsUsed.Should().Contain("إيش الهرجة");
        response.DialectIdiomsUsed.Should().Contain("يا سيدي");
    }

    [Fact]
    public void SynthesizeDialectScript_GulfKhaleeji_InjectsKhaleejiVernacular()
    {
        // Arrange
        var request = new SynthesizeDialectScriptRequest(
            StandardScript: "ما رأيك في عروضنا؟ يسرنا التعامل معكم والمنتجات جيدة جداً الآن.",
            TargetDialect: ArabicRegionalDialect.GulfKhaleeji
        );

        // Act
        var response = _service.SynthesizeDialectScript(request);

        // Assert
        response.TargetDialect.Should().Be(ArabicRegionalDialect.GulfKhaleeji);
        response.LocalizedScript.Should().Contain("شنو رايك");
        response.LocalizedScript.Should().Contain("فالك طيب");
        response.DialectIdiomsUsed.Should().Contain("شنو رايك");
        response.DialectIdiomsUsed.Should().Contain("فالك طيب");
    }

    [Fact]
    public void GenerateVariants_MultiArmedBandit_ProducesScoredVariants()
    {
        // Arrange
        var projects = _service.GetProjects(DefaultTenantId);
        var project = projects.First();

        var request = new GenerateVariantsRequest(
            TenantId: DefaultTenantId,
            ProjectId: project.ProjectId,
            VariantCount: 4
        );

        // Act
        var variants = _service.GenerateVariants(request);

        // Assert
        variants.Should().HaveCount(4);
        foreach (var variant in variants)
        {
            variant.ProjectId.Should().Be(project.ProjectId);
            variant.TenantId.Should().Be(DefaultTenantId);
            variant.VariantCode.Should().StartWith("VAR-");
            variant.HookText.Should().NotBeNullOrWhiteSpace();
            variant.CtaText.Should().NotBeNullOrWhiteSpace();
            variant.BanditExplorationScore.Should().BeInRange(0.10, 1.0);
            variant.PredictedCtrPercent.Should().BeGreaterThan(2.0);
        }

        // Verify variants are retrievable
        var retrieved = _service.GetVariants(DefaultTenantId, project.ProjectId);
        retrieved.Should().Contain(variants);
    }

    [Fact]
    public void TenantIsolation_EnforcesStrictMultiTenantSeparation()
    {
        // Arrange
        var foreignTenantId = Guid.NewGuid();
        var foreignProjectRequest = new CreateVideoProjectRequest(
            TenantId: foreignTenantId,
            ProjectTitle: "Confidential Foreign Video Ad",
            ProductSku: "CONF-001",
            TargetAudience: "Specific Tenant Market",
            Dialect: ArabicRegionalDialect.Egyptian,
            AspectRatio: VideoAspectRatio.Landscape_16_9,
            CustomScenes: null
        );

        // Act
        var foreignProject = _service.CreateProject(foreignProjectRequest);
        var defaultTenantProjects = _service.GetProjects(DefaultTenantId);
        var foreignTenantProjects = _service.GetProjects(foreignTenantId);

        // Assert: Default tenant does not see foreign project
        defaultTenantProjects.Should().NotContain(p => p.ProjectId == foreignProject.ProjectId);
        foreignTenantProjects.Should().ContainSingle(p => p.ProjectId == foreignProject.ProjectId);

        // Cross-tenant retrieval returns null
        var crossLookup = _service.GetProject(DefaultTenantId, foreignProject.ProjectId);
        crossLookup.Should().BeNull();

        // Cross-tenant variant generation throws KeyNotFoundException
        var crossGenerateAction = () => _service.GenerateVariants(new GenerateVariantsRequest(
            TenantId: DefaultTenantId,
            ProjectId: foreignProject.ProjectId,
            VariantCount: 2
        ));
        crossGenerateAction.Should().Throw<KeyNotFoundException>();
    }
}
