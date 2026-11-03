import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { ApiService } from './api.service';

describe('ApiService', () => {
  let service: ApiService;
  let mockHttp: any;
  let injector: Injector;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn()
    };

    injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    service = runInInjectionContext(injector, () => new ApiService());
  });

  it('should fetch CRM contacts with query parameter', () => {
    const mockContacts = [{ id: 'c-1', name: 'John Doe', email: 'john@example.com' }];
    mockHttp.get.mockReturnValue(of(mockContacts));

    let result: any;
    service.getContacts('tenant-1', 'john').subscribe(res => result = res);

    expect(result).toEqual(mockContacts);
    expect(mockHttp.get).toHaveBeenCalledWith('http://localhost:5000/api/crm/contacts?tenantId=tenant-1&query=john');
  });

  it('should fetch messages and post new message', () => {
    const mockMessages = [{ id: 'm-1', content: 'Hello' }];
    mockHttp.get.mockReturnValue(of(mockMessages));
    mockHttp.post.mockReturnValue(of({ id: 'm-2', content: 'World' }));

    service.getMessages('conv-1', 'tenant-1').subscribe();
    expect(mockHttp.get).toHaveBeenCalledWith('http://localhost:5000/api/conversations/conv-1/messages?tenantId=tenant-1');

    service.sendMessage('conv-1', { content: 'World' }).subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/conversations/conv-1/messages', { content: 'World' });
  });

  it('should approve AI draft and update status', () => {
    mockHttp.post.mockReturnValue(of({ success: true }));

    service.approveAiDraft('draft-1', 'tenant-1').subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/ai/drafts/draft-1/approve', {
      tenantId: 'tenant-1',
      draftId: 'draft-1'
    });

    service.changeStatus('conv-1', 'tenant-1', 2).subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/conversations/conv-1/status', {
      tenantId: 'tenant-1',
      conversationId: 'conv-1',
      status: 2
    });
  });

  it('should manage outbound campaigns and dispatch', () => {
    mockHttp.get.mockReturnValue(of([]));
    mockHttp.post.mockReturnValue(of({ enrolledCount: 5 }));

    service.getCampaigns('tenant-1').subscribe();
    expect(mockHttp.get).toHaveBeenCalledWith('http://localhost:5000/api/outbound/campaigns?tenantId=tenant-1');

    service.enrollProspects('camp-1', 'tenant-1', ['c-1', 'c-2']).subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/outbound/campaigns/camp-1/enroll?tenantId=tenant-1', ['c-1', 'c-2']);

    service.triggerDispatch('tenant-1').subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/outbound/dispatch?tenantId=tenant-1', {});
  });

  it('should manage journeys and surveys', () => {
    mockHttp.get.mockReturnValue(of([]));
    mockHttp.post.mockReturnValue(of({ id: 'survey-resp-1' }));

    service.getJourneys('tenant-1').subscribe();
    expect(mockHttp.get).toHaveBeenCalledWith('http://localhost:5000/api/journeys?tenantId=tenant-1');

    service.submitFeedback('tenant-1', 'form-1', 'contact-1', 5, 'Great service!').subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/feedback/submit?tenantId=tenant-1', {
      surveyFormId: 'form-1',
      contactId: 'contact-1',
      score: 5,
      feedbackText: 'Great service!'
    });
  });

  it('should handle appointment booking and checkout links', () => {
    mockHttp.post.mockReturnValue(of({ id: 'booking-1' }));

    service.bookAppointment('tenant-1', 'slot-1', 'contact-1', '2026-11-04T10:00:00Z').subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/sales/booking/book?tenantId=tenant-1', {
      slotId: 'slot-1',
      contactId: 'contact-1',
      scheduledTimeUtc: '2026-11-04T10:00:00Z'
    });

    service.createCheckoutLink('tenant-1', 'contact-1', 'conv-1', 'Pro Plan', 99, 'USD').subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/sales/checkout/create?tenantId=tenant-1', {
      contactId: 'contact-1',
      conversationId: 'conv-1',
      title: 'Pro Plan',
      amount: 99,
      currency: 'USD'
    });
  });

  it('should execute SEO audit and funnel calls', () => {
    mockHttp.post.mockReturnValue(of({ report: {}, pages: [] }));
    mockHttp.get.mockReturnValue(of([]));

    service.runSeoAudit({ targetUrl: 'https://store.sgrowing.io' }).subscribe();
    expect(mockHttp.post).toHaveBeenCalledWith('http://localhost:5000/api/seo/audits/run', {
      targetUrl: 'https://store.sgrowing.io'
    });

    service.getFunnels('tenant-1').subscribe();
    expect(mockHttp.get).toHaveBeenCalledWith('http://localhost:5000/api/funnels?tenantId=tenant-1');
  });
});
