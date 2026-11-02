import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { CustomDomainsCockpitComponent } from './custom-domains-cockpit.component';

describe('CustomDomainsCockpitComponent', () => {
  let component: CustomDomainsCockpitComponent;
  let fixture: ComponentFixture<CustomDomainsCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomDomainsCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomDomainsCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
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
});
