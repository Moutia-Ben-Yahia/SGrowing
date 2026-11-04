// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { EnterpriseLicenseCockpitComponent } from './enterprise-license-cockpit.component';

describe('EnterpriseLicenseCockpitComponent', () => {
  let component: EnterpriseLicenseCockpitComponent;

  beforeEach(() => {
    component = new EnterpriseLicenseCockpitComponent();
  });

  it('should initialize active air-gapped license details', () => {
    expect(component).toBeTruthy();
    expect(component.license().isAirGappedValid).toBe(true);
    expect(component.license().daysRemaining).toBeGreaterThan(0);
    expect(component.license().entitledModules.length).toBeGreaterThanOrEqual(5);
  });

  it('should apply license key and update state', () => {
    component.inputKey = 'SG-NEW-KEY-123';
    component.inputOrg = 'New Corp Ltd';
    component.applyLicense();

    expect(component.license().licenseKey).toBe('SG-NEW-KEY-123');
    expect(component.license().organizationName).toBe('New Corp Ltd');
    expect(component.activationMessage()).toContain('successfully verified');
  });

  it('should ignore empty license key submission and apply default sovereign organization name if org is blank', () => {
    component.inputKey = '   ';
    component.applyLicense();
    expect(component.activationMessage()).toBe('');

    component.inputKey = 'SG-SOVEREIGN-KEY';
    component.inputOrg = '';
    component.applyLicense();
    expect(component.license().organizationName).toBe('Acme Global Sovereign');
    expect(component.license().daysRemaining).toBe(365);
    expect(component.activationMessage()).toContain('SHA-256');
  });

  it('should verify entitled modules contain core AI and voice capabilities', () => {
    const modules = component.license().entitledModules;
    expect(modules).toContain('AI.Swarm Coordination');
    expect(modules).toContain('Real-Time WebRTC Voice SDR');
    expect(modules).toContain('Executive Revenue Intelligence');
    expect(component.license().fingerprint).toBe('NODE-01-MAC-48-2C');
  });
});
