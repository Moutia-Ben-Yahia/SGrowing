// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { OmnichannelUnifiedDeskComponent } from './omnichannel-unified-desk.component';

describe('OmnichannelUnifiedDeskComponent', () => {
  let component: OmnichannelUnifiedDeskComponent;
  let mockApi: any;

  const mockConversations = [
    {
      conversationId: 'c1',
      tenantId: 't1',
      contactId: 'ct1',
      customerName: 'Samir Ben Ali',
      customerContactInfo: '+21698000111',
      channelType: 'WhatsApp',
      status: 1,
      snippet: 'Where is my order?',
      unreadCount: 1,
      lastMessageAtUtc: '2026-11-03T10:00:00Z',
      activeAgentsCount: 1,
      slaStatus: 'Normal'
    },
    {
      conversationId: 'c2',
      tenantId: 't1',
      contactId: 'ct2',
      customerName: 'Leila Mansour',
      customerContactInfo: 'leila@example.com',
      channelType: 'Email',
      status: 2,
      snippet: 'Can I change my address?',
      unreadCount: 0,
      lastMessageAtUtc: '2026-11-03T09:30:00Z',
      activeAgentsCount: 0,
      slaStatus: 'Breached'
    }
  ];

  const mockDetail = {
    conversationId: 'c1',
    tenantId: 't1',
    customerName: 'Samir Ben Ali',
    channelType: 'WhatsApp',
    status: 1,
    messages: [
      { id: 'm1', content: 'Where is my order?', senderType: 1, createdAtUtc: '2026-11-03T10:00:00Z', isPrivateNote: false }
    ],
    customer360: {
      customerId: 'ct1',
      customerName: 'Samir Ben Ali',
      lifecycleStage: 'VIP Active',
      totalOrders: 12,
      totalSpent: 1850,
      deliverySuccessRate: 98,
      recentOrders: []
    },
    copilotSuggestions: [
      { id: 's1', title: 'Tracking Status', suggestedText: 'Your order is en route.', confidenceScore: 0.94, groundingSource: 'Courier CAPI' }
    ],
    presence: {
      conversationId: 'c1',
      activeAgents: [{ agentId: 'agent-current', agentName: 'Agent Sarah', isTyping: false }]
    }
  };

  const mockSlaOverview = {
    tenantId: 't1',
    totalActiveConversations: 14,
    pausedOnHoldCount: 3,
    breachedCount: 1,
    slaTargetComplianceRatio: 0.93
  };

  beforeEach(() => {
    mockApi = {
      getUnifiedDeskConversations: vi.fn().mockReturnValue(of(mockConversations)),
      getUnifiedDeskSlaOverview: vi.fn().mockReturnValue(of(mockSlaOverview)),
      getUnifiedDeskConversationDetail: vi.fn().mockReturnValue(of(mockDetail)),
      updateUnifiedDeskPresence: vi.fn().mockReturnValue(of(mockDetail.presence)),
      sendUnifiedDeskMessage: vi.fn().mockReturnValue(of({ softLockTriggered: false, messageId: 'm2' })),
      resolveUnifiedDeskAutopilot: vi.fn().mockReturnValue(of({
        intent: 'OrderTracking',
        confidenceScore: 0.98,
        actionTaken: 'Sent live tracking link to customer'
      })),
      updateUnifiedDeskStatus: vi.fn().mockReturnValue(of({ success: true }))
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi }
      ]
    });

    component = runInInjectionContext(injector, () => new OmnichannelUnifiedDeskComponent());
    component.ngOnInit();
  });

  it('should initialize and load omnichannel conversations and SLA metrics', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getUnifiedDeskConversations).toHaveBeenCalled();
    expect(mockApi.getUnifiedDeskSlaOverview).toHaveBeenCalled();
    expect(component.conversations().length).toBe(2);
    expect(component.selectedConv()?.conversationId).toBe('c1');
    expect(component.selectedConvDetail()?.customerName).toBe('Samir Ben Ali');
  });

  it('should filter conversations by search query', () => {
    component.searchQuery = 'Leila';
    component.conversations.update(c => [...c]);
    expect(component.filteredConversations().length).toBe(1);
    expect(component.filteredConversations()[0].customerName).toBe('Leila Mansour');

    component.searchQuery = 'non-existent';
    component.conversations.update(c => [...c]);
    expect(component.filteredConversations().length).toBe(0);
  });

  it('should switch channel filter and reload conversations', () => {
    component.selectChannelFilter('WhatsApp');
    expect(component.selectedChannel()).toBe('WhatsApp');
    expect(mockApi.getUnifiedDeskConversations).toHaveBeenCalledWith('WhatsApp');
  });

  it('should send a reply message and clear composer', () => {
    component.composerText = 'Your order will arrive today.';
    component.sendMessage();

    expect(mockApi.sendUnifiedDeskMessage).toHaveBeenCalledWith('c1', expect.objectContaining({
      content: 'Your order will arrive today.',
      isPrivateNote: false
    }));
    expect(component.composerText).toBe('');
  });

  it('should apply AI copilot suggestion to composer', () => {
    component.useCopilotSuggestion('Your order is en route.');
    expect(component.composerText).toBe('Your order is en route.');
    expect(component.isPrivateNote()).toBe(false);
  });

  it('should simulate AI autopilot inquiry resolution', () => {
    component.simulateAutopilot('Where is my shipment for order #ORD-7721?');
    expect(mockApi.sendUnifiedDeskMessage).toHaveBeenCalledWith('c1', expect.objectContaining({
      content: 'Where is my shipment for order #ORD-7721?'
    }));
    expect(mockApi.resolveUnifiedDeskAutopilot).toHaveBeenCalledWith('c1', 'Where is my shipment for order #ORD-7721?', true);
    expect(component.lastAutopilotResolution()?.intent).toBe('OrderTracking');
  });
});
