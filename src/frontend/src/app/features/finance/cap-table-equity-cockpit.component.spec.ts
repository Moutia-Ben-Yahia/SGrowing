// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CapTableEquityCockpitComponent } from './cap-table-equity-cockpit.component';

describe('CapTableEquityCockpitComponent', () => {
  it('should initialize with stakeholders and option grants', () => {
    const comp = new CapTableEquityCockpitComponent();
    expect(comp.stakeholders().length).toBe(3);
    expect(comp.optionGrants().length).toBe(1);
    expect(comp.stakeholders()[0].ownershipPercentagePct).toBe(60);
  });

  it('should grant new stock option and prepend to list', () => {
    const comp = new CapTableEquityCockpitComponent();
    comp.grantNewOption();
    expect(comp.optionGrants().length).toBe(2);
    expect(comp.optionGrants()[0].employeeName).toContain('Marcus Brody');
    expect(comp.optionGrants()[0].grantedSharesCount).toBe(80000);
  });
});
