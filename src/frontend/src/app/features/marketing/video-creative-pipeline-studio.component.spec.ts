// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import {
  VideoCreativePipelineStudioComponent,
  synthesizeRegionalDialect,
  getDialectDisplayLabel,
  getAspectRatioLabel,
  getStageMeta
} from './video-creative-pipeline-studio.component';

describe('VideoCreativePipelineStudioComponent (Domain C5 & J1/J3 Generative AI Video Pipeline)', () => {
  let component: VideoCreativePipelineStudioComponent;

  beforeEach(() => {
    component = new VideoCreativePipelineStudioComponent();
  });

  it('should initialize component with seeded video projects and variants', () => {
    expect(component).toBeDefined();
    const projects = component.projects();
    expect(projects.length).toBe(2);

    const oudProj = projects[0];
    expect(oudProj.projectTitle).toContain('Ramadan Luxury Oud Collection');
    expect(oudProj.dialect).toBe('SaudiNajdi');
    expect(oudProj.aspectRatio).toBe('Vertical_9_16');
    expect(oudProj.totalDurationSeconds).toBe(25);
    expect(oudProj.estimatedRenderCostSar).toBe(45.0);
    expect(oudProj.scenes.length).toBe(5);

    const posProj = projects[1];
    expect(posProj.projectTitle).toContain('Smart Cloud POS');
    expect(posProj.dialect).toBe('GulfKhaleeji');
    expect(posProj.aspectRatio).toBe('Square_1_1');
    expect(posProj.totalDurationSeconds).toBe(20);
    expect(posProj.estimatedRenderCostSar).toBe(35.0);
    expect(posProj.scenes.length).toBe(4);
  });

  it('should compute initial top KPIs correctly', () => {
    expect(component.activeProjectsCount()).toBe(2);
    expect(component.renderedVariantsCount()).toBe(5);
    expect(component.avgPredictedCtr()).toBe('4.8%');
    expect(component.dialectCoverageCount()).toBe(2);
  });

  it('should update active project and variants when switching projects', () => {
    const initialProject = component.selectedProject();
    expect(initialProject.projectId).toBe('a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d');
    expect(component.activeProjectVariants().length).toBe(3);

    // Switch to Project 2 (Smart Cloud POS)
    component.onSelectProject('b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e');

    const updatedProject = component.selectedProject();
    expect(updatedProject.projectId).toBe('b2c3d4e5-f6a7-8b9c-0d1e-2f3a4b5c6d7e');
    expect(updatedProject.dialect).toBe('GulfKhaleeji');
    expect(component.targetDialect()).toBe('GulfKhaleeji');
    expect(component.activeProjectVariants().length).toBe(2);
    expect(component.activeProjectVariants()[0].variantCode).toBe('VAR-GUL-01');
  });

  it('should synthesize Saudi Najdi dialect and identify vernacular idioms', () => {
    component.targetDialect.set('SaudiNajdi');
    component.draftScript.set('ما رأيك في تجربة منتجنا المميز اليوم؟ يسرنا خدمتك، ولا تفوت فرصة الاستفادة.');

    component.synthesizeScript();

    const result = component.synthesizedResult();
    expect(result).not.toBeNull();
    expect(result!.targetDialect).toBe('SaudiNajdi');
    expect(result!.localizedScript).toContain('وش رايك');
    expect(result!.localizedScript).toContain('أبشر بعزك');
    expect(result!.localizedScript).toContain('يا هلا ومسهلا');
    expect(result!.localizedScript).toContain('لا يفوتك');
    expect(result!.dialectIdiomsUsed).toContain('وش رايك');
    expect(result!.dialectIdiomsUsed).toContain('أبشر بعزك');
    expect(result!.dialectIdiomsUsed).toContain('لا يفوتك');
  });

  it('should synthesize Saudi Hejazi and Gulf Khaleeji dialects accurately', () => {
    // Saudi Hejazi
    const hejazi = synthesizeRegionalDialect('ما رأيك يا سيدي؟ كل شيء حسب رغبتك الآن ولا داعي للقلق.', 'SaudiHejazi');
    expect(hejazi.localizedScript).toContain('إيش الهرجة');
    expect(hejazi.localizedScript).toContain('يا سيدي');
    expect(hejazi.localizedScript).toContain('على كيفك');
    expect(hejazi.dialectIdiomsUsed).toContain('إيش الهرجة');

    // Gulf Khaleeji
    const khaleeji = synthesizeRegionalDialect('ما رأيك في العرض؟ يسرنا مساعدتك كثيراً الآن.', 'GulfKhaleeji');
    expect(khaleeji.localizedScript).toContain('شنو رايك');
    expect(khaleeji.localizedScript).toContain('فالك طيب');
    expect(khaleeji.localizedScript).toContain('وايد');
    expect(khaleeji.dialectIdiomsUsed).toContain('شنو رايك');
    expect(khaleeji.dialectIdiomsUsed).toContain('فالك طيب');
  });

  it('should apply synthesized localized script directly to active hook scene', () => {
    component.targetDialect.set('SaudiNajdi');
    component.draftScript.set('يا هلا ومسهلا! وش رايك في فخامة دهن العود؟ أبشر بعزك، لا يفوتك العرض.');
    component.synthesizeScript();

    component.applySynthesizedToActiveHook();

    const activeProject = component.selectedProject();
    const hookScene = activeProject.scenes.find(s => s.stage === 'HookIntro');
    expect(hookScene).toBeDefined();
    expect(hookScene!.voiceoverScript).toContain('يا هلا ومسهلا');
    expect(hookScene!.voiceoverScript).toContain('وش رايك');
    expect(component.statusMessage()).toContain('Applied localized regional script');
  });

  it('should generate multi-armed bandit creative variants and update metrics', () => {
    const initialVariantCount = component.allVariants().length;
    const initialProjectVariantsCount = component.activeProjectVariants().length;

    component.variantCountToGenerate.set(3);
    component.generateBanditVariants();

    expect(component.allVariants().length).toBe(initialVariantCount + 3);
    expect(component.activeProjectVariants().length).toBe(initialProjectVariantsCount + 3);
    expect(component.renderedVariantsCount()).toBe(initialVariantCount + 3);

    const latestVariant = component.allVariants()[component.allVariants().length - 1];
    expect(latestVariant.variantCode).toMatch(/^VAR-NAJ-\d{2}$/);
    expect(latestVariant.banditExplorationScore).toBeGreaterThanOrEqual(0.1);
    expect(latestVariant.banditExplorationScore).toBeLessThanOrEqual(1.0);
    expect(latestVariant.predictedCtrPercent).toBeGreaterThan(3.0);
  });

  it('should return appropriate metadata for dialect labels, aspect ratios, and creative stages', () => {
    expect(getDialectDisplayLabel('SaudiNajdi')).toContain('Saudi Najdi');
    expect(getDialectDisplayLabel('GulfKhaleeji')).toContain('Gulf Khaleeji');
    expect(getDialectDisplayLabel('Egyptian')).toContain('Egyptian');

    expect(getAspectRatioLabel('Vertical_9_16')).toContain('9:16');
    expect(getAspectRatioLabel('Square_1_1')).toContain('1:1');
    expect(getAspectRatioLabel('Landscape_16_9')).toContain('16:9');

    const hookMeta = getStageMeta('HookIntro');
    expect(hookMeta.label).toContain('Hook Intro');
    expect(hookMeta.icon).toBe('⚡');

    const ctaMeta = getStageMeta('CallToAction');
    expect(ctaMeta.label).toContain('Call To Action');
    expect(ctaMeta.icon).toBe('🚀');
  });
});
