// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SovereignDstCockpitComponent } from './sovereign-dst-cockpit.component';

describe('SovereignDstCockpitComponent', () => {
  it('should initialize with covered jurisdictions, accrued DST, and GloBE assessments', () => {
    const comp = new SovereignDstCockpitComponent();
    expect(comp.coveredJurisdictionsCount()).toBe(3);
    expect(comp.totalDstAccrued()).toBe(1245000);
    expect(comp.totalTopUpTax()).toBe(425000);
    expect(comp.avgEtr()).toBe(14.2);
    expect(comp.records().length).toBe(2);
    expect(comp.assessments().length).toBe(1);
  });

  it('should compute new DST accrual and update total accrued liability', () => {
    const comp = new SovereignDstCockpitComponent();
    const initialAccrued = comp.totalDstAccrued();
    comp.computeNewDstAccrual();

    expect(comp.records().length).toBe(3);
    expect(comp.totalDstAccrued()).toBe(initialAccrued + 360000);
    expect(comp.records()[2].jurisdictionIsoCode).toBe('IT');
    expect(comp.records()[2].status).toBe('ACCRUED');
  });
});
