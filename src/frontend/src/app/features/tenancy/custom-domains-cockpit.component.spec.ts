// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { CustomDomainsCockpitComponent } from './custom-domains-cockpit.component';

describe('CustomDomainsCockpitComponent', () => {
  let component: CustomDomainsCockpitComponent;

  beforeEach(() => {
    component = new CustomDomainsCockpitComponent();
  });

  it('should create component and render initial domains', () => {
    expect(component).toBeTruthy();
    expect(component.domains().length).toBeGreaterThanOrEqual(2);
    expect(component.selectedDomain()).toBeTruthy();
  });

  it('should register a new custom domain with verification tokens', () => {
    component.newDomainInput = 'orders.starlight.com';
    component.primaryColorInput = '#4f46e5';
    component.accentColorInput = '#10b981';
    component.submitRegisterDomain();

    const registered = component.domains().find(d => d.domainName === 'orders.starlight.com');
    expect(registered).toBeTruthy();
    expect(registered?.isDnsVerified).toBe(false);
    expect(registered?.sslStatus).toBe('PendingVerification');
    expect(component.selectedDomain()?.domainName).toBe('orders.starlight.com');
  });

  it('should verify DNS and renew SSL certificate', () => {
    const target = component.domains()[1]; // HyperGrowth
    expect(target.isDnsVerified).toBe(false);

    component.verifyDns(target);
    expect(component.domains().find(d => d.id === target.id)?.isDnsVerified).toBe(true);

    component.renewSsl(target);
    expect(component.domains().find(d => d.id === target.id)?.sslStatus).toBe('Active');
  });

  it('should toggle register modal and ignore empty domain submission', () => {
    expect(component.showRegisterModal()).toBe(false);
    component.toggleRegisterModal();
    expect(component.showRegisterModal()).toBe(true);

    const initialLen = component.domains().length;
    component.newDomainInput = '   ';
    component.submitRegisterDomain();
    expect(component.domains().length).toBe(initialLen);
  });

  it('should compute activeDomainsCount and allow selecting a domain', () => {
    expect(component.activeDomainsCount()).toBe(1);
    const hyperGrowth = component.domains()[1];
    component.selectDomain(hyperGrowth);
    expect(component.selectedDomain()?.id).toBe(hyperGrowth.id);
  });
});
