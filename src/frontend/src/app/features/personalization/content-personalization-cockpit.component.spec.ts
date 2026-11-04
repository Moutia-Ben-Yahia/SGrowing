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

  it('should allow dynamically adding new firmographic segments and updating active rules count', () => {
    const comp = new ContentPersonalizationCockpitComponent();
    comp.segments.update(list => [
      ...list,
      {
        segmentId: 'SEG-GOV-04',
        name: 'Public Sector & Defense',
        targetIndustry: 'Government',
        employeeRange: '10,000+',
        revenueTier: '$500M+',
        activeAccountsCount: 95
      }
    ]);
    comp.activeRulesCount.set(4);

    expect(comp.segments().length).toBe(4);
    expect(comp.activeRulesCount()).toBe(4);
    expect(comp.segments()[3].segmentId).toBe('SEG-GOV-04');
  });

  it('should support clearing segments and resetting conversion lift percentage', () => {
    const comp = new ContentPersonalizationCockpitComponent();
    comp.segments.set([]);
    comp.activeRulesCount.set(0);
    comp.averageConversionLiftPct.set(0);

    expect(comp.segments().length).toBe(0);
    expect(comp.activeRulesCount()).toBe(0);
    expect(comp.averageConversionLiftPct()).toBe(0);
  });
});
