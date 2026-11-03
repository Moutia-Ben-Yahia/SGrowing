// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { DeveloperWebhooksCockpitComponent } from './developer-webhooks-cockpit.component';

describe('DeveloperWebhooksCockpitComponent (Outward Webhook Delivery Engine & Public SDK)', () => {
  let component: DeveloperWebhooksCockpitComponent;

  beforeEach(() => {
    component = new DeveloperWebhooksCockpitComponent();
  });

  it('should initialize with default subscriptions, logs, and DLQ entries', () => {
    expect(component).toBeTruthy();
    expect(component.subscriptions().length).toBe(2);
    expect(component.logs().length).toBe(3);
    expect(component.dlqLogs().length).toBe(1);
    expect(component.activeEndpointsCount()).toBe(2);
  });

  it('should toggle event selection in creation dialog', () => {
    expect(component.isEventSelected('deal.won')).toBe(true);

    component.toggleEventSelection('deal.won');
    expect(component.isEventSelected('deal.won')).toBe(false);

    component.toggleEventSelection('deal.won');
    expect(component.isEventSelected('deal.won')).toBe(true);
  });

  it('should toggle subscription active status', () => {
    const sub = component.subscriptions()[0];
    expect(sub.isActive).toBe(true);

    component.toggleSubscription(sub.id);
    let updated = component.subscriptions().find(s => s.id === sub.id)!;
    expect(updated.isActive).toBe(false);
    expect(component.activeEndpointsCount()).toBe(1);

    component.toggleSubscription(sub.id);
    updated = component.subscriptions().find(s => s.id === sub.id)!;
    expect(updated.isActive).toBe(true);
    expect(component.activeEndpointsCount()).toBe(2);
  });

  it('should trigger test dispatch and prepend new delivery log', () => {
    const logsBefore = component.logs().length;
    component.triggerTestDispatch();

    expect(component.logs().length).toBe(logsBefore + 1);
    const newest = component.logs()[0];
    expect(newest.eventType).toBe('contact.created');
    expect(newest.httpStatusCode).toBe(200);
    expect(newest.signatureHeader).toContain('v1=');
  });

  it('should retry DLQ item and remove it from dead letter queue', () => {
    const dlqItem = component.dlqLogs()[0];
    expect(dlqItem).toBeTruthy();

    component.retryDlqItem(dlqItem.id);

    expect(component.dlqLogs().length).toBe(0);
    expect(component.logs().length).toBeGreaterThan(3);
  });

  it('should add new webhook endpoint and close modal', () => {
    component.newTargetUrl = 'https://partner-portal.com/webhooks/incoming';
    component.selectedEvents.set(['contact.created', 'order.delivered']);

    component.addEndpoint();

    expect(component.subscriptions().some(s => s.targetUrl === 'https://partner-portal.com/webhooks/incoming')).toBe(true);
    expect(component.showCreateModal()).toBe(false);
  });
});
