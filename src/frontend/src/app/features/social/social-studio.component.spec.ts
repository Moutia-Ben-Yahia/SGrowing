// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { of } from 'rxjs';
import { SocialStudioComponent } from './social-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SocialPlatform, TokenHealthAuditReportDto } from '../../core/models/social.models';

describe('SocialStudioComponent', () => {
  let component: SocialStudioComponent;
  let apiMock: any;
  let authMock: any;

  const mockTokenAudit: TokenHealthAuditReportDto = {
    tenantId: '11111111-1111-1111-1111-111111111111',
    totalAccountsAudited: 4,
    healthyAccountsCount: 4,
    expiringOrRequiredRefreshCount: 0,
    accountsRequiringRefresh: [],
    auditedAtUtc: '2026-11-03T20:00:00Z'
  };

  beforeEach(() => {
    apiMock = {
      getSocialPosts: vi.fn().mockReturnValue(of([])),
      auditSocialTokenHealth: vi.fn().mockReturnValue(of(mockTokenAudit)),
      generateViralHooks: vi.fn().mockReturnValue(of([
        { hookText: 'Why 99% of B2B outbound fails:', angle: 'Contrarian', estimatedViralityScore: 95 }
      ])),
      rewritePostForPlatforms: vi.fn().mockReturnValue(of([
        { platform: SocialPlatform.X, adaptedContent: 'Short X post' }
      ])),
      createSocialPost: vi.fn().mockReturnValue(of({ id: 'post-1' })),
      publishDueSocialPosts: vi.fn().mockReturnValue(of({ count: 2 })),
      deflectSocialComment: vi.fn().mockReturnValue(of({ replyText: 'Sent you details via DM!' }))
    };

    authMock = {
      currentTenant: vi.fn().mockReturnValue({ id: 'tenant-111', name: 'Acme HQ' }),
      currentUser: vi.fn().mockReturnValue({ id: 'u-1', tenantId: 'tenant-111' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiMock },
      { provide: AuthService, useValue: authMock }
    ]);

    injector.runInContext(() => {
      component = new SocialStudioComponent();
      component.ngOnInit();
    });
  });

  it('initializes and audits token health', () => {
    expect(apiMock.getSocialPosts).toHaveBeenCalled();
    expect(apiMock.auditSocialTokenHealth).toHaveBeenCalled();
    expect(component.hasTokenAlerts()).toBe(false);
    expect(component.tokenStatusText()).toContain('Tokens Healthy');
  });

  it('toggles social network accounts selection', () => {
    const xAccount = component.accounts().find(a => a.platform === SocialPlatform.X)!;
    expect(xAccount.selected).toBe(true);

    component.toggleAccount(xAccount);
    expect(xAccount.selected).toBe(false);

    component.toggleAccount(xAccount);
    expect(xAccount.selected).toBe(true);
  });

  it('generates viral hook suggestions and injects hook into content', () => {
    component.generateHooks();
    expect(apiMock.generateViralHooks).toHaveBeenCalled();

    component.injectHook('Why 99% of B2B outbound fails:');
    expect(component.postText).toContain('Why 99% of B2B outbound fails:');
  });

  it('adapts post content and calculates remaining characters per platform', () => {
    const xContent = component.getAdaptedContent(SocialPlatform.X);
    expect(xContent).toBeDefined();

    const remainingX = component.getRemainingChars(SocialPlatform.X);
    expect(typeof remainingX).toBe('number');

    const limitLinkedIn = component.getPlatformLimit(SocialPlatform.LinkedIn);
    expect(limitLinkedIn).toBe(3000);
  });

  it('deflects public comments to private DM', () => {
    const comment = component.comments()[0];
    expect(comment.deflected).toBe(false);

    component.deflectToDm(comment);

    expect(apiMock.deflectSocialComment).toHaveBeenCalled();
    expect(comment.deflected).toBe(true);
    expect(component.feedbackMessage()).toContain('Sent automated private DM');
  });
});
