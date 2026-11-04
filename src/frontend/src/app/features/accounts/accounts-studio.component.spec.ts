// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AccountsStudioComponent } from './accounts-studio.component';

describe('AccountsStudioComponent', () => {
  let component: AccountsStudioComponent;

  beforeEach(() => {
    vi.spyOn(window, 'alert').mockImplementation(() => {});
    vi.spyOn(window, 'prompt').mockImplementation(() => 'boutique.medina.tn');
    component = new AccountsStudioComponent();
  });

  it('should initialize with tracked companies and storefront domains', () => {
    expect(component).toBeTruthy();
    expect(component.companies().length).toBe(3);
    expect(component.storefrontDomains().length).toBe(2);
  });

  it('should create a new company account and reset modal form state', () => {
    component.newCompanyName = 'Atlas Logistics Group';
    component.newCompanyDomain = 'atlas-logistics.tn';
    component.newCompanyIndustry = 'Freight & Logistics';

    component.createCompany();

    const companies = component.companies();
    expect(companies.length).toBe(4);
    const added = companies.find(c => c.domain === 'atlas-logistics.tn');
    expect(added).toBeTruthy();
    expect(added?.name).toBe('Atlas Logistics Group');
    expect(component.newCompanyName).toBe('');
    expect(component.showCreateModal).toBe(false);
  });

  it('should not create company when required fields are empty', () => {
    component.newCompanyName = '';
    component.newCompanyDomain = '';
    component.createCompany();

    expect(component.companies().length).toBe(3);
  });

  it('should simulate storefront domain registration and DNS verification', () => {
    component.simulateRegisterDomain();
    const domains = component.storefrontDomains();
    expect(domains.length).toBe(3);
    const newDomain = domains.find(d => d.hostname === 'boutique.medina.tn');
    expect(newDomain).toBeTruthy();
    expect(newDomain?.status).toBe('PendingDns');

    component.verifyDomainUi(newDomain);
    expect(newDomain?.status).toBe('Active');
    expect(newDomain?.ssl).toContain("Let's Encrypt");
  });

  it('should ignore simulateRegisterDomain when user prompt input is cancelled or empty', () => {
    vi.spyOn(window, 'prompt').mockReturnValueOnce(null);
    const initialLen = component.storefrontDomains().length;

    component.simulateRegisterDomain();
    expect(component.storefrontDomains().length).toBe(initialLen);
  });
});
