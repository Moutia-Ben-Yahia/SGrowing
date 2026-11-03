// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { VoiceBiometricCockpitComponent } from './voice-biometric-cockpit.component';

describe('VoiceBiometricCockpitComponent', () => {
  it('should initialize with enrolled profiles and defense accuracy', () => {
    const comp = new VoiceBiometricCockpitComponent();
    expect(comp.totalProfiles()).toBe(2);
    expect(comp.scannedCalls()).toBe(2);
    expect(comp.blockedDeepfakes()).toBe(1);
    expect(comp.defenseAccuracy()).toBe(99.4);
    expect(comp.profiles().length).toBe(2);
  });

  it('should simulate deepfake attack and increment blocked count', () => {
    const comp = new VoiceBiometricCockpitComponent();
    const initialBlocked = comp.blockedDeepfakes();
    comp.simulateDeepfakeAttack();

    expect(comp.blockedDeepfakes()).toBe(initialBlocked + 1);
    expect(comp.audits()[0].defenseActionTaken).toBe('TERMINATED_QUARANTINED');
    expect(comp.audits()[0].syntheticDeepfakeProbability).toBeGreaterThan(0.9);
  });
});
