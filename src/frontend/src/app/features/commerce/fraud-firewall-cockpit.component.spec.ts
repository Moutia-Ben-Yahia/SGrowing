// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { FraudFirewallCockpitComponent } from './fraud-firewall-cockpit.component';

describe('FraudFirewallCockpitComponent', () => {
  let component: FraudFirewallCockpitComponent;

  beforeEach(() => {
    component = new FraudFirewallCockpitComponent();
  });

  it('should initialize with rules and default evaluations', () => {
    expect(component).toBeTruthy();
    expect(component.rules().length).toBe(4);
    expect(component.evaluations().length).toBe(3);
    expect(component.avgRiskScore()).toBeGreaterThan(40);
  });

  it('should toggle heuristic rule status', () => {
    const initialStatus = component.rules()[0].isActive;
    component.toggleRule('rule_geo_mismatch');

    expect(component.rules()[0].isActive).toBe(!initialStatus);
  });

  it('should simulate high-risk transaction and append Block decision', () => {
    const initialCount = component.evaluations().length;
    component.simulateEvaluation();

    expect(component.evaluations().length).toBe(initialCount + 1);
    expect(component.evaluations()[0].decision).toBe('Block');
    expect(component.evaluations()[0].riskScore).toBe(85);
  });

  it('should compute blockRate and challengeCount correctly', () => {
    expect(component.blockRate()).toBe(0);
    expect(component.challengeCount()).toBe(2);

    component.simulateEvaluation();
    expect(component.blockRate()).toBe(25);
    expect(component.challengeCount()).toBe(2);
  });
});
