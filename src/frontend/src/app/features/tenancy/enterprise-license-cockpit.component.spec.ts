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
});
