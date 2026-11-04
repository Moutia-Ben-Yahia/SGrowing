// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { IntentRollupCockpitComponent } from './intent-rollup-cockpit.component';

describe('IntentRollupCockpitComponent', () => {
  it('should initialize with correct default metrics and accounts', () => {
    const comp = new IntentRollupCockpitComponent();
    expect(comp.totalAccounts()).toBe(3);
    expect(comp.highIntentAccounts()).toBe(2);
    expect(comp.deDupedSignals()).toBe(1420);
    expect(comp.collisionPreventionRate()).toBe(99.4);
    expect(comp.accounts().length).toBe(3);
  });

  it('should surge intent score on simulation', () => {
    const comp = new IntentRollupCockpitComponent();
    comp.simulateIntentSurge();
    expect(comp.highIntentAccounts()).toBe(3);
    const acc = comp.accounts().find(a => a.accountId === 'ACC-CORP-03');
    expect(acc?.aggregateIntentScore).toBe(85);
  });

  it('should lock account outreach and update locked status', () => {
    const comp = new IntentRollupCockpitComponent();
    comp.lockCorporateOutreach();
    const acc = comp.accounts().find(a => a.accountId === 'ACC-CORP-02');
    expect(acc?.outreachLocked).toBe(true);
  });

  it('should allow updating deDupedSignals and collisionPreventionRate signals', () => {
    const comp = new IntentRollupCockpitComponent();
    comp.deDupedSignals.set(1850);
    comp.collisionPreventionRate.set(99.9);

    expect(comp.deDupedSignals()).toBe(1850);
    expect(comp.collisionPreventionRate()).toBe(99.9);
  });
});
