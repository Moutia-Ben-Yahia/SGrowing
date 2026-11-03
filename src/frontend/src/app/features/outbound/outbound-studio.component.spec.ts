// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { of, throwError } from 'rxjs';
import { OutboundStudioComponent } from './outbound-studio.component';
import { ApiService } from '../../core/services/api.service';
import { CampaignDto, SenderAccountDto, DealDto } from '../../core/models/outbound.models';

describe('OutboundStudioComponent', () => {
  let component: OutboundStudioComponent;
  let apiMock: any;

  const mockCampaigns: CampaignDto[] = [
    {
      id: 'camp-100',
      tenantId: '00000000-0000-0000-0000-000000000001',
      name: 'Q4 Enterprise SaaS Cadence',
      status: 2,
      createdAtUtc: '2026-11-01T10:00:00Z',
      steps: [
        {
          stepNumber: 1,
          delayDays: 0,
          channel: 1,
          subjectTemplate: 'Accelerating AI SDR with SGrowing',
          bodyTemplate: 'Hi {{first_name}}, let us talk.'
        }
      ]
    }
  ];

  const mockMailboxes: SenderAccountDto[] = [
    {
      id: 'mb-100',
      tenantId: '00000000-0000-0000-0000-000000000001',
      email: 'sdr.lead@sgrowing.io',
      displayName: 'Lead SDR',
      provider: 1,
      dailyQuota: 50,
      sentTodayCount: 15,
      warmupEnabled: true,
      warmupRampDailyIncrement: 5,
      healthScore: 99,
      isActive: true
    }
  ];

  const mockDeals: DealDto[] = [
    {
      id: 'deal-100',
      tenantId: '00000000-0000-0000-0000-000000000001',
      contactId: 'c-100',
      title: 'Deal: Acme FinTech - 10 Seats',
      amount: 15000,
      stage: 1,
      notes: 'Initial reply captured by AI SDR',
      createdAtUtc: '2026-11-02T10:00:00Z'
    },
    {
      id: 'deal-200',
      tenantId: '00000000-0000-0000-0000-000000000001',
      contactId: 'c-200',
      title: 'Deal: Mega Logistics - Enterprise SLA',
      amount: 32000,
      stage: 2,
      notes: 'Demo scheduled',
      createdAtUtc: '2026-11-02T14:00:00Z'
    }
  ];

  beforeEach(() => {
    apiMock = {
      getCampaigns: vi.fn().mockReturnValue(of(mockCampaigns)),
      getMailboxes: vi.fn().mockReturnValue(of(mockMailboxes)),
      getDeals: vi.fn().mockReturnValue(of(mockDeals)),
      getDialerQueue: vi.fn().mockReturnValue(of([])),
      triggerDispatch: vi.fn().mockReturnValue(of({ processed: 8 }))
    };

    // Mock window.alert
    vi.spyOn(window, 'alert').mockImplementation(() => {});

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiMock }
    ]);

    injector.runInContext(() => {
      component = new OutboundStudioComponent();
      component.ngOnInit();
    });
  });

  it('initializes and populates campaigns, mailboxes, and deals from API', () => {
    expect(apiMock.getCampaigns).toHaveBeenCalled();
    expect(apiMock.getMailboxes).toHaveBeenCalled();
    expect(apiMock.getDeals).toHaveBeenCalled();

    expect(component.campaigns().length).toBe(1);
    expect(component.mailboxes().length).toBe(1);
    expect(component.deals().length).toBe(2);
  });

  it('falls back to mock data when API calls fail', () => {
    apiMock.getCampaigns.mockReturnValue(throwError(() => new Error('Network error')));
    apiMock.getMailboxes.mockReturnValue(throwError(() => new Error('Network error')));
    apiMock.getDeals.mockReturnValue(throwError(() => new Error('Network error')));

    component.loadData();

    expect(component.campaigns().length).toBeGreaterThan(0);
    expect(component.mailboxes().length).toBeGreaterThan(0);
    expect(component.deals().length).toBeGreaterThan(0);
  });

  it('filters deals by stage accurately', () => {
    const stage1Deals = component.getDealsByStage(1);
    expect(stage1Deals.length).toBe(1);
    expect(stage1Deals[0].id).toBe('deal-100');

    const stage2Deals = component.getDealsByStage(2);
    expect(stage2Deals.length).toBe(1);
    expect(stage2Deals[0].id).toBe('deal-200');

    const stage3Deals = component.getDealsByStage(3);
    expect(stage3Deals.length).toBe(0);
  });

  it('resolves email provider names correctly', () => {
    expect(component.getProviderName(1)).toBe('Google Workspace');
    expect(component.getProviderName(2)).toBe('Microsoft 365');
    expect(component.getProviderName(3)).toBe('Custom SMTP');
  });

  it('triggers cadence dispatch and alerts result', () => {
    component.triggerDispatch();

    expect(apiMock.triggerDispatch).toHaveBeenCalledWith(component.tenantId);
    expect(component.isDispatching()).toBe(false);
    expect(window.alert).toHaveBeenCalledWith('Dispatched 8 pending cadence emails.');
  });
});
