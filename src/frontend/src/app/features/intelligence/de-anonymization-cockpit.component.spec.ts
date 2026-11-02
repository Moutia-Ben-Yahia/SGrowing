// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { DeAnonymizationCockpitComponent } from './de-anonymization-cockpit.component';

describe('DeAnonymizationCockpitComponent (Horizon 56)', () => {
  let component: DeAnonymizationCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new DeAnonymizationCockpitComponent();
  });

  it('should initialize with corporate B2B visitor accounts', () => {
    expect(component).toBeTruthy();
    expect(component.accounts().length).toBeGreaterThanOrEqual(2);
    expect(component.readyToBuyCount()).toBeGreaterThanOrEqual(1);
    expect(component.avgScore()).toBeGreaterThan(50);
  });

  it('should simulate de-anonymization of new high-intent account', () => {
    const initialCount = component.accounts().length;
    component.simulateDeAnonymization();
    expect(component.accounts().length).toBe(initialCount + 1);
    expect(component.selectedAccount()?.domain).toBe('databricks.com');
    expect(component.selectedAccount()?.activeScore).toBe(89);
  });

  it('should dispatch alert and set preview message', () => {
    const acc = component.accounts()[0];
    component.dispatchAlert(acc);
    expect(component.lastAlert()).toContain('Alert Dispatched');
    expect(component.lastAlert()).toContain(acc.companyName);
  });
});
