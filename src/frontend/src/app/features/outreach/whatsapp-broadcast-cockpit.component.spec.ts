// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { WhatsAppBroadcastCockpitComponent } from './whatsapp-broadcast-cockpit.component';

describe('WhatsAppBroadcastCockpitComponent', () => {
  let component: WhatsAppBroadcastCockpitComponent;

  beforeEach(() => {
    component = new WhatsAppBroadcastCockpitComponent();
  });

  it('should initialize with approved templates and draft campaign', () => {
    expect(component).toBeTruthy();
    expect(component.templates().length).toBeGreaterThanOrEqual(2);
    expect(component.campaign().status).toBe('Draft');
    expect(component.campaign().total).toBe(25000);
  });

  it('should dispatch campaign and update delivery metrics', () => {
    component.dispatchCampaign();

    expect(component.campaign().status).toBe('Completed');
    expect(component.campaign().sent).toBe(25000);
    expect(component.campaign().delivered).toBeGreaterThan(24000);
    expect(component.campaign().read).toBeGreaterThan(19000);
    expect(component.deliveredPercent()).toBeGreaterThanOrEqual(95);
  });

  it('should support switching selected template and calculate 0 percent metrics in draft state', () => {
    expect(component.deliveredPercent()).toBe(0);
    expect(component.readPercent()).toBe(0);

    const utilityTemplate = component.templates()[1];
    expect(utilityTemplate.category).toBe('UTILITY');

    component.selectedTemplate.set(utilityTemplate);
    expect(component.selectedTemplate().name).toBe('order_dispatch_verification');
    expect(component.selectedTemplate().buttons.length).toBe(2);
  });
});
