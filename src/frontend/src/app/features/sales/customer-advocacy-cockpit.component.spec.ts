// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CustomerAdvocacyCockpitComponent } from './customer-advocacy-cockpit.component';

describe('CustomerAdvocacyCockpitComponent', () => {
  it('should initialize with active advocates, completed calls, and win rate lift', () => {
    const comp = new CustomerAdvocacyCockpitComponent();
    expect(comp.activeAdvocatesCount()).toBe(47);
    expect(comp.callsCompletedQuarter()).toBe(29);
    expect(comp.winRateLiftPct()).toBe(34.5);
    expect(comp.rewardPointsTotal()).toBe(17750);
    expect(comp.matches().length).toBe(1);
    expect(comp.advocates().length).toBe(2);
  });

  it('should match new reference champion and update pipeline', () => {
    const comp = new CustomerAdvocacyCockpitComponent();
    const initialMatches = comp.matches().length;
    comp.matchNewReference();

    expect(comp.matches().length).toBe(initialMatches + 1);
    expect(comp.matches()[initialMatches].prospectCompanyName).toBe('Starlight Media Network');
    expect(comp.matches()[initialMatches].referenceCallStatus).toBe('SCHEDULED');
  });
});
