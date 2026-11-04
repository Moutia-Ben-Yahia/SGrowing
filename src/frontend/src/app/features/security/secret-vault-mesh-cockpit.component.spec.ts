// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SecretVaultMeshCockpitComponent } from './secret-vault-mesh-cockpit.component';

describe('SecretVaultMeshCockpitComponent', () => {
  it('should initialize with secrets and correct active key metrics', () => {
    const comp = new SecretVaultMeshCockpitComponent();
    expect(comp.secrets().length).toBe(4);
    expect(comp.activeSecretsCount()).toBe(4);
    expect(comp.zeroizedCount()).toBe(0);
  });

  it('should select a secret', () => {
    const comp = new SecretVaultMeshCockpitComponent();
    const second = comp.secrets()[1];
    comp.selectSecret(second);
    expect(comp.selectedSecret()?.secretId).toBe('sec_jwt_ed25519_02');
  });

  it('should rotate secret, incrementing version and resetting age to 0', () => {
    const comp = new SecretVaultMeshCockpitComponent();
    const originalVersion = comp.secrets()[0].version;
    comp.rotateSecret('sec_db_master_01');

    const rotated = comp.secrets().find(s => s.secretId === 'sec_db_master_01');
    expect(rotated?.version).toBe(originalVersion + 1);
    expect(rotated?.ageInDays).toBe(0);
    expect(rotated?.phase).toBe('ActivePrimary');
  });

  it('should zeroize secret and mark it as ZeroizedPurged', () => {
    const comp = new SecretVaultMeshCockpitComponent();
    comp.zeroizeSecret('sec_stripe_live_04');

    expect(comp.zeroizedCount()).toBe(1);
    expect(comp.activeSecretsCount()).toBe(3);

    const zeroized = comp.secrets().find(s => s.secretId === 'sec_stripe_live_04');
    expect(zeroized?.phase).toBe('ZeroizedPurged');
  });

  it('should return 0 averageAge when there are no active primary secrets', () => {
    const comp = new SecretVaultMeshCockpitComponent();
    comp.secrets.set([]);
    expect(comp.averageAge()).toBe(0);
    expect(comp.activeSecretsCount()).toBe(0);
  });
});
