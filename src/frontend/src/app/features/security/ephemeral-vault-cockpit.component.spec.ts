// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { EphemeralVaultCockpitComponent } from './ephemeral-vault-cockpit.component';

describe('EphemeralVaultCockpitComponent', () => {
  it('initializes with active ephemeral tokens and zero revoked', () => {
    const comp = new EphemeralVaultCockpitComponent();
    expect(comp.tokens().length).toBe(2);
    expect(comp.activeCount()).toBe(2);
    expect(comp.revokedCount()).toBe(0);
  });

  it('revokes single token and updates status to RevokedEmergency', () => {
    const comp = new EphemeralVaultCockpitComponent();
    comp.revokeSingleToken(comp.tokens()[0]);
    expect(comp.activeCount()).toBe(1);
    expect(comp.revokedCount()).toBe(1);
    expect(comp.tokens()[0].status).toBe('RevokedEmergency');
  });

  it('triggers emergency kill all and invalidates all credentials', () => {
    const comp = new EphemeralVaultCockpitComponent();
    comp.triggerEmergencyKillAll();
    expect(comp.activeCount()).toBe(0);
    expect(comp.revokedCount()).toBe(2);
    expect(comp.tokens().every(t => t.status === 'RevokedEmergency')).toBe(true);
  });
});
