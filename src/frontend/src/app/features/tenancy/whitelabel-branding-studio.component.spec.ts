// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { WhitelabelBrandingStudioComponent } from './whitelabel-branding-studio.component';

describe('WhitelabelBrandingStudioComponent', () => {
  let component: WhitelabelBrandingStudioComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue(of([])),
      post: vi.fn().mockReturnValue(of({})),
      put: vi.fn().mockReturnValue(of({})),
      delete: vi.fn().mockReturnValue(of({}))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new WhitelabelBrandingStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with pre-seeded whitelabel configs and default active tenant', () => {
    expect(component).toBeTruthy();
    expect(component.tenantsList().length).toBeGreaterThanOrEqual(2);
    expect(component.currentConfig().profile.brandName).toBe('Apex Growth Partners');
    expect(component.currentConfig().theme.primaryColor).toBe('#4338CA');
  });

  it('should switch active tenant and load respective whitelabel settings', () => {
    const nordic = component.tenantsList().find(t => t.slug === 'nordic')!;
    component.onSelectTenant(nordic.tenantId);

    expect(component.selectedTenantId()).toBe(nordic.tenantId);
    expect(component.currentConfig().profile.brandName).toBe('Nordic Scale Agency');
    expect(component.currentConfig().email.customMailDomain).toBe('mail.nordicscale.se');
  });

  it('should simulate hostname resolution for whitelabel tenant routing', () => {
    mockHttp.post.mockReturnValue(of({
      resolvedTenantId: '11111111-1111-1111-1111-111111111111',
      resolvedHost: 'portal.apex-marketing.com',
      resolutionType: 1,
      brandName: 'Apex Growth Partners',
      matchedDomain: 'portal.apex-marketing.com',
      isSslActive: true,
      config: component.currentConfig()
    }));

    component.hostToResolve = 'portal.apex-marketing.com';
    component.resolveHostTest();

    expect(mockHttp.post).toHaveBeenCalled();
    const result = component.resolutionResult();
    expect(result).toBeDefined();
    expect(result?.resolutionType).toBe(1);
    expect(result?.brandName).toContain('Apex Growth Partners');
    expect(component.getResolutionTypeName(1)).toBe('Exact Custom Domain Match');
  });

  it('should renew and revoke custom domain SSL certificates', () => {
    const domain = component.currentConfig().customDomains[0];
    expect(domain.sslStatus).toBe('Issued');

    component.renewSsl(domain.hostname);
    expect(mockHttp.post).toHaveBeenCalled();

    component.revokeSsl(domain.hostname);
    expect(mockHttp.post).toHaveBeenCalled();
  });

  it('should calculate contrasting text color for given hex codes', () => {
    // Dark background should yield light text (#FFFFFF)
    const darkContrast = component['calculateContrast']('#0B0F19');
    expect(darkContrast).toBe('#FFFFFF');

    // Light background should yield dark text (#0F172A)
    const lightContrast = component['calculateContrast']('#FFFFFF');
    expect(lightContrast).toBe('#0F172A');
  });
});
