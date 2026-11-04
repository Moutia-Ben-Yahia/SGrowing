// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { IdentityGraphCockpitComponent } from './identity-graph-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('IdentityGraphCockpitComponent (Domain B3 CDP Identity Resolution)', () => {
  let component: IdentityGraphCockpitComponent;
  let translation: TranslationService;

  beforeEach(() => {
    localStorage.clear();
    translation = new TranslationService();
    translation.setLocale('en-US');
    component = new IdentityGraphCockpitComponent(translation);
  });

  it('should initialize with baseline identity nodes and metrics', () => {
    expect(component.identityNodes().length).toBe(4);
    expect(component.totalProfiles()).toBe(1420);
    expect(component.totalSessions()).toBe(3890);
    expect(component.sessions().length).toBe(2);
    expect(component.auditLogs().length).toBe(2);
  });

  it('should stitch anonymous identity to known profile and update nodes and audit log', () => {
    component.newAnonId = 'cookie_test_session_55';
    component.newIdType = 'Email';
    component.newIdValue = 'cmo@growthscale.com';

    component.stitchIdentity();

    // Verify identity nodes increased
    expect(component.identityNodes().length).toBe(6); // +2 (known email + anon cookie)
    expect(component.identityNodes()[0].value).toBe('cmo@growthscale.com');
    expect(component.identityNodes()[0].confidence).toBe(1.0);

    // Verify session added
    expect(component.sessions().length).toBe(3);
    expect(component.sessions()[0].anonymousId).toBe('cookie_test_session_55');

    // Verify audit log added
    expect(component.auditLogs().length).toBe(3);
    expect(component.auditLogs()[0].triggerIdentifier).toContain('Email: cmo@growthscale.com');
  });

  it('should reset demo graph cleanly', () => {
    component.newAnonId = 'cookie_temp';
    component.newIdType = 'PhoneNumber';
    component.newIdValue = '+15550009999';
    component.stitchIdentity();

    expect(component.totalProfiles()).toBe(1421);

    component.resetDemoGraph();
    expect(component.totalProfiles()).toBe(1420);
    expect(component.activeProfileId()).toBe('prof_c0a80199_canonical');
  });

  it('should ignore stitchIdentity when inputs are missing', () => {
    const initialProfiles = component.totalProfiles();
    component.newAnonId = '';
    component.newIdValue = '';
    component.stitchIdentity();

    expect(component.totalProfiles()).toBe(initialProfiles);
    expect(component.identityNodes().length).toBe(4);
  });
});
