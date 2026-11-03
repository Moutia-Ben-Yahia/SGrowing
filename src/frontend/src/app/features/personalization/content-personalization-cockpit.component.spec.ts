// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ContentPersonalizationCockpitComponent } from './content-personalization-cockpit.component';

describe('ContentPersonalizationCockpitComponent', () => {
  it('should initialize with default segments and rules', () => {
    const comp = new ContentPersonalizationCockpitComponent();
    expect(comp.segments().length).toBe(3);
    expect(comp.activeRulesCount()).toBe(3);
    expect(comp.averageConversionLiftPct()).toBeGreaterThan(10);
    expect(comp.selectedHeadline()).toContain('Global Financial Institutions');
  });

  it('should update headline and CTA on evaluatePage simulation', () => {
    const comp = new ContentPersonalizationCockpitComponent();
    comp.evaluatePage();
    expect(comp.selectedHeadline()).toContain('Patient Acquisition');
    expect(comp.selectedCta()).toContain('HIPAA Compliant');
  });
});
