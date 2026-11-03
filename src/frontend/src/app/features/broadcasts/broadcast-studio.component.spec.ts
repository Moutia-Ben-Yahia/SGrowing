// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { BroadcastStudioComponent } from './broadcast-studio.component';
import { ApiService } from '../../core/services/api.service';
import { WhatsAppTemplateDto, BroadcastCampaignDto } from '../../core/models/platform.models';

describe('BroadcastStudioComponent (WhatsApp Broadcast Studio)', () => {
  let component: BroadcastStudioComponent;
  let apiServiceMock: {
    getWhatsAppTemplates: ReturnType<typeof vi.fn>;
    getBroadcasts: ReturnType<typeof vi.fn>;
    createWhatsAppTemplate: ReturnType<typeof vi.fn>;
    createBroadcast: ReturnType<typeof vi.fn>;
    dispatchBroadcast: ReturnType<typeof vi.fn>;
  };

  const sampleTemplates: WhatsAppTemplateDto[] = [
    {
      id: 'tpl-101',
      tenantId: '00000000-0000-0000-0000-000000000001',
      name: 'flash_sale_summer',
      category: 1,
      language: 'en_US',
      headerText: 'Flash Sale',
      bodyText: 'Hello {{1}}, summer sale is now live! Use code SUMMER20.',
      footerText: 'Reply STOP to opt out',
      status: 3,
      createdAtUtc: new Date().toISOString()
    }
  ];

  const sampleCampaigns: BroadcastCampaignDto[] = [
    {
      id: 'camp-201',
      tenantId: '00000000-0000-0000-0000-000000000001',
      name: 'Summer VIP Outreach',
      templateId: 'tpl-101',
      targetTag: 'VIP_CUSTOMER',
      status: 1,
      totalRecipients: 50,
      sentCount: 0,
      createdAtUtc: new Date().toISOString()
    }
  ];

  beforeEach(() => {
    apiServiceMock = {
      getWhatsAppTemplates: vi.fn().mockReturnValue(of(sampleTemplates)),
      getBroadcasts: vi.fn().mockReturnValue(of(sampleCampaigns)),
      createWhatsAppTemplate: vi.fn(),
      createBroadcast: vi.fn(),
      dispatchBroadcast: vi.fn()
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: apiServiceMock }
      ]
    });

    component = runInInjectionContext(injector, () => new BroadcastStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load WhatsApp templates and broadcast campaigns', () => {
    expect(component).toBeTruthy();
    expect(apiServiceMock.getWhatsAppTemplates).toHaveBeenCalledWith(component.tenantId);
    expect(apiServiceMock.getBroadcasts).toHaveBeenCalledWith(component.tenantId);
    expect(component.templates().length).toBe(1);
    expect(component.campaigns().length).toBe(1);
  });

  it('should fallback to demo templates and campaigns if ApiService fails', () => {
    apiServiceMock.getWhatsAppTemplates.mockReturnValue(throwError(() => new Error('API down')));
    apiServiceMock.getBroadcasts.mockReturnValue(throwError(() => new Error('API down')));

    component.loadData();

    expect(component.templates().length).toBeGreaterThan(0);
    expect(component.campaigns().length).toBeGreaterThan(0);
  });

  it('should create new Meta template via ApiService and reset form', () => {
    const created: WhatsAppTemplateDto = {
      id: 'tpl-new-99',
      tenantId: component.tenantId,
      name: 'vip_welcome_gift',
      category: 1,
      language: 'en_US',
      headerText: 'Welcome',
      bodyText: 'Hello {{1}}, thank you for joining VIP club.',
      footerText: 'SGrowing Team',
      status: 3,
      createdAtUtc: new Date().toISOString()
    };
    apiServiceMock.createWhatsAppTemplate.mockReturnValue(of(created));

    component.newTemplateName = 'vip_welcome_gift';
    component.newTemplateCategory = 1;
    component.newTemplateHeader = 'Welcome';
    component.newTemplateBody = 'Hello {{1}}, thank you for joining VIP club.';
    component.newTemplateFooter = 'SGrowing Team';

    component.createTemplate();

    expect(apiServiceMock.createWhatsAppTemplate).toHaveBeenCalledWith(expect.objectContaining({
      name: 'vip_welcome_gift',
      bodyText: 'Hello {{1}}, thank you for joining VIP club.'
    }));
    expect(component.templates()).toContainEqual(created);
    expect(component.newTemplateName).toBe('');
  });

  it('should create campaign and dispatch it updating status to Completed', () => {
    const newCamp: BroadcastCampaignDto = {
      id: 'camp-dispatch-1',
      tenantId: component.tenantId,
      name: 'Weekend Flash Dispatch',
      templateId: 'tpl-101',
      targetTag: 'PROSPECT',
      status: 1,
      totalRecipients: 25,
      sentCount: 0,
      createdAtUtc: new Date().toISOString()
    };
    apiServiceMock.createBroadcast.mockReturnValue(of(newCamp));
    apiServiceMock.dispatchBroadcast.mockReturnValue(of({ sentCount: 25 }));

    component.newCampaignName = 'Weekend Flash Dispatch';
    component.selectedTemplateId = 'tpl-101';
    component.targetTag = 'PROSPECT';

    component.createCampaign();
    expect(component.campaigns()).toContainEqual(newCamp);

    component.dispatchCampaign('camp-dispatch-1');
    expect(apiServiceMock.dispatchBroadcast).toHaveBeenCalledWith('camp-dispatch-1', component.tenantId);

    const dispatched = component.campaigns().find(c => c.id === 'camp-dispatch-1')!;
    expect(dispatched.status).toBe(3); // Completed
    expect(dispatched.sentCount).toBe(25);
  });

  it('should return human-readable status labels for templates and campaigns', () => {
    expect(component.getStatusLabel(1)).toBe('Draft');
    expect(component.getStatusLabel(2)).toBe('Pending');
    expect(component.getStatusLabel(3)).toBe('Approved');
    expect(component.getStatusLabel(4)).toBe('Rejected');

    expect(component.getCampaignStatus(1)).toBe('Scheduled');
    expect(component.getCampaignStatus(2)).toBe('Processing');
    expect(component.getCampaignStatus(3)).toBe('Completed');
    expect(component.getCampaignStatus(4)).toBe('Failed');
  });
});
