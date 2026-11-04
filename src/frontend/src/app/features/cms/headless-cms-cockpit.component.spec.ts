// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { HeadlessCmsCockpitComponent } from './headless-cms-cockpit.component';

describe('HeadlessCmsCockpitComponent', () => {
  let component: HeadlessCmsCockpitComponent;

  beforeEach(() => {
    component = new HeadlessCmsCockpitComponent();
  });

  it('should initialize with published articles and average voice score', () => {
    expect(component).toBeTruthy();
    expect(component.articles().length).toBeGreaterThanOrEqual(2);
    expect(component.averageVoiceScore()).toBeGreaterThanOrEqual(90);
  });

  it('should evaluate brand voice copy', () => {
    component.sampleTextToAudit = 'Comprehensive enterprise revenue intelligence and predictive attribution.';
    component.auditText();

    expect(component.auditResult()?.score).toBeGreaterThanOrEqual(90);
    expect(component.auditResult()?.tone).toBe('Enterprise Precision');
  });

  it('should create new modular content article and update article count', () => {
    const initial = component.articles().length;
    component.newTitle = 'Zero-Trust Security Whitepaper';
    component.newSlug = 'zero-trust-whitepaper';
    component.newDescription = 'Cryptographic audit trails and Merkle verification.';

    component.createArticle();

    expect(component.articles().length).toBe(initial + 1);
    expect(component.articles()[0].slug).toBe('zero-trust-whitepaper');
  });

  it('should penalize brand voice score when text includes unsupported guarantee language', () => {
    component.sampleTextToAudit = 'We guarantee 100% pipeline conversions without any effort.';
    component.auditText();

    expect(component.auditResult()?.score).toBe(60.0);
    expect(component.auditResult()?.readingLevel).toBe('Grade 11 (Executive Brief)');

    component.triggerEdgeRevalidation('art_1');
    const art = component.articles().find(a => a.id === 'art_1');
    expect(art?.status).toBe('Edge Synced');
  });

  it('should return 0 averageVoiceScore when articles list is empty', () => {
    component.articles.set([]);
    expect(component.averageVoiceScore()).toBe(0);
  });
});
