// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Customer360CockpitComponent } from './customer-360-cockpit.component';

describe('Customer360CockpitComponent (Unified Identity Graph & Multi-Touch Attribution)', () => {
  let component: Customer360CockpitComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue({ subscribe: () => {} }),
      post: vi.fn().mockReturnValue({ subscribe: () => {} }),
      put: vi.fn().mockReturnValue({ subscribe: () => {} }),
      delete: vi.fn().mockReturnValue({ subscribe: () => {} })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new Customer360CockpitComponent());
    component.ngOnInit();
  });

  it('should initialize with available customers and select default customer profile', () => {
    expect(component.availableCustomers().length).toBeGreaterThanOrEqual(2);
    const customer = component.currentCustomer();
    expect(customer).toBeDefined();
    expect(customer.primaryName).toBe('Samir Ben Ali');
    expect(customer.primaryPhone).toBe('+21698123456');
    expect(customer.lifetimeValue).toBe(2450.0);
    expect(customer.linkedChannels.length).toBeGreaterThanOrEqual(4);
  });

  it('should switch selected customer and update computed currentCustomer', () => {
    component.onSelectCustomer('cust-002');
    expect(component.selectedCustomerId()).toBe('cust-002');

    const customer = component.currentCustomer();
    expect(customer.primaryName).toBe('Amira Trabelsi');
    expect(customer.primaryEmail).toBe('amira@tech.tn');
    expect(customer.churnRiskLevel).toBe('Low');
  });

  it('should filter timeline events by channel reactively', () => {
    const allCount = component.timelineEvents().length;
    expect(component.filteredTimeline().length).toBe(allCount);

    // Filter by WhatsApp
    component.selectedChannelFilter.set('WhatsApp');
    const waEvents = component.filteredTimeline();
    expect(waEvents.length).toBeGreaterThan(0);
    expect(waEvents.every(e => e.channel.toLowerCase() === 'whatsapp')).toBe(true);

    // Filter by Voice
    component.selectedChannelFilter.set('Voice');
    const voiceEvents = component.filteredTimeline();
    expect(voiceEvents.length).toBeGreaterThan(0);
    expect(voiceEvents.every(e => e.channel.toLowerCase() === 'voice')).toBe(true);
  });

  it('should toggle chronological sorting of interaction events', () => {
    // Default is reverse-chronological (most recent first)
    expect(component.isChronological()).toBe(false);
    const descFirst = component.filteredTimeline()[0];

    // Toggle to chronological (oldest first)
    component.toggleSort();
    expect(component.isChronological()).toBe(true);
    const ascFirst = component.filteredTimeline()[0];

    expect(new Date(ascFirst.occurredAtUtc).getTime()).toBeLessThan(new Date(descFirst.occurredAtUtc).getTime());
  });

  it('should provide multi-touch attribution models with revenue breakdown', () => {
    const models = component.modelResultsMap();
    expect(models).toBeDefined();

    // 1: FirstTouch, 2: LastTouch, 3: Linear, 5: TimeDecay, 6: U-Shaped
    expect(models[1].modelName).toBe('First-Touch');
    expect(models[2].modelName).toBe('Last-Touch');
    expect(models[3].modelName).toBe('Linear');
    expect(models[5].modelName).toBe('Time-Decay');
    expect(models[6].modelName).toBe('U-Shaped');
  });

  it('should calculate true ROAS and CAC for marketing campaigns', () => {
    const campaigns = component.campaignRoas();
    expect(campaigns.length).toBeGreaterThanOrEqual(3);

    const tiktokCamp = campaigns.find(c => c.campaignName.includes('Viral_Fall_Hook'));
    expect(tiktokCamp).toBeDefined();
    expect(tiktokCamp!.trueRoas).toBeGreaterThan(0);
    expect(tiktokCamp!.cac).toBeGreaterThan(0);
  });

  it('should trigger identity stitch HTTP request with unified payload', () => {
    component.stitchPhone = '+21699887766';
    component.stitchEmail = 'new.stitched@enterprise.tn';
    component.stitchName = 'Samir Ben Ali';
    component.executeIdentityStitch();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/crm/customer-360/identity/stitch',
      expect.objectContaining({
        phone: '+21699887766',
        email: 'new.stitched@enterprise.tn',
        name: 'Samir Ben Ali'
      })
    );
  });
});
