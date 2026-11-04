// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { FeatureFlagsCockpitComponent } from './feature-flags-cockpit.component';

describe('FeatureFlagsCockpitComponent (Horizon 60)', () => {
  let component: FeatureFlagsCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new FeatureFlagsCockpitComponent();
  });

  it('should initialize with active feature flags and canary rings', () => {
    expect(component).toBeTruthy();
    expect(component.flags().length).toBeGreaterThanOrEqual(2);
    expect(component.activeFlagsCount()).toBe(2);
    expect(component.canaryActiveCount()).toBe(2);
  });

  it('should advance canary ring to PublicGA', () => {
    component.advanceCanary();
    expect(component.selectedFlag()?.currentRing).toBe('PublicGA');
    expect(component.selectedFlag()?.rolloutPercentage).toBe(100);
  });

  it('should trigger emergency kill switch and trip circuit breaker', () => {
    const flag = component.flags()[0];
    component.emergencyKill(flag);
    expect(component.selectedFlag()?.isEnabled).toBe(false);
    expect(component.selectedFlag()?.circuitBreakerTripped).toBe(true);
    expect(component.selectedFlag()?.rolloutPercentage).toBe(0);
  });

  it('should select a feature flag and verify whitelistedTenants', () => {
    const second = component.flags()[1];
    component.selectFlag(second);
    expect(component.selectedFlag()?.flagKey).toBe('zk_differential_privacy_cleanroom');
    expect(component.selectedFlag()?.whitelistedTenants).toContain('org_enterprise_vip');
  });

  it('should handle null selectedFlag on advanceCanary and compute counts on empty flags list', () => {
    component.selectedFlag.set(null);
    expect(() => component.advanceCanary()).not.toThrow();

    component.flags.set([]);
    expect(component.activeFlagsCount()).toBe(0);
    expect(component.canaryActiveCount()).toBe(0);
  });
});
