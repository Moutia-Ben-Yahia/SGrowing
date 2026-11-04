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

  it('should return 0 avgScore and 0 readyToBuyCount when accounts list is empty', () => {
    expect(component.avgScore()).toBe(85);
    expect(component.readyToBuyCount()).toBe(1);
    component.accounts.set([]);
    expect(component.avgScore()).toBe(0);
    expect(component.readyToBuyCount()).toBe(0);
  });

  it('should select an account and update selectedAccount signal', () => {
    const second = component.accounts()[1];
    component.selectAccount(second);
    expect(component.selectedAccount()?.accountId).toBe(second.accountId);
    expect(component.selectedAccount()?.companyName).toBe('Shopify Inc.');
  });
});
