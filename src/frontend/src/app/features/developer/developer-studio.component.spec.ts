// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { DeveloperStudioComponent } from './developer-studio.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { TenantApiKeyDto, WebhookSubscriptionRecordDto, WebhookDeliveryAttemptLogDto } from '../../core/models/developer-webhooks.models';
import { B2BCreditAccountDto, FactoringReceivableRecordDto } from '../../core/models/horizons-advanced.models';

describe('DeveloperStudioComponent (Developer API, Webhooks Mesh & B2B Financials)', () => {
  let component: DeveloperStudioComponent;
  let apiServiceMock: {
    getApiKeys: ReturnType<typeof vi.fn>;
    createApiKey: ReturnType<typeof vi.fn>;
    revokeApiKey: ReturnType<typeof vi.fn>;
    getWebhookSubscriptions: ReturnType<typeof vi.fn>;
    createWebhookSubscription: ReturnType<typeof vi.fn>;
    getWebhookLogs: ReturnType<typeof vi.fn>;
    getB2BFinancialSummary: ReturnType<typeof vi.fn>;
    replayDeveloperWebhook: ReturnType<typeof vi.fn>;
    fundFactoringAdvance: ReturnType<typeof vi.fn>;
    testDispatchWebhook: ReturnType<typeof vi.fn>;
  };
  let authServiceMock: {
    currentTenant: ReturnType<typeof vi.fn>;
  };

  const sampleApiKey: TenantApiKeyDto = {
    id: 'key-101',
    tenantId: 'a0000000-0000-0000-0000-000000000001',
    name: 'ERP Automation Engine',
    tokenPrefix: 'sg_live_7a8b',
    plainKey: 'sg_live_7a8b9c0d1e2f3a4b5c6d',
    scopes: ['orders:read', 'orders:write'],
    isLive: true,
    isRevoked: false,
    rateLimitPerMinute: 240,
    createdAtUtc: new Date().toISOString()
  };

  const sampleWebhook: WebhookSubscriptionRecordDto = {
    id: 'wh-201',
    tenantId: 'a0000000-0000-0000-0000-000000000001',
    targetUrl: 'https://api.merchant-portal.tn/events/incoming',
    secretMasked: 'whsec_••••••••••••34f2',
    subscribedEvents: ['order.delivered'],
    isActive: true,
    createdAtUtc: new Date().toISOString()
  };

  const sampleLog: WebhookDeliveryAttemptLogDto = {
    id: 'log-301',
    tenantId: 'a0000000-0000-0000-0000-000000000001',
    subscriptionId: 'wh-201',
    eventType: 'order.delivered',
    targetUrl: 'https://api.merchant-portal.tn/events/incoming',
    payloadJson: '{"orderId":"ORD-99"}',
    responseStatusCode: 200,
    isSuccess: true,
    executionDurationMs: 45,
    attemptNumber: 1,
    attemptedAtUtc: new Date().toISOString()
  };

  beforeEach(() => {
    apiServiceMock = {
      getApiKeys: vi.fn().mockReturnValue(of([sampleApiKey])),
      createApiKey: vi.fn().mockReturnValue(of(sampleApiKey)),
      revokeApiKey: vi.fn().mockReturnValue(of(true)),
      getWebhookSubscriptions: vi.fn().mockReturnValue(of([sampleWebhook])),
      createWebhookSubscription: vi.fn().mockReturnValue(of(sampleWebhook)),
      getWebhookLogs: vi.fn().mockReturnValue(of([sampleLog])),
      getB2BFinancialSummary: vi.fn().mockReturnValue(of({
        creditAccounts: [
          {
            id: 'b2b-1',
            tenantId: 'a0000000-0000-0000-0000-000000000001',
            companyId: 'comp-101',
            companyName: 'MedTech Distribution SARL',
            creditLimit: 50000.0,
            outstandingBalance: 14200.0,
            availableCredit: 35800.0,
            paymentTermsDays: 30,
            creditScoreRating: 'AAA',
            isFrozen: false,
            createdAtUtc: new Date().toISOString()
          }
        ],
        factoringReceivables: [],
        totalFactoringAvailable: 21000,
        totalCreditExposure: 82000
      })),
      replayDeveloperWebhook: vi.fn().mockReturnValue(of({
        ...sampleLog,
        id: 'log-replay-99',
        responseStatusCode: 200
      })),
      fundFactoringAdvance: vi.fn().mockReturnValue(of({ success: true })),
      testDispatchWebhook: vi.fn().mockReturnValue(of({ logs: [sampleLog] }))
    };

    authServiceMock = {
      currentTenant: vi.fn().mockReturnValue({
        id: 'a0000000-0000-0000-0000-000000000001',
        name: 'Developer Workspace'
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: apiServiceMock },
        { provide: AuthService, useValue: authServiceMock }
      ]
    });

    component = runInInjectionContext(injector, () => new DeveloperStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load API keys, webhooks, delivery logs, and B2B financials', () => {
    expect(component).toBeTruthy();
    expect(apiServiceMock.getApiKeys).toHaveBeenCalledWith(component.tenantId);
    expect(apiServiceMock.getWebhookSubscriptions).toHaveBeenCalledWith(component.tenantId);
    expect(apiServiceMock.getWebhookLogs).toHaveBeenCalledWith(component.tenantId);
    expect(component.apiKeys().length).toBe(1);
    expect(component.subscriptions().length).toBe(1);
    expect(component.logs().length).toBe(1);
    expect(component.creditAccounts().length).toBe(1);
  });

  it('should switch navigation tabs across keys, webhooks, logs, and factoring', () => {
    component.activeTab.set('webhooks');
    expect(component.activeTab()).toBe('webhooks');

    component.activeTab.set('logs');
    expect(component.activeTab()).toBe('logs');

    component.activeTab.set('b2b-factoring');
    expect(component.activeTab()).toBe('b2b-factoring');

    component.activeTab.set('keys');
    expect(component.activeTab()).toBe('keys');
  });

  it('should revoke API key and mark it revoked in signal state', () => {
    component.revokeKey('key-101');

    expect(apiServiceMock.revokeApiKey).toHaveBeenCalledWith(component.tenantId, 'key-101');
    const revoked = component.apiKeys().find(k => k.id === 'key-101');
    expect(revoked?.isRevoked).toBe(true);
  });

  it('should replay webhook event and append replayed log to signal list', () => {
    component.replayWebhook('log-301');

    expect(apiServiceMock.replayDeveloperWebhook).toHaveBeenCalledWith('log-301', component.tenantId);
    expect(component.logs().some(l => l.id === 'log-replay-99')).toBe(true);
  });

  it('should fund factoring advance and mark receivable as Funded', () => {
    component.factoringReceivables.set([
      {
        id: 'fact-1',
        tenantId: component.tenantId,
        courierProviderId: 'cr-yalidine',
        courierName: 'Yalidine Express',
        inTransitCashFloat: 18500.0,
        verifiedDeliveredCash: 12400.0,
        advanceDiscountRatePercentage: 2.5,
        immediateCashAvailable: 12090.0,
        status: 'Available',
        createdAtUtc: new Date().toISOString()
      }
    ]);

    component.fundAdvance('fact-1');

    expect(apiServiceMock.fundFactoringAdvance).toHaveBeenCalledWith('fact-1', component.tenantId);
    const updated = component.factoringReceivables().find(f => f.id === 'fact-1');
    expect(updated?.status).toBe('Funded');
  });
});
