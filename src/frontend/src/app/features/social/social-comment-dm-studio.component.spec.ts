// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { of } from 'rxjs';
import { SocialCommentDmStudioComponent } from './social-comment-dm-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import {
  SocialRuleMatchType,
  SocialCommentRuleDto,
  SocialCommentFunnelMetricsDto,
  CommentProcessingLogDto,
  TwoPhaseReactionResultDto,
  CommentProcessingStatus
} from '../../core/models/social.models';

describe('SocialCommentDmStudioComponent', () => {
  let component: SocialCommentDmStudioComponent;
  let apiMock: any;
  let authMock: any;

  const mockRules: SocialCommentRuleDto[] = [
    {
      id: 'rule-1',
      tenantId: '11111111-1111-1111-1111-111111111111',
      name: 'Price Keyword Trigger',
      platform: null,
      matchType: SocialRuleMatchType.Contains,
      keywords: ['PRICE', 'HOW MUCH'],
      discountPrefix: 'VIP',
      discountPercent: 20,
      minJitterSeconds: 5,
      maxJitterSeconds: 30,
      isEnabled: true,
      privateDmTemplate: 'Hey @{{username}}! Here is your code: {{discount_code}}',
      publicReplyTemplate: 'Sent you a DM! Check your requests.',
      createdAtUtc: '2026-11-01T10:00:00Z'
    }
  ];

  const mockFunnel: SocialCommentFunnelMetricsDto = {
    totalComments: 1420,
    matchedIntent: 680,
    publicRepliesSent: 680,
    dmsDelivered: 654,
    duplicateSkipped: 26,
    linkClicks: 312,
    conversions: 89,
    totalRevenue: 8811,
    matchRatePercentage: 47.9,
    clickThroughRatePercentage: 47.7,
    conversionRatePercentage: 28.5,
    platformBreakdowns: []
  };

  const mockLogs: CommentProcessingLogDto[] = [
    {
      id: 'log-1',
      tenantId: '11111111-1111-1111-1111-111111111111',
      platform: 'Instagram',
      postId: 'reel_1',
      commentId: 'cmt_1',
      commenterUsername: 'jane_dev',
      commentText: 'Price please!',
      matchedRuleName: 'Price Keyword Trigger',
      status: CommentProcessingStatus.FullyProcessed,
      couponCode: 'VIP-7491',
      jitterSeconds: 12,
      processedAtUtc: '2026-11-03T18:00:00Z',
      hasClicked: true,
      hasConverted: false,
      orderValue: 0
    }
  ];

  const mockReactionResult: TwoPhaseReactionResultDto = {
    commentId: 'cmt_999',
    status: CommentProcessingStatus.FullyProcessed,
    matchedRuleId: 'rule-1',
    matchedRuleName: 'Price Keyword Trigger',
    matchedKeyword: 'PRICE',
    publicReplySent: true,
    publicReplyText: 'Sent you a DM @sophia_creator!',
    privateDmDelivered: true,
    privateDmText: 'Hey @sophia_creator! Here is your VIP code: VIP-9999',
    couponCode: 'VIP-9999',
    jitterAppliedSeconds: 8,
    isDuplicateCommenter: false,
    message: 'Reaction dispatched successfully'
  };

  beforeEach(() => {
    apiMock = {
      getSocialCommentRules: vi.fn().mockReturnValue(of(mockRules)),
      getSocialCommentFunnel: vi.fn().mockReturnValue(of(mockFunnel)),
      getSocialCommentLogs: vi.fn().mockReturnValue(of(mockLogs)),
      simulateSocialCommentWebhook: vi.fn().mockReturnValue(of(mockReactionResult)),
      recordSocialCommentClick: vi.fn().mockReturnValue(of({ success: true })),
      recordSocialCommentConversion: vi.fn().mockReturnValue(of({ success: true }))
    };

    authMock = {
      currentUser: vi.fn().mockReturnValue({ id: 'u-1', tenantId: '11111111-1111-1111-1111-111111111111' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiMock },
      { provide: AuthService, useValue: authMock }
    ]);

    injector.runInContext(() => {
      component = new SocialCommentDmStudioComponent();
      component.ngOnInit();
    });
  });

  it('initializes and loads rules, funnel metrics, and audit logs', () => {
    expect(apiMock.getSocialCommentRules).toHaveBeenCalled();
    expect(apiMock.getSocialCommentFunnel).toHaveBeenCalled();
    expect(apiMock.getSocialCommentLogs).toHaveBeenCalled();

    expect(component.rules().length).toBe(1);
    expect(component.funnelMetrics()?.totalComments).toBe(1420);
    expect(component.logs().length).toBe(1);
  });

  it('switches navigation tabs between simulator, rules, funnel, and logs', () => {
    expect(component.activeTab()).toBe('simulator');

    component.activeTab.set('rules');
    expect(component.activeTab()).toBe('rules');

    component.activeTab.set('funnel');
    expect(component.activeTab()).toBe('funnel');

    component.activeTab.set('logs');
    expect(component.activeTab()).toBe('logs');
  });

  it('simulates inbound comment webhook and records two-phase reaction', () => {
    component.simComment.set('PRICE info please!');
    component.simulateComment();

    expect(apiMock.simulateSocialCommentWebhook).toHaveBeenCalled();
    expect(component.lastResult()).toEqual(mockReactionResult);
    expect(component.isSimulating()).toBe(false);
  });

  it('simulates coupon link click and conversion actions', () => {
    component.simulateClick('VIP-7491');
    expect(apiMock.recordSocialCommentClick).toHaveBeenCalledWith('VIP-7491', '11111111-1111-1111-1111-111111111111');

    component.simulateConversion('VIP-7491', 120);
    expect(apiMock.recordSocialCommentConversion).toHaveBeenCalledWith('VIP-7491', 120, '11111111-1111-1111-1111-111111111111');
  });

  it('opens new rule modal and initializes default form', () => {
    component.openNewRuleModal();
    expect(component.currentRuleForm.name).toBe('New Inbound Trigger Rule');
    expect(component.currentRuleForm.discountPercent).toBe(20);
    expect(component.currentRuleForm.isEnabled).toBe(true);
  });
});
