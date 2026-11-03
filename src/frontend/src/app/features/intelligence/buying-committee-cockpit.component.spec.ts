// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { BuyingCommitteeCockpitComponent } from './buying-committee-cockpit.component';

describe('BuyingCommitteeCockpitComponent', () => {
  it('initializes with mapped stakeholders and consensus score', () => {
    const comp = new BuyingCommitteeCockpitComponent();
    expect(comp.members().length).toBe(3);
    expect(comp.approvedCount()).toBe(2);
    expect(comp.consensusScore()).toBeGreaterThan(50);
  });

  it('authorizes pending member approval and updates count', () => {
    const comp = new BuyingCommitteeCockpitComponent();
    const pending = comp.members().find(m => !m.hasApproved);
    expect(pending).toBeDefined();

    comp.toggleApprove(pending!);
    expect(comp.approvedCount()).toBe(3);
  });

  it('adds CISO stakeholder and resolves missing persona gap alert', () => {
    const comp = new BuyingCommitteeCockpitComponent();
    expect(comp.gaps().some(g => g.missingRole === 'SecurityCiso')).toBe(true);

    comp.addSecurityCisoMember();
    expect(comp.members().length).toBe(4);
    expect(comp.gaps().some(g => g.missingRole === 'SecurityCiso')).toBe(false);
  });
});
