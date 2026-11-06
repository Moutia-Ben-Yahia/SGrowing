// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ContentBriefStudioComponent } from './content-brief-studio.component';

describe('ContentBriefStudioComponent', () => {
  let component: ContentBriefStudioComponent;

  beforeEach(() => {
    component = new ContentBriefStudioComponent();
  });

  it('should initialize with default signals and tabs', () => {
    expect(component).toBeTruthy();
    expect(component.primaryKeyword()).toBe('enterprise dynamic pricing');
    expect(component.secondaryKeywordsInput()).toBe('cod checkout, ecom margin, pricing rules');
    expect(component.targetAudience()).toBe('E-Commerce Directors');
    expect(component.searchIntent()).toBe('Commercial');
    expect(component.draftTitle()).toBe('The Definitive Guide to Dynamic Pricing');
    expect(component.draftBody()).toBe('');
    expect(component.currentBrief()).toBeNull();
    expect(component.draftGradeReport()).toBeNull();
    expect(component.geoCitabilityAudit()).toBeNull();
    expect(component.llmsTxtContent()).toBe('');
    expect(component.activeTab()).toBe('brief');
    expect(component.liveWordCount()).toBe(0);
    expect(component.secondaryKeywordsList()).toEqual(['cod checkout', 'ecom margin', 'pricing rules']);
  });

  it('should switch active tabs cleanly', () => {
    expect(component.activeTab()).toBe('brief');

    component.setActiveTab('grader');
    expect(component.activeTab()).toBe('grader');

    component.setActiveTab('geo');
    expect(component.activeTab()).toBe('geo');

    component.setActiveTab('llms');
    expect(component.activeTab()).toBe('llms');
    // When switching to llms tab, it auto-generates llms content if empty
    expect(component.llmsTxtContent()).toContain('# Enterprise Dynamic Pricing Knowledge Hub');

    component.setActiveTab('brief');
    expect(component.activeTab()).toBe('brief');
  });

  it('should generate content brief with outline sections and semantic keywords', () => {
    component.generateBrief();

    const brief = component.currentBrief();
    expect(brief).not.toBeNull();
    expect(brief?.primaryKeyword).toBe('enterprise dynamic pricing');
    expect(brief?.targetAudience).toBe('E-Commerce Directors');
    expect(brief?.searchIntent).toBe('Commercial');
    expect(brief?.targetWordCount.min).toBe(2200);
    expect(brief?.targetWordCount.max).toBe(2800);
    expect(brief?.targetReadingGrade).toContain('Flesch-Kincaid');
    expect(brief?.targetContentScore).toBeGreaterThanOrEqual(80);

    // Verify outline sections
    expect(brief?.outlineSections.length).toBeGreaterThanOrEqual(4);
    expect(brief?.outlineSections[0].heading).toContain('Enterprise Dynamic Pricing');
    expect(brief?.outlineSections[0].suggestedQuestions.length).toBeGreaterThan(0);
    expect(brief?.outlineSections[0].recommendedWords).toBeGreaterThan(100);

    // Verify semantic keywords & target densities
    expect(brief?.semanticKeywords.length).toBeGreaterThanOrEqual(4);
    const pkKeyword = brief?.semanticKeywords.find(k => k.term === 'enterprise dynamic pricing');
    expect(pkKeyword).toBeDefined();
    expect(pkKeyword?.targetDensityPercent).toBeGreaterThan(0.5);

    // Verify competitor benchmarks
    expect(brief?.competitorBenchmarks.length).toBeGreaterThanOrEqual(3);
  });

  it('should adapt generated brief when parameters are customized', () => {
    component.primaryKeyword.set('headless commerce seo');
    component.targetAudience.set('Chief Technology Officers');
    component.searchIntent.set('Informational');
    component.secondaryKeywordsInput.set('ssr routing, edge rendering');

    component.generateBrief();

    const brief = component.currentBrief();
    expect(brief?.primaryKeyword).toBe('headless commerce seo');
    expect(brief?.targetAudience).toBe('Chief Technology Officers');
    expect(brief?.searchIntent).toBe('Informational');
    expect(brief?.outlineSections.some(s => s.heading.includes('Headless Commerce Seo'))).toBe(true);
    expect(brief?.semanticKeywords.some(k => k.term === 'ssr routing')).toBe(true);
  });

  it('should grade draft and compute word count, Flesch score, and letter grade', () => {
    // Load pre-formatted sample draft
    component.loadSampleDraft();

    expect(component.draftBody().length).toBeGreaterThan(100);
    expect(component.liveWordCount()).toBeGreaterThan(50);

    component.gradeDraft();

    const report = component.draftGradeReport();
    expect(report).not.toBeNull();
    expect(report?.wordCount).toBe(component.liveWordCount());
    expect(report?.charCount).toBe(component.liveCharCount());
    expect(report?.readingEaseScore).toBeGreaterThan(0);
    expect(['A+', 'A', 'B', 'C', 'D', 'F']).toContain(report?.letterGrade);
    expect(report?.primaryKeywordDensity).toBeGreaterThanOrEqual(0.1);
    expect(report?.secondaryKeywordMatches.length).toBeGreaterThanOrEqual(3);
    expect(report?.questionsAnswered.length).toBeGreaterThanOrEqual(1);
    expect(report?.recommendations.length).toBeGreaterThanOrEqual(2);
  });

  it('should handle empty draft gracefully during grading', () => {
    component.draftBody.set('');
    component.gradeDraft();

    const report = component.draftGradeReport();
    expect(report).not.toBeNull();
    expect(report?.wordCount).toBe(0);
    expect(report?.letterGrade).toBe('F');
    expect(report?.questionsMissing.length).toBeGreaterThan(0);
    expect(report?.recommendations[0].type).toBe('critical');
  });

  it('should audit GEO citability and calculate conciseness and entity density scores', () => {
    // Load sample draft for rich citability audit
    component.loadSampleDraft();
    component.auditGeoCitability();

    const geo = component.geoCitabilityAudit();
    expect(geo).not.toBeNull();
    expect(geo?.concisenessScore).toBeGreaterThanOrEqual(50);
    expect(geo?.concisenessScore).toBeLessThanOrEqual(100);
    expect(geo?.entityDensityPercent).toBeGreaterThanOrEqual(5);
    expect(geo?.qaStructureRatio).toBeGreaterThan(0);
    expect(geo?.ambiguousPronounCount).toBeGreaterThanOrEqual(0);
    expect(['High', 'Medium', 'Low']).toContain(geo?.directAnswerClarity);

    // Formatted citation passage
    expect(geo?.formattedCitationSnippet).toContain('enterprise dynamic pricing');
    expect(geo?.extractedEntities.length).toBeGreaterThanOrEqual(3);

    // Readiness matrix
    expect(geo?.perEngineReadiness.length).toBe(4);
    expect(geo?.perEngineReadiness.some(e => e.engine === 'Perplexity Search')).toBe(true);
    expect(geo?.perEngineReadiness.some(e => e.engine === 'Google AI Overviews')).toBe(true);
  });

  it('should generate and copy llms.txt content', () => {
    component.generateLlmsTxt();

    const content = component.llmsTxtContent();
    expect(content).toContain('# Enterprise Dynamic Pricing Knowledge Hub');
    expect(content).toContain('## Core Specifications & Guides');
    expect(content).toContain('## Key Named Entities');
    expect(content).toContain('/llms-full.txt');

    // Clipboard copy simulation
    component.copyLlmsTxt();
    expect(component.copiedLlms()).toBe(true);
  });

  it('should reactively compute secondary keywords list on input change', () => {
    component.secondaryKeywordsInput.set('alpha, beta, gamma');
    expect(component.secondaryKeywordsList()).toEqual(['alpha', 'beta', 'gamma']);

    component.secondaryKeywordsInput.set('   single-tag   ');
    expect(component.secondaryKeywordsList()).toEqual(['single-tag']);

    component.secondaryKeywordsInput.set('');
    expect(component.secondaryKeywordsList()).toEqual([]);
  });
});
