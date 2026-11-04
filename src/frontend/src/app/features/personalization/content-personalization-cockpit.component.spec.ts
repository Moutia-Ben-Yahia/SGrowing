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

  it('should verify firmographic segment account distribution and default hero CTA content', () => {
    const comp = new ContentPersonalizationCockpitComponent();
    const totalAccounts = comp.segments().reduce((sum, s) => sum + s.activeAccountsCount, 0);
    expect(totalAccounts).toBe(1350); // 420 + 280 + 650

    const fintech = comp.segments().find(s => s.segmentId === 'SEG-ENT-01');
    expect(fintech?.revenueTier).toBe('$100M+');

    expect(comp.selectedCta()).toBe('Schedule Executive Architecture Review');
  });
});
